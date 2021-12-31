using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using NLua;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.DataModels;

namespace ScriptRenamer
{
    [Renamer(RenamerId)]
    public class ScriptRenamer : IRenamer
    {
        public const string RenamerId = nameof(ScriptRenamer);
        private static string _script = string.Empty;
        private static ParserRuleContext _context;
        private static Exception _contextException;
        private static readonly Type Repofact = GetTypeFromAssemblies("Shoko.Server.Repositories.RepoFactory");
        private static readonly dynamic VideoLocalRepo = Repofact.GetProperty("VideoLocal")?.GetValue(null);
        private static readonly dynamic ImportFolderRepo = Repofact.GetProperty("ImportFolder")?.GetValue(null);

        public (IImportFolder destination, string subfolder) GetDestination(MoveEventArgs args)
        {
            var visitor = new ScriptRenamerVisitor(args);
            if (CheckBadArgs(visitor))
            {
                args.Cancel = true;
                return (null, null);
            }
            SetupAndLaunch(visitor);
            if (visitor.FindLastLocation)
            {
                IImportFolder oldFld = null;
                string subFld = null;
                var lastFileLocation = (IVideoFile)((IEnumerable<dynamic>)VideoLocalRepo.GetByAniDBAnimeID(visitor.AnimeInfo.AnimeID))
                    .Where(vl => !string.Equals(vl.CRC32, visitor.FileInfo.Hashes.CRC, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(vl => vl.DateTimeUpdated)
                    .Select(vl => vl.GetBestVideoLocalPlace())
                    .FirstOrDefault(vlp => (oldFld = (IImportFolder)ImportFolderRepo.GetByID(vlp.ImportFolderID)) is not null &&
                                           (oldFld.DropFolderType.HasFlag(DropFolderType.Destination) ||
                                            oldFld.DropFolderType.HasFlag(DropFolderType.Excluded)));
                if (lastFileLocation is not null)
                {
                    var oldLoc = NormPath(oldFld.Location);
                    subFld = Path.GetRelativePath(oldLoc, Path.GetDirectoryName(lastFileLocation.FilePath)!);
                }
                if (oldFld is not null && subFld is not null)
                    return (oldFld, subFld);
            }
            var destfolder = GetNewDestination(visitor);
            var subfolder = GetNewSubfolder(args, visitor);
            return (destfolder, subfolder);
        }

        public string GetFilename(RenameEventArgs args)
        {
            var mvEventArgs = new MoveEventArgs
            {
                Cancel = args.Cancel,
                AvailableFolders = ((IEnumerable)ImportFolderRepo.GetAll()).Cast<IImportFolder>()
                    .Where(a => a.DropFolderType != DropFolderType.Excluded).ToList<IImportFolder>(),
                FileInfo = args.FileInfo,
                AnimeInfo = args.AnimeInfo,
                GroupInfo = args.GroupInfo,
                EpisodeInfo = args.EpisodeInfo,
                Script = args.Script
            };
            var visitor = new ScriptRenamerVisitor(mvEventArgs);
            if (CheckBadArgs(visitor))
            {
                args.Cancel = true;
                return null;
            }
            SetupAndLaunch(visitor);
            return !string.IsNullOrWhiteSpace(visitor.Filename)
                ? RemoveInvalidFilenameChars(visitor.RemoveReservedChars ? visitor.Filename : visitor.Filename.ReplaceInvalidPathCharacters()) +
                  Path.GetExtension(args.FileInfo.Filename)
                : null;
        }

        private static Type GetTypeFromAssemblies(string typeName)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(currentassembly => currentassembly.GetType(typeName, false, true))
                .FirstOrDefault(t => t is not null);
        }

        private static string GetNewSubfolder(MoveEventArgs args, ScriptRenamerVisitor visitor)
        {
            if (visitor.Subfolder is null)
                return RemoveInvalidFilenameChars(args.AnimeInfo.OrderBy(a => a.AnimeID).First().PreferredTitle is var title && visitor.RemoveReservedChars
                    ? title
                    : title.ReplaceInvalidPathCharacters());
            var subfolder = string.Empty;
            var newsubfoldersplit = visitor.Subfolder.Trim((char)0x1F).Split((char)0x1F)
                .Select(f => RemoveInvalidFilenameChars(visitor.RemoveReservedChars ? f : f.ReplaceInvalidPathCharacters())).ToArray();
            subfolder = newsubfoldersplit.Aggregate(subfolder, (current, t) => current + (t + '/'));
            subfolder = NormPath(subfolder);
            return subfolder;
        }

        private static IImportFolder GetNewDestination(ScriptRenamerVisitor visitor)
        {
            IImportFolder destfolder;
            if (string.IsNullOrWhiteSpace(visitor.Destination))
            {
                destfolder = visitor.AvailableFolders
                    // Order by common prefix (stronger version of same drive)
                    .OrderBy(f => string.Concat(NormPath(visitor.FileInfo.FilePath)
                        .TakeWhile((ch, i) => i < NormPath(f.Location).Length
                                              && char.ToUpperInvariant(NormPath(f.Location)[i]) == char.ToUpperInvariant(ch))).Length)
                    .FirstOrDefault(f => f.DropFolderType.HasFlag(DropFolderType.Destination));
            }
            else
            {
                destfolder = visitor.AvailableFolders.FirstOrDefault(f =>
                    f.DropFolderType.HasFlag(DropFolderType.Destination)
                    && (string.Equals(f.Name, visitor.Destination, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(NormPath(f.Location), NormPath(visitor.Destination), StringComparison.OrdinalIgnoreCase))
                );
                if (destfolder is null)
                    throw new ArgumentException($"Bad destination: {visitor.Destination}");
            }
            return destfolder;
        }

        private static void SetContext(string script)
        {
            if (script == _script)
                if (_contextException is not null)
                    ExceptionDispatchInfo.Capture(_contextException).Throw();
                else if (_context is not null)
                    return;
            _script = script;
            _contextException = null;
            _context = null;
            AntlrInputStream inputStream = new(new StringReader(script));
            CaseChangingCharStream lowerstream = new(inputStream, false);
            ScriptRenamerLexer lexer = new(lowerstream);
            lexer.AddErrorListener(ExceptionErrorListener.Instance);
            CommonTokenStream tokenStream = new(lexer);
            ScriptRenamerParser parser = new(tokenStream);
            parser.ErrorHandler = new BailErrorStrategy();
            parser.AddErrorListener(ExceptionErrorListener.Instance);
            try
            {
                _context = parser.start();
            }
            catch (Exception e)
            {
                _contextException = e;
                ExceptionDispatchInfo.Capture(e).Throw();
            }
        }

        private static void SetupAndLaunch(ScriptRenamerVisitor visitor)
        {
            SetContext(visitor.Script.Script);
            try
            {
                _ = visitor.Visit(_context);
            }
            catch (SkipException)
            {
                visitor.Filename = null;
                visitor.Destination = null;
                visitor.Subfolder = null;
            }
        }

        private static bool CheckBadArgs(ScriptRenamerVisitor visitor)
        {
            if (string.IsNullOrWhiteSpace(visitor.Script?.Script))
                throw new ArgumentException("Script is empty or null");
            if (visitor.Script.Type != RenamerId)
                throw new ArgumentException($"Script doesn't match {RenamerId}");
            return visitor.AnimeInfo is null || visitor.EpisodeInfo is null;
        }

        public static string NormPath(string path)
        {
            return path?.Replace('\\', '/').TrimEnd('/');
        }

        private static string RemoveInvalidFilenameChars(string filename)
        {
            filename = filename.RemoveInvalidPathCharacters();
            filename = string.Concat(filename.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
            return filename;
        }
    }

    public class SkipException : Exception
    {
    }
}
