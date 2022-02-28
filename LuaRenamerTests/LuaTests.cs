using System;
using System.Collections.Generic;
using System.Diagnostics;
using LuaRenamer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NLua.Exceptions;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;

namespace LuaRenamerTests
{
    [TestClass]
    public class LuaTests
    {
        private MoveEventArgs Args()
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
                            md.AudioCodecs == new List<string> { "mp3", "FLAC", "opus" } &&
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


        [TestMethod]
        public void TestScriptRuns()
        {
            var args = Args();
            args.Script = new RenameScriptImpl
            {
                Script = @"filename = ""testfilename""
destination = ""Anime""
subfolder = {""test123""}
",
                Type = nameof(LuaRenamer.LuaRenamer)
            };
            var renamer = new LuaRenamer.LuaRenamer
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
                Script = @"filename = tostring(anime.type == AnimeType.Movie)",
                Type = nameof(LuaRenamer.LuaRenamer),
                ExtraData = null
            };
            var renamer = new LuaRenamer.LuaRenamer
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
                Script = @"filename = os.date(""%c"", os.time(file.anidb.releasedate))",
                Type = nameof(LuaRenamer.LuaRenamer),
                ExtraData = null
            };
            var renamer = new LuaRenamer.LuaRenamer
            {
                Args = args
            };
            var res = renamer.GetInfo();
        }

        [TestMethod]
        public void TestEpisodes()
        {
            var args = Args();
            args.Script = new RenameScriptImpl
            {
                Script = @"local episode = episodes[1]
filename = episode.titles[1].name .. "" "" .. episode.number .. "" "" .. episode.type",
                Type = nameof(LuaRenamer.LuaRenamer),
                ExtraData = null
            };
            var renamer = new LuaRenamer.LuaRenamer
            {
                Args = args
            };
            var res = renamer.GetInfo();
            Debug.Assert(res != null, nameof(res) + " != null");
            Assert.AreEqual("episodeTitle1 5 1", res.Value.filename);
        }

        [TestMethod]
        public void TestImportFolder()
        {
            var args = Args();
            args.Script = new RenameScriptImpl
            {
                Script = @"if (importfolders[2].type & DropFolderType.Destination) then
  destination = importfolders[2]
end",
                Type = nameof(LuaRenamer.LuaRenamer),
                ExtraData = null
            };
            var renamer = new LuaRenamer.LuaRenamer
            {
                Args = args
            };
            var res = renamer.GetInfo();
            Assert.AreSame(args.AvailableFolders[1], res?.destination);
        }

        [TestMethod]
        public void TestSandbox()
        {
            var args = Args();
            args.Script = new RenameScriptImpl
            {
                Script = @"TitleType.Main = 25
",
                Type = nameof(LuaRenamer.LuaRenamer),
                ExtraData = null
            };
            try
            {
                var renamer = new LuaRenamer.LuaRenamer
                {
                    Args = args
                };
                var res = renamer.GetInfo();
            }
            catch (LuaException ex)
            {
                Assert.IsTrue(ex.Message.Contains("attempt to update a read-only table"));
                return;
            }
            Assert.Fail("Should have thrown an LuaException with access readonly error");
        }

        [TestMethod]
        public void TestLuaLinq()
        {
            var args = Args();
            args.Script = new RenameScriptImpl
            {
                Script = @"filename = from(anime.titles):map(function(x, r) return r .. x.name; end, """")",
                Type = nameof(LuaRenamer.LuaRenamer),
                ExtraData = null
            };
            var renamer = new LuaRenamer.LuaRenamer
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
                Script = @"filename = episode_numbers(3)",
                Type = nameof(LuaRenamer.LuaRenamer),
                ExtraData = null
            };
            var renamer = new LuaRenamer.LuaRenamer
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
                Script = @"filename = anime:getname(Language.English) .. episode:getname(Language.English, true)",
                Type = nameof(LuaRenamer.LuaRenamer),
                ExtraData = null
            };
            var renamer = new LuaRenamer.LuaRenamer
            {
                Args = args
            };
            var res = renamer.GetInfo();
            Assert.AreEqual("animeTitle4episdoeTitle2", res?.filename);
        }

        [TestMethod]
        public void TestLuaEnvNames()
        {
            Assert.AreEqual("file.media.sublanguages", LuaEnv.file.media.Fn(LuaEnv.file.media.sublanguages));
        }
    }
}
