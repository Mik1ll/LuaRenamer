using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptRenamer;
using Shoko.Plugin.Abstractions.DataModels;

namespace ScriptRenamerTests
{
    [TestClass]
    public class ParserTest
    {
        private static ScriptRenamerParser Setup(string text)
        {
            AntlrInputStream inputStream = new(text);
            ScriptRenamerLexer lexer = new(inputStream);
            CommonTokenStream tokenStream = new(lexer);
            ScriptRenamerParser parser = new(tokenStream);
            return parser;
        }

        [TestMethod]
        public void TestDanglingElse()
        {
            var parser = Setup(
           @"if (true)
                    if (false) {
                    } else {
                    }");
            var context = parser.start();
            var visitor = new ScriptRenamerVisitor();
            _ = visitor.Visit(context);
            Assert.IsTrue(context.stmt(0).if_stmt().ELSE() is null && context.stmt(0).if_stmt().true_branch.if_stmt().ELSE() is not null);
        }

        [TestMethod]
        public void TestAnimeTypeIs()
        {
            var parser = Setup("if (AnimeType is Movie) {        }");
            var context = parser.if_stmt();
            var visitor = new ScriptRenamerVisitor
            {
                AnimeInfo = new MockAnimeInfo
                {
                    Type = AnimeType.Movie
                }
            };
            var result = visitor.Visit(context);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void TestNumberAtomCompare()
        {
            var parser = Setup("if (22 < EpisodeCount) filename add 'testing' ");
            var context = parser.if_stmt();
            var visitor = new ScriptRenamerVisitor
            {
                AnimeInfo = new MockAnimeInfo
                {
                    EpisodeCounts = new EpisodeCounts
                    {
                        Episodes = 25
                    }
                }
            };
            var result = visitor.Visit(context);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void TestStringAtomCompare()
        {
            var parser = Setup("if ('testing' == AnimeTitlePreferred) {}");
            var context = parser.if_stmt().bool_expr();
            var visitor = new ScriptRenamerVisitor
            {
                AnimeInfo = new MockAnimeInfo
                {
                    PreferredTitle = "testing"
                }
            };
            var result = (bool)visitor.Visit(context);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void TestHasOperator()
        {
            var parser = Setup("if (AnimeTitles has English has Main and len(AnimeTitles has English has Main) == 2) filename add AnimeTitles has English has Main"
                             + "if (len(ImportFolders) == 0) add ' empty import folder'"
                             + "if (");
            var context = parser.start();
            var visitor = new ScriptRenamerVisitor
            {
                FileInfo = new MockVideoFile(),
                AnimeInfo = new MockAnimeInfo
                {
                    Titles = new List<AnimeTitle>
                {
                    new AnimeTitle
                    {
                        Title = "test",
                        Language = TitleLanguage.English,
                        Type = TitleType.Main
                    },
                    new AnimeTitle
                    {
                        Title = "test2",
                        Language = TitleLanguage.English,
                        Type = TitleType.Official
                    },
                    new AnimeTitle
                    {
                        Title = "test3",
                        Language = TitleLanguage.Romaji,
                        Type = TitleType.Main
                    },
                    new AnimeTitle
                    {
                        Title = "test4",
                        Language = TitleLanguage.English,
                        Type = TitleType.Main
                    }
                }
                }
            };
            _ = visitor.Visit(context);
            Assert.IsTrue(visitor.Filename == "test, test4 empty import folder");
        }

        [TestMethod]
        public void TestSetStmt()
        {
            var parser = Setup("filename set 'test' 'testing' 'testing' AnimeTitlePreferred");
            var context = parser.start();
            var visitor = new ScriptRenamerVisitor
            {
                AnimeInfo = new MockAnimeInfo
                {
                    PreferredTitle = "wioewoihwoiehwoihweohwiowj"
                }
            };
            _ = visitor.Visit(context);
            Assert.IsTrue(visitor.Filename == "testtestingtestingwioewoihwoiehwoihweohwiowj");
        }

        [TestMethod]
        public void TestDynamicEquality()
        {
            var parser = Setup("265252 != 234232 and 'abc' == 'abc' and 1231 == 1231 and 'abd' != 'abdd'");
            var context = parser.bool_expr();
            Assert.IsTrue((bool)new ScriptRenamerVisitor().Visit(context));
        }


        [TestMethod]
        public void TestLexerError()
        {
            var parser = Setup("if (true or false and true and -++107.2342 == 3) filename add ' ' ");
            var context = parser.start();
            var visitor = new ScriptRenamerVisitor();
            _ = visitor.Visit(context);
        }
    }
}
