using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            throw new NotImplementedException();
        }

        public string GetFilename(RenameEventArgs args)
        {
            AntlrInputStream inputStream = new(new StreamReader(args.Script.Script, Encoding.UTF8));
            ScriptRenamerLexer lexer = new(inputStream);
            CommonTokenStream tokenStream = new(lexer);
            ScriptRenamerParser parser = new(tokenStream);
            ScriptRenamerParser.StartContext context = parser.start();
            ScriptRenamerVisitor visitor = new();
            visitor.Visit(context);
            return string.Empty;
        }
    }
}
