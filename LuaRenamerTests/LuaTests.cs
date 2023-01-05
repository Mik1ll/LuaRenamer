using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LuaRenamer;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;

// ReSharper disable PossibleUnintendedReferenceComparison
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace LuaRenamerTests
{
    [TestClass]
    public class LuaTests
    {
        private static MoveEventArgs Args()
        {
            return new MoveEventArgs
            {
                AnimeInfo = new List<IAnime>
                {
                    Mock.Of<IAnime>(a =>
                        a.AnimeID == 532 &&
                        a.PreferredTitle == "prefTitle" &&
                        a.Restricted &&
                        a.Type == AnimeType.Movie &&
                        a.EpisodeCounts == new EpisodeCounts { Episodes = 20 } &&
                        a.AirDate == new DateTime(2001, 1, 20) &&
                        a.Titles == new List<AnimeTitle>
                        {
                            new()
                            {
                                Title = "animeTitle1",
                                Language = TitleLanguage.English,
                                Type = TitleType.Short
                            },
                            new()
                            {
                                Title = "animeTitle2",
                                Language = TitleLanguage.Japanese,
                                Type = TitleType.Official
                            },
                            new()
                            {
                                Title = "animeTitle3",
                                Language = TitleLanguage.Romaji,
                                Type = TitleType.Synonym
                            },
                            new()
                            {
                                Title = "animeTitle4",
                                Language = TitleLanguage.English,
                                Type = TitleType.Main
                            }
                        } &&
                        a.Relations == new List<IRelatedAnime>
                        {
                            Mock.Of<IRelatedAnime>(ira =>
                                ira.RelatedAnime == Mock.Of<IAnime>(ra => 
                                    ra.AnimeID == 643 &&
                                    ra.PreferredTitle == "prefSequelTitle" &&
                                    ra.Restricted &&
                                    a.Type == AnimeType.Movie &&
                                    a.EpisodeCounts == new EpisodeCounts { Episodes = 4 } &&
                                    a.AirDate == new DateTime(2002, 2, 24) &&
                                    a.Titles == new List<AnimeTitle>
                                    {
                                        new()
                                        {
                                            Title = "sequelTitle1",
                                            Language = TitleLanguage.English,
                                            Type = TitleType.Short
                                        },
                                        new()
                                        {
                                            Title = "sequelTitle2",
                                            Language = TitleLanguage.Japanese,
                                            Type = TitleType.Official
                                        },
                                        new()
                                        {
                                            Title = "sequelTitle3",
                                            Language = TitleLanguage.Romaji,
                                            Type = TitleType.Synonym
                                        },
                                        new()
                                        {
                                            Title = "sequelTitle4",
                                            Language = TitleLanguage.English,
                                            Type = TitleType.Main
                                        }
                                    } &&
                                    a.Relations == new List<IRelatedAnime>{}
                                ) &&
                                ira.RelationType == RelationType.Sequel
                            )
                        }
                    )
                },
                FileInfo = Mock.Of<IVideoFile>(file =>
                    file.Hashes == Mock.Of<IHashes>(hashes =>
                        hashes.CRC == "abc123") &&
                    file.MediaInfo == Mock.Of<IMediaContainer>(x =>
                        x.Video == Mock.Of<IVideoStream>(vs =>
                            vs.StandardizedResolution == "1080p" &&
                            vs.BitDepth == 8 &&
                            vs.SimplifiedCodec == "x.264") &&
                        x.Subs == new List<ITextStream>() &&
                        x.Audio == new List<IAudioStream>() &&
                        x.General == Mock.Of<IGeneralStream>(gs =>
                            gs.Duration == 20.3 &&
                            gs.FrameRate == 30.21m &&
                            gs.OverallBitRate == 23423)) &&
                    file.AniDBFileInfo == Mock.Of<IAniDBFile>(af =>
                        af.ReleaseGroup == Mock.Of<IReleaseGroup>(rg =>
                            rg.Name == "testGroup" &&
                            rg.ShortName == "TG") &&
                        af.ReleaseDate == new DateTime(1997, 12, 2) &&
                        af.Censored &&
                        af.Source == "DVD" &&
                        af.Version == 2 &&
                        af.MediaInfo == Mock.Of<AniDBMediaData>(md =>
                            md.AudioLanguages == new List<TitleLanguage> { TitleLanguage.English, TitleLanguage.Japanese } &&
                            md.SubLanguages == new List<TitleLanguage>())) &&
                    file.FilePath == @"C:\Users\Mike\Desktop\Anime\testfile.mp4" &&
                    file.Filename == "testfilename"
                ),
                EpisodeInfo = new List<IEpisode>
                {
                    Mock.Of<IEpisode>(e =>
                        e.Number == 5 &&
                        e.Type == EpisodeType.Episode &&
                        e.AirDate == new DateTime(2001, 3, 7) &&
                        e.AnimeID == 532 &&
                        e.Titles == new List<AnimeTitle>
                        {
                            new()
                            {
                                Title = "episodeTitle1",
                                Language = TitleLanguage.English,
                                Type = TitleType.Short
                            },
                            new()
                            {
                                Title = "episdoeTitle2",
                                Language = TitleLanguage.English,
                                Type = TitleType.Synonym
                            },
                            new()
                            {
                                Title = "episodeTitle3",
                                Language = TitleLanguage.Romaji,
                                Type = TitleType.Short
                            }
                        }
                    )
                },
                AvailableFolders = new List<IImportFolder>
                {
                    Mock.Of<IImportFolder>(x =>
                        x.DropFolderType == DropFolderType.Source && x.Location == @"C:\Users\Mike\Desktop\Anime Drop" && x.Name == "Drop"),
                    Mock.Of<IImportFolder>(x =>
                        x.DropFolderType == DropFolderType.Destination && x.Location == @"C:\Users\Mike\Desktop\Movies" && x.Name == "Movies"),
                    Mock.Of<IImportFolder>(x => x.DropFolderType == DropFolderType.Both && x.Location == @"C:\Users\Mike\Desktop\Anime" && x.Name == "Anime"),
                    Mock.Of<IImportFolder>(x =>
                        x.DropFolderType == DropFolderType.Destination && x.Location == @"C:\Users\Mike\Desktop\h-anime" && x.Name == "h-anime")
                },
                GroupInfo = new List<IGroup>
                {
                    Mock.Of<IGroup>(x => x.Name == "groupname" && x.MainSeries == Mock.Of<IAnime>(a => a.AnimeID == 3523) && x.Series == new List<IAnime>())
                }
            };
        }

        private static readonly ILogger<LuaRenamer.LuaRenamer> Logmock = Mock.Of<ILogger<LuaRenamer.LuaRenamer>>();

        [TestMethod]
        public void TestScriptRuns()
        {
            var args = Args();
            args.Script = new RenameScriptImpl
            {
                Script = $@"{LuaEnv.filename} = ""testfilename""
{LuaEnv.destination} = ""Anime""
{LuaEnv.subfolder} = {{""test123""}}
",
                Type = nameof(LuaRenamer.LuaRenamer)
            };
            var renamer = new LuaRenamer.LuaRenamer(Logmock)
            {
                Args = args
            };
            var res = renamer.GetInfo();
            Assert.AreEqual("testfilename", res?.filename);
        }

        [TestMethod]
        public void TestAnime()
        {
            var args = Args();
            args.Script = new RenameScriptImpl
            {
                Script = $@"{LuaEnv.filename} = tostring({LuaEnv.anime.typeFn} == {LuaEnv.AnimeType}.{nameof(AnimeType.Movie)})",
                Type = nameof(LuaRenamer.LuaRenamer),
                ExtraData = null
            };
            var renamer = new LuaRenamer.LuaRenamer(Logmock)
            {
                Args = args
            };
            var res = renamer.GetInfo();
            Assert.AreEqual("true", res?.filename);
        }

        [TestMethod]
        public void TestFile()
        {
            var args = Args();
            args.Script = new RenameScriptImpl
            {
                Script = $@"{LuaEnv.filename} = os.date(""%c"", os.time({LuaEnv.file.anidb.releasedateFn}))",
                Type = nameof(LuaRenamer.LuaRenamer),
                ExtraData = null
            };
            var renamer = new LuaRenamer.LuaRenamer(Logmock)
            {
                Args = args
            };
            renamer.GetInfo();
        }

        [TestMethod]
        public void TestEpisodes()
        {
            var args = Args();
            args.Script = new RenameScriptImpl
            {
                Script =
                    $@"{LuaEnv.filename} = {LuaEnv.episode.titlesFn}[1].{LuaEnv.title.name} .. "" "" .. {LuaEnv.episode.numberFn} .. "" "" .. {LuaEnv.episode.typeFn}",
                Type = nameof(LuaRenamer.LuaRenamer),
                ExtraData = null
            };
            var renamer = new LuaRenamer.LuaRenamer(Logmock)
            {
                Args = args
            };
            var res = renamer.GetInfo();
            Assert.IsNotNull(res);
            Assert.AreEqual("episodeTitle1 5 Episode", res.Value.filename);
        }

        [TestMethod]
        public void TestImportFolder()
        {
            var args = Args();
            args.Script = new RenameScriptImpl
            {
                Script = $@"if ({LuaEnv.importfolders}[2].{LuaEnv.importfolder.type} == {LuaEnv.ImportFolderType}.{nameof(DropFolderType.Destination)}) then
  {LuaEnv.destination} = {LuaEnv.importfolders}[2]
end",
                Type = nameof(LuaRenamer.LuaRenamer),
                ExtraData = null
            };
            var renamer = new LuaRenamer.LuaRenamer(Logmock)
            {
                Args = args
            };
            var res = renamer.GetInfo();
            Assert.AreSame(args.AvailableFolders[1], res?.destination);
        }

        [TestMethod]
        public void TestLuaLinq()
        {
            var args = Args();
            args.Script = new RenameScriptImpl
            {
                Script = $@"{LuaEnv.filename} = from({LuaEnv.anime.titlesFn}):map(function(x, r) return r .. x.{LuaEnv.title.name}; end, """")",
                Type = nameof(LuaRenamer.LuaRenamer),
                ExtraData = null
            };
            var renamer = new LuaRenamer.LuaRenamer(Logmock)
            {
                Args = args
            };
            var res = renamer.GetInfo();
            Assert.AreEqual("animeTitle1animeTitle2animeTitle3animeTitle4", res?.filename);
        }

        [TestMethod]
        public void TestEpisodeNumbers()
        {
            var args = Args();
            args.Script = new RenameScriptImpl
            {
                Script = $@"{LuaEnv.filename} = {LuaEnv.episode_numbers}(3)",
                Type = nameof(LuaRenamer.LuaRenamer),
                ExtraData = null
            };
            var renamer = new LuaRenamer.LuaRenamer(Logmock)
            {
                Args = args
            };
            var res = renamer.GetInfo();
            Assert.AreEqual("005", res?.filename);
        }

        [TestMethod]
        public void TestGetTitle()
        {
            var args = Args();
            args.Script = new RenameScriptImpl()
            {
                Script =
                    $@"{LuaEnv.filename} = {LuaEnv.anime.getnameFn}({LuaEnv.Language}.{nameof(TitleLanguage.English)}) .. {LuaEnv.episode.getnameFn}({LuaEnv.Language}.{nameof(TitleLanguage.English)}, true)",
                Type = nameof(LuaRenamer.LuaRenamer),
                ExtraData = null
            };
            var renamer = new LuaRenamer.LuaRenamer(Logmock)
            {
                Args = args
            };
            var res = renamer.GetInfo();
            Assert.AreEqual("animeTitle4episdoeTitle2", res?.filename);
        }

        [TestMethod]
        public void TestLuaEnvNames()
        {
            Assert.AreEqual("file.media.sublanguages", LuaEnv.file.media.sublanguagesFn);
        }

        [TestMethod]
        public void TestLogging()
        {
            var args = Args();
            args.Script = new RenameScriptImpl()
            {
                Script =
                    $"log(\"test\")",
                Type = nameof(LuaRenamer.LuaRenamer),
                ExtraData = null
            };
            var renamer = new LuaRenamer.LuaRenamer(Logmock)
            {
                Args = args
            };
            renamer.GetInfo();
        }

        [TestMethod]
        public void TestStringMethod()
        {
            var args = Args();
            args.Script = new RenameScriptImpl()
            {
                Script =
                    @"function string:clean_spaces(char) return (self:match(""^%s*(.-)%s*$""):gsub(""%s+"", char or "" "")) end
                filename = ((""blah  sdhow  wh ""):clean_spaces())",
                Type = nameof(LuaRenamer.LuaRenamer),
                ExtraData = null
            };
            var renamer = new LuaRenamer.LuaRenamer(Logmock)
            {
                Args = args
            };
            var res = renamer.GetInfo();
            Assert.AreEqual("blah sdhow wh", res?.filename);
        }

        [TestMethod]
        public void TestLogAbstractionVersion()
        {
            Assert.AreEqual("2.1.0.0", Assembly.GetAssembly(typeof(ILogger))?.GetName().Version?.ToString());
        }


        [TestMethod]
        public void TestBenchmark()
        {
            foreach (var i in Enumerable.Range(0, 1000))
            {
                TestStringMethod();
            }
        }
    }
}
