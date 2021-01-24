using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
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


        //[TestMethod]
        //public void BigTest()
        //{
        //    var parser = Setup(
        //        @"if (GroupShort)
        //              add '[' GroupShort '] '
        //          else if (GroupLong) 
        //              add '[' GroupLong '] '
        //          if (AnimeTitleEnglish) 
        //              add AnimeTitleEnglish ' '
        //          else 
        //              add AnimeTitle ' '
        //          if (EpisodeType is Episode and len(EpisodeCount) >= 2 and EpisodeNumber <= 9) 
        //              add '0'
        //          add EpisodePrefix EpisodeNumber ' '
        //          if (EpisodeTitleEnglish)
        //              add EpisodeTitleEnglish ' '
        //          else
        //              add first(EpisodeTitles has Main) ' '
        //          add Resolution ' ' VideoCodecShort ' '
        //          if (BitDepth)
        //              add BitDepth 'bit '
        //          add Source ' '
        //          if (DubLanguages has English)
        //              if (DubLanguages has Japanese)
        //                  add '[DUAL-AUDIO] '
        //              else
        //                  add '[DUB] '
        //          if (DubLanguages has Japanese and not SubLanguages has English)
        //              add '[raw] '
        //          if (Restricted)
        //              if (Censored)
        //                  add '[CEN] '
        //              else
        //                  add '[UNC] '
        //          add CRCUpper

        //          // Import folders:
        //          if (Restricted and ImportFolders has 'h-anime')
        //              destination set 'h-anime'
        //          else if (AnimeType is Movie)
        //              destination set 'Movies'
        //          else
        //              destination set 'Anime'
        //          if (AnimeTitles has English)
        //              if (AnimeTitles has English has Main)
        //                  subfolder set first(AnimeTitles has English has Main)
        //              else if (AnimeTitles has English has Official)
        //                  subfolder set first(AnimeTitles has English has Official)
        //              else
        //                  subfolder set first(AnimeTitles has English)
        //          else
        //              subfolder set first(AnimeTitles has Main)
        //         ");
        //    var context = parser.start();

        //    // TODO: moq everything....

        //    var visitor = new ScriptRenamerVisitor();
        //    _ = visitor.Visit(context);

        //}

        [TestMethod]
        public void TestDanglingElse()
        {
            var parser = Setup(
           @"if (true) if (false) {} else {}");
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
                AnimeInfo = Mock.Of<IAnime>(a => a.Type == AnimeType.Movie)
            };
            var result = visitor.Visit(context);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void TestNumberAtomCompare()
        {
            var parser = Setup("if (22 < EpisodeCount) filename add 'testing' ");
            var context = parser.start();
            var visitor = new ScriptRenamerVisitor
            {
                AnimeInfo = Mock.Of<IAnime>(x =>
                    x.EpisodeCounts == new EpisodeCounts
                    {
                        Episodes = 25
                    })
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
                AnimeInfo = Mock.Of<IAnime>(a => a.PreferredTitle == "testing")
            };
            var result = (bool)visitor.Visit(context);
            Assert.IsTrue(result);
        }

        [DataTestMethod]
        //[DataRow("if (DubLanguages has Japanese and not SubLanguages has English) add '[raw] '")]
        //[DataRow("if (AnimeTitles has English has Main and len(AnimeTitles has English has Main) == 2) filename add AnimeTitles has English has Main")]
        //[DataRow("if (len(ImportFolders) == 0) add ' empty import folder'")]
        //[DataRow("if (AudioCodecs has 'mp3') add ' has ' AudioCodecs has 'mp3'")]
        //[DataRow("if (first(AnimeTitles)) add ' ' first(AnimeTitles)")]
        public void TestHasOperator()
        {
            var input = "if (DubLanguages has Japanese and not SubLanguages has English) add '[raw] '"
                        + "if (AnimeTitles has English has Main and len(AnimeTitles has Main has English) == 2) filename add AnimeTitles has English has Main"
                        + "if (len(ImportFolders) == 0) add ' empty import folder'"
                        + "if (AudioCodecs has 'mp3') add ' has ' AudioCodecs has 'mp3'"
                        + "if (first(AnimeTitles)) add ' ' first(AnimeTitles)"
                        + "if (EpisodeTitles has English has Main) add ' ' EpisodeTitles has Main has English";
            var parser = Setup(input);
            var context = parser.start();
            var visitor = new ScriptRenamerVisitor
            {
                FileInfo = Mock.Of<IVideoFile>(v => v.AniDBFileInfo == Mock.Of<IAniDBFile>(m => m.MediaInfo == Mock.Of<AniDBMediaData>(md => md.AudioCodecs == new List<string> { "mp3", "FLAC", "opus" } && md.AudioLanguages == new List<TitleLanguage> { TitleLanguage.Afrikaans, TitleLanguage.Japanese } && md.SubLanguages == new List<TitleLanguage> { TitleLanguage.Hebrew, TitleLanguage.Galician }
                ))),
                AnimeInfo = Mock.Of<IAnime>(a => a.Titles == new List<AnimeTitle>
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
                        Language = TitleLanguage.Japanese,
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
                }),
                EpisodeInfo = Mock.Of<IEpisode>(e => e.Titles == new List<AnimeTitle>
                {
                    new AnimeTitle
                    {
                        Title = "etest",
                        Language = TitleLanguage.English,
                        Type = TitleType.Main
                    },
                    new AnimeTitle
                    {
                        Title = "etest2",
                        Language = TitleLanguage.Japanese,
                        Type = TitleType.Official
                    },
                    new AnimeTitle
                    {
                        Title = "etest3",
                        Language = TitleLanguage.Romaji,
                        Type = TitleType.Main
                    },
                    new AnimeTitle
                    {
                        Title = "etest4",
                        Language = TitleLanguage.English,
                        Type = TitleType.Main
                    }
                })
            };
            _ = visitor.Visit(context);
            Assert.IsTrue(visitor.Filename == "[raw] test, test4 empty import folder has mp3 test etest, etest4");
        }

        [TestMethod]
        public void TestSetStmt()
        {
            var parser = Setup("filename set 'test' 'testing' 'testing' AnimeTitlePreferred");
            var context = parser.start();
            var visitor = new ScriptRenamerVisitor
            {
                AnimeInfo = Mock.Of<IAnime>(a => a.PreferredTitle == "wioewoihwoiehwoihweohwiowj")
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
            try
            {
                _ = visitor.Visit(context);
                Assert.Fail();
            }
            catch
            {
            }
        }
    }
}
