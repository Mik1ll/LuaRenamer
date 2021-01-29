using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antlr4.Runtime;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.DataModels;

namespace ScriptRenamer
{
    [Renamer(RENAMER_ID)]
    public class ScriptRenamer : IRenamer
    {
        private const string RENAMER_ID = nameof(ScriptRenamer);
        private static string _script = string.Empty;
        private static ParserRuleContext _context;

        public (IImportFolder destination, string subfolder) GetDestination(MoveEventArgs args)
        {
            if (BadArgs(args))
            {
                args.Cancel = true;
                return (null, null);
            }
            var context = GetContext(args.Script.Script);
            var visitor = new ScriptRenamerVisitor
            {
                Renaming = false,
                AvailableFolders = args.AvailableFolders,
                AnimeInfo = args.AnimeInfo.FirstOrDefault(),
                EpisodeInfo = args.EpisodeInfo.FirstOrDefault(),
                FileInfo = args.FileInfo,
                GroupInfo = args.GroupInfo.FirstOrDefault()
            };
            try
            {
                _ = visitor.Visit(context);
            }
            catch (CancelStmtException)
            {
                args.Cancel = true;
                return (null, null);
            }
            return (args.AvailableFolders.FirstOrDefault(f => f.Name == visitor.Destination),
                    !string.IsNullOrWhiteSpace(visitor.Subfolder) ? visitor.Subfolder.ReplaceInvalidPathCharacters() : null);
        }

        public string GetFilename(RenameEventArgs args)
        {
            if (BadArgs(args))
            {
                args.Cancel = true;
                return null;
            }
            var context = GetContext(args.Script.Script);
            var visitor = new ScriptRenamerVisitor
            {
                Renaming = true,
                AvailableFolders = new List<IImportFolder>(),
                AnimeInfo = args.AnimeInfo.FirstOrDefault(),
                EpisodeInfo = args.EpisodeInfo.FirstOrDefault(),
                FileInfo = args.FileInfo,
                GroupInfo = args.GroupInfo.FirstOrDefault()
            };
            try
            {
                _ = visitor.Visit(context);
            }
            catch (CancelStmtException)
            {
                args.Cancel = true;
                return null;
            }
            return !string.IsNullOrWhiteSpace(visitor.Filename) ? visitor.Filename.ReplaceInvalidPathCharacters() + Path.GetExtension(args.FileInfo.Filename) : null;
        }

        private static ParserRuleContext GetContext(string script)
        {
            if (script == _script && _context is not null)
                return _context;
            else
                _script = script;
            AntlrInputStream inputStream = new(new StringReader(script));
            ScriptRenamerLexer lexer = new(inputStream);
            CommonTokenStream tokenStream = new(lexer);
            ScriptRenamerParser parser = new(tokenStream);
            _context = parser.start();
            return _context;
        }

        private static bool BadArgs(dynamic args)
        {
            return string.IsNullOrWhiteSpace(args.Script?.Script) || args.Script.Type != RENAMER_ID || args.AnimeInfo is null || args.EpisodeInfo is null || args.FileInfo is null || ((IVideoFile)args.FileInfo)?.MediaInfo is null;
        }
    }

    internal class CancelStmtException : System.Exception
    {
    }
}
