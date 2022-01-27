using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;

namespace ScriptRenamerTests
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
                                Type = TitleType.Main
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
                                Type = TitleType.Main
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
                        e.Titles == new List<AnimeTitle>
                        {
                            new()
                            {
                                Title = "episodeTitle1",
                                Language = TitleLanguage.English,
                                Type = TitleType.Official
                            },
                            new()
                            {
                                Title = "episdoeTitle2",
                                Language = TitleLanguage.Danish,
                                Type = TitleType.Main
                            },
                            new()
                            {
                                Title = "episodeTitle3",
                                Language = TitleLanguage.Romaji,
                                Type = TitleType.Main
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
                Type = nameof(ScriptRenamer.ScriptRenamer)
            };
            var res = ScriptRenamer.ScriptRenamer.GetInfo(args);
            Assert.AreEqual(res!.Value.filename, "testfilename");
        }

        [TestMethod]
        public void TestAnime()
        {
            var args = Args();
            args.Script = new RenameScriptImpl
            {
                Script = @"filename = tostring(anime[1].type == AnimeType.Movie)",
                Type = nameof(ScriptRenamer.ScriptRenamer),
                ExtraData = null
            };
            var res = ScriptRenamer.ScriptRenamer.GetInfo(args);
            Assert.AreEqual(res!.Value.filename, "true");
        }

        [TestMethod]
        public void TestFile()
        {
            var args = Args();
            args.Script = new RenameScriptImpl
            {
                Script = @"filename = os.date(""%c"", os.time(file.anidb.releasedate))",
                Type = nameof(ScriptRenamer.ScriptRenamer),
                ExtraData = null
            };
            var res = ScriptRenamer.ScriptRenamer.GetInfo(args);
        }

        [TestMethod]
        public void TestEpisodes()
        {
            var args = Args();
            args.Script = new RenameScriptImpl
            {
                Script = @"local episode = episodes[1]
filename = episode.titles[1].title .. "" "" .. episode.number .. "" "" .. episode.type",
                Type = nameof(ScriptRenamer.ScriptRenamer),
                ExtraData = null
            };
            var res = ScriptRenamer.ScriptRenamer.GetInfo(args);
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
                Type = nameof(ScriptRenamer.ScriptRenamer),
                ExtraData = null
            };
            var res = ScriptRenamer.ScriptRenamer.GetInfo(args);
            Assert.AreSame(res!.Value.destination, args.AvailableFolders[1]);
        }
    }
}
