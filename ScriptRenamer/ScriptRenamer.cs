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
            if (BadArgs(args.Script, visitor))
            {
                return (null, null);
            }
            var context = GetContext(args.Script.Script);
            try
            {
                _ = visitor.Visit(context);
            }
            catch (CancelStmtException)
            {
                args.Cancel = true;
                return (null, null);
            }
            return (args.AvailableFolders.SingleOrDefault(f => f.Name == visitor.Destination && f.DropFolderType != DropFolderType.Source),
                    !string.IsNullOrWhiteSpace(visitor.Subfolder) ? visitor.Subfolder.ReplaceInvalidPathCharacters() : null);
        }

        public string GetFilename(RenameEventArgs args)
        {
            var visitor = new ScriptRenamerVisitor(args);
            if (BadArgs(args.Script, visitor))
            {
                return null;
            }
            var context = GetContext(args.Script.Script);
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

        private static bool BadArgs(IRenameScript script, ScriptRenamerVisitor visitor)
        {
            return string.IsNullOrWhiteSpace(script?.Script)
                            || script.Type != RENAMER_ID
                            || visitor.AnimeInfo is null
                            || visitor.EpisodeInfo is null
                            || visitor.FileInfo is null
                            || visitor.FileInfo.MediaInfo is null;
        }
    }

    internal class CancelStmtException : System.Exception
    {
    }
}
