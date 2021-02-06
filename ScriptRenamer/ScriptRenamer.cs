using System;
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
        public const string RENAMER_ID = nameof(ScriptRenamer);
        private static string _script = string.Empty;
        private static ParserRuleContext _context;

        public (IImportFolder destination, string subfolder) GetDestination(MoveEventArgs args)
        {
            var visitor = new ScriptRenamerVisitor(args);
            SetupAndLaunch(visitor);
            return (args.AvailableFolders.SingleOrDefault(f => f.Name == visitor.Destination && f.DropFolderType != DropFolderType.Source),
                    !string.IsNullOrWhiteSpace(visitor.Subfolder) ? visitor.Subfolder.ReplaceInvalidPathCharacters() : null);
        }

        public string GetFilename(RenameEventArgs args)
        {
            var visitor = new ScriptRenamerVisitor(args);
            SetupAndLaunch(visitor);
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
            lexer.AddErrorListener(ExceptionErrorListener.Instance);
            CommonTokenStream tokenStream = new(lexer);
            ScriptRenamerParser parser = new(tokenStream);
            parser.ErrorHandler = new BailErrorStrategy();
            parser.AddErrorListener(ExceptionErrorListener.Instance);
            _context = parser.start();
            return _context;
        }

        private static void SetupAndLaunch(ScriptRenamerVisitor visitor)
        {
            try
            {
                CheckBadArgs(visitor);
                var context = GetContext(visitor.Script.Script);
                _ = visitor.Visit(context);
            }
            catch (SkipException)
            {
                visitor.Filename = null;
                visitor.Destination = null;
                visitor.Subfolder = null;
            }
            catch (Exception)
            {
                _context = null;
                throw;
            }
        }

        private static void CheckBadArgs(ScriptRenamerVisitor visitor)
        {
            if (string.IsNullOrWhiteSpace(visitor.Script?.Script))
                throw new ArgumentException("Script is empty or null");
            if (visitor.Script.Type != RENAMER_ID)
                throw new ArgumentException($"Script doesn't match {RENAMER_ID}");
            if (visitor.AnimeInfo is null || visitor.EpisodeInfo is null)
                throw new ArgumentException("No anime info or episode info, cannot rename unrecognized file");
            if (visitor.FileInfo.MediaInfo is null)
                throw new ArgumentException("No media info, cannot handle file.");
        }
    }

    public class SkipException : Exception
    {
    }
}
