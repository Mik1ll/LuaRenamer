using Antlr4.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptRenamer;

namespace ScriptRenamerTests
{
    [TestClass]
    public class ParserTest
    {
        private ScriptRenamerParser Setup(string text)
        {
            AntlrInputStream inputStream = new(text);
            ScriptRenamerLexer lexer = new(inputStream);
            CommonTokenStream tokenStream = new(lexer);
            ScriptRenamerParser parser = new(tokenStream);
            return parser;
        }

        [TestMethod]
        public void TestIfStatement()
        {
            var parser = Setup("if true or false {        }");
            var context = parser.if_stmt();
            var visitor = new ScriptRenamerVisitor();
            visitor.Visit(context);
            var andnull = context.bool_expr().and;
            var orexists = context.bool_expr().or;
        }
    }
}
