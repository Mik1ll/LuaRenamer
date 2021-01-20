using System.IO;
using System.Linq;
using System.Text;
using Antlr4.Runtime;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.DataModels;

namespace ScriptRenamer
{
    [Renamer("ScriptRenamer")]
    class ScriptRenamer : IRenamer
    {
        public (IImportFolder destination, string subfolder) GetDestination(MoveEventArgs args)
        {
            AntlrInputStream inputStream = new(new StreamReader(args.Script.Script, Encoding.UTF8));
            var context = SetupContext(inputStream);
            var visitor = new ScriptRenamerVisitor
            {
                AvailableFolders = args.AvailableFolders,
                AnimeInfo = args.AnimeInfo.FirstOrDefault(),
                EpisodeInfo = args.EpisodeInfo.FirstOrDefault(),
                FileInfo = args.FileInfo,
                GroupInfo = args.GroupInfo.FirstOrDefault(),
                Script = args.Script
            };
            _ = visitor.Visit(context);
            return (args.AvailableFolders.FirstOrDefault(f => f.Name == visitor.Destination), visitor.Subfolder);
        }

        public string GetFilename(RenameEventArgs args)
        {
            AntlrInputStream inputStream = new(new StreamReader(args.Script.Script, Encoding.UTF8));
            var context = SetupContext(inputStream);
            var visitor = new ScriptRenamerVisitor
            {
                AnimeInfo = args.AnimeInfo.FirstOrDefault(),
                EpisodeInfo = args.EpisodeInfo.FirstOrDefault(),
                FileInfo = args.FileInfo,
                GroupInfo = args.GroupInfo.FirstOrDefault(),
                Script = args.Script
            };
            _ = visitor.Visit(context);
            return visitor.Filename;
        }

        private static ParserRuleContext SetupContext(AntlrInputStream inputStream)
        {
            ScriptRenamerLexer lexer = new(inputStream);
            CommonTokenStream tokenStream = new(lexer);
            ScriptRenamerParser parser = new(tokenStream);
            return parser.start();
        }
    }
}
