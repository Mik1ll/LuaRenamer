using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptRenamer;
using Shoko.Plugin.Abstractions.DataModels;

namespace ScriptRenamerTests
{
    public class MockAnimeInfo : IAnime
    {
        public int AnimeID { get; set; }

        public EpisodeCounts EpisodeCounts { get; set; }

        public DateTime? AirDate { get; set; }

        public DateTime? EndDate { get; set; }

        public AnimeType Type { get; set; }

        public IReadOnlyList<AnimeTitle> Titles { get; set; }

        public double Rating { get; set; }

        public bool Restricted { get; set; }

        public string PreferredTitle { get; set; }
    }


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
        public void TestDanglingElse()
        {
            var parser = Setup("if (true) if (false) {} else {}");
            var context = parser.if_stmt();
            var visitor = new ScriptRenamerVisitor();
            visitor.Visit(context);
            Assert.IsFalse((context.false_branch?.GetText().Length ?? 0) > 0);
        }

        [TestMethod]
        public void TestAnimeTypeIs()
        {
            var parser = Setup("if (AnimeType is Movie) {        }");
            var context = parser.if_stmt();
            var visitor = new ScriptRenamerVisitor();
            visitor.AnimeInfo = new MockAnimeInfo
            {
                Type = AnimeType.Movie
            };
            var result = visitor.Visit(context);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void TestNumberAtomCompare()
        {
            var parser = Setup("if (22.22412 < EpisodeCount) filename add 'testing' ");
            var context = parser.if_stmt();
            var visitor = new ScriptRenamerVisitor();
            visitor.AnimeInfo = new MockAnimeInfo
            {
                EpisodeCounts = new EpisodeCounts
                {
                    Episodes = 25
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
            var visitor = new ScriptRenamerVisitor();
            visitor.AnimeInfo = new MockAnimeInfo
            {
                PreferredTitle = "testing"
            };
            var result = (bool)visitor.Visit(context);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void TestHasOperator()
        {
            var parser = Setup("if (AnimeTitles has English has Main) filename add 'test'");
            var context = parser.start();
            var visitor = new ScriptRenamerVisitor();
            visitor.AnimeInfo = new MockAnimeInfo
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
            };
            var result = visitor.Visit(context);
        }

        [TestMethod]
        public void TestSetStmt()
        {
            var parser = Setup("filename set 'test' 'testing' 'testing' AnimeTitlePreferred");
            var context = parser.start();
            var visitor = new ScriptRenamerVisitor();
            visitor.AnimeInfo = new MockAnimeInfo
            {
                PreferredTitle = "wioewoihwoiehwoihweohwiowj"
            };
            visitor.Visit(context);
        }

    }
}
