using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LuaRenamer;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NLua;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;

// ReSharper disable PossibleUnintendedReferenceComparison
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace LuaRenamerTests;

[TestClass]
public class LuaTests
{
    private static readonly ILogger<LuaRenamer.LuaRenamer> Logmock = Mock.Of<ILogger<LuaRenamer.LuaRenamer>>();

    private static MoveEventArgs MinimalArgs(string script)
    {
        return new MoveEventArgs
        {
            Script = new RenameScriptImpl
            {
                Script = script
            },
            FileInfo = Mock.Of<IVideoFile>(file =>
                file.Hashes.CRC == "abc123" && file.FilePath == "C:\\testimportfolder\\testsubfolder" && file.Filename == "testfilename.mp4"),
            AnimeInfo = new List<IAnime>
            {
                Mock.Of<IAnime>(a =>
                    a.PreferredTitle == "blah" &&
                    a.Titles == new List<AnimeTitle>() &&
                    a.EpisodeCounts == new EpisodeCounts() &&
                    a.Relations == new List<IRelatedAnime>())
            },
            AvailableFolders = new List<IImportFolder>
                { Mock.Of<IImportFolder>(i => i.Location == "C:\\testimportfolder" && i.DropFolderType == DropFolderType.Destination) },
            EpisodeInfo = new List<IEpisode> { Mock.Of<IEpisode>(e => e.Titles == new List<AnimeTitle>() && e.Type == EpisodeType.Episode) },
            GroupInfo = new List<IGroup>()
        };
    }

    [TestMethod]
    public void TestScriptRuns()
    {
        var args = MinimalArgs($@"{LuaEnv.filename} = 'testfilename'");
        var renamer = new LuaRenamer.LuaRenamer(Logmock)
        {
            Args = args
        };
        var res = renamer.GetInfo();
        Assert.AreEqual("testfilename.mp4", res?.filename);
    }

    [TestMethod]
    public void TestAnime()
    {
        var args = MinimalArgs($@"{LuaEnv.filename} = tostring({LuaEnv.anime.typeFn} == {LuaEnv.AnimeType}.{nameof(AnimeType.Movie)})");
        args.AnimeInfo[0] = Mock.Of<IAnime>(a => a.Type == AnimeType.Movie &&
                                                 a.PreferredTitle == "blah" &&
                                                 a.Titles == new List<AnimeTitle>() &&
                                                 a.EpisodeCounts == new EpisodeCounts() &&
                                                 a.Relations == new List<IRelatedAnime>());
        var renamer = new LuaRenamer.LuaRenamer(Logmock)
        {
            Args = args
        };
        var res = renamer.GetInfo();
        Assert.AreEqual("true.mp4", res?.filename);
    }

    [TestMethod]
    public void TestDateTime()
    {
        var args = MinimalArgs($@"{LuaEnv.filename} = os.date('%c', os.time({LuaEnv.file.anidb.releasedateFn}))");
        args.FileInfo = Mock.Of<IVideoFile>(file =>
            file.Hashes.CRC == args.FileInfo.Hashes.CRC &&
            file.FilePath == args.FileInfo.FilePath &&
            file.Filename == args.FileInfo.Filename &&
            file.AniDBFileInfo.ReleaseDate == new DateTime(2022, 02, 03, 5, 3, 2) &&
            file.AniDBFileInfo.MediaInfo == new AniDBMediaData { AudioLanguages = new List<TitleLanguage>(), SubLanguages = new List<TitleLanguage>() });
        var renamer = new LuaRenamer.LuaRenamer(Logmock)
        {
            Args = args
        };
        var res = renamer.GetInfo();
        Assert.AreEqual("Thu Feb  3 05_03_02 2022.mp4", res?.filename);
    }

    [TestMethod]
    public void TestEpisodes()
    {
        var args = MinimalArgs(
            $@"{LuaEnv.filename} = {LuaEnv.episode.titlesFn}[1].{LuaEnv.title.name} .. ' ' .. {LuaEnv.episode.numberFn} .. ' ' .. {LuaEnv.episode.typeFn}");
        args.EpisodeInfo[0] = Mock.Of<IEpisode>(e =>
            e.Titles == new List<AnimeTitle> { new() { Title = "episodeTitle1" } } &&
            e.Number == 5 &&
            e.Type == EpisodeType.Episode);
        var renamer = new LuaRenamer.LuaRenamer(Logmock)
        {
            Args = args
        };
        var res = renamer.GetInfo();
        Assert.IsNotNull(res);
        Assert.AreEqual("episodeTitle1 5 Episode.mp4", res.Value.filename);
    }

    [TestMethod]
    public void TestImportFolder()
    {
        var args = MinimalArgs(
            $@"if ({LuaEnv.importfolders}[2].{LuaEnv.importfolder.type} == {LuaEnv.ImportFolderType}.{nameof(DropFolderType.Destination)}) then {LuaEnv.destination} = {LuaEnv.importfolders}[2] end");
        args.AvailableFolders.Add(Mock.Of<IImportFolder>(i => i.DropFolderType == DropFolderType.Destination && i.Name == "testimport"));
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
        var args = MinimalArgs($@"{LuaEnv.filename} = from({LuaEnv.anime.titlesFn}):map(function(x, r) return r .. x.{LuaEnv.title.name}; end, '')");
        ((List<AnimeTitle>)args.AnimeInfo[0].Titles).AddRange(new AnimeTitle[]
        {
            new()
            {
                Title = "animeTitle1"
            },
            new()
            {
                Title = "animeTitle2"
            }
        });
        var renamer = new LuaRenamer.LuaRenamer(Logmock)
        {
            Args = args
        };
        var res = renamer.GetInfo();
        Assert.AreEqual("animeTitle1animeTitle2.mp4", res?.filename);
    }

    [TestMethod]
    public void TestEpisodeNumbers()
    {
        var args = MinimalArgs($@"{LuaEnv.filename} = {LuaEnv.episode_numbers}(3)");
        args.EpisodeInfo[0] = Mock.Of<IEpisode>(e => e.Titles == args.EpisodeInfo[0].Titles && e.Number == 5 && e.Type == EpisodeType.Episode);
        var renamer = new LuaRenamer.LuaRenamer(Logmock)
        {
            Args = args
        };
        var res = renamer.GetInfo();
        Assert.AreEqual("005.mp4", res?.filename);
    }

    [TestMethod]
    public void TestGetTitle()
    {
        var args = MinimalArgs(
            $@"{LuaEnv.filename} = {LuaEnv.anime.getnameFn}({LuaEnv.Language}.{nameof(TitleLanguage.English)}) .. {LuaEnv.episode.getnameFn}({LuaEnv.Language}.{nameof(TitleLanguage.English)}, true)");
        ((List<AnimeTitle>)args.AnimeInfo[0].Titles).AddRange(new AnimeTitle[]
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
        });
        ((List<AnimeTitle>)args.EpisodeInfo[0].Titles).AddRange(new List<AnimeTitle>
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
        });
        var renamer = new LuaRenamer.LuaRenamer(Logmock)
        {
            Args = args
        };
        var res = renamer.GetInfo();
        Assert.AreEqual("animeTitle4episdoeTitle2.mp4", res?.filename);
    }

    [TestMethod]
    public void TestLuaEnvNames()
    {
        Assert.AreEqual("file.media.sublanguages", LuaEnv.file.media.sublanguagesFn);
    }

    [TestMethod]
    public void TestLogging()
    {
        var args = MinimalArgs("log(\"test\")");
        var logmock = new Mock<ILogger<LuaRenamer.LuaRenamer>>();
        var renamer = new LuaRenamer.LuaRenamer(logmock.Object)
        {
            Args = args
        };
        renamer.GetInfo();

        logmock.Verify(l => l.Log(It.Is<LogLevel>(ll => ll == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString() == "test"),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [TestMethod]
    public void TestStringMethod()
    {
        var args = MinimalArgs(
            $@"function string:clean_spaces(char) return (self:match('^%s*(.-)%s*$'):gsub('%s+', char or ' ')) end
                {LuaEnv.filename} = (('blah  sdhow  wh '):clean_spaces())");
        var renamer = new LuaRenamer.LuaRenamer(Logmock)
        {
            Args = args
        };
        var res = renamer.GetInfo();
        Assert.AreEqual("blah sdhow wh.mp4", res?.filename);
    }

    [TestMethod]
    public void TestLogAbstractionVersion()
    {
        Assert.AreEqual("2.1.0.0", Assembly.GetAssembly(typeof(ILogger))?.GetName().Version?.ToString());
    }


    [TestMethod]
    public void TestEnumDefs()
    {
        void CompareEnums(LuaTable enum1, LuaTable enum2)
        {
            foreach (var (e1, e2) in new[] { (enum1, enum2), (enum2, enum1) })
                foreach (KeyValuePair<object, object> kvp in e1)
                {
                    Assert.AreEqual(kvp.Key, kvp.Value);
                    Assert.IsTrue(e2.Keys.Cast<string>().Contains(kvp.Key));
                }
        }

        var defsEnv = new Lua();
        defsEnv.DoFile(Path.Combine(LuaContext.LuaPath, "defs.lua"));
        var sandboxEnv = new LuaContext(Logmock, MinimalArgs("")).RunSandboxed();
        CompareEnums((LuaTable)defsEnv[LuaEnv.Language], (LuaTable)sandboxEnv[LuaEnv.Language]);
        CompareEnums((LuaTable)defsEnv[LuaEnv.AnimeType], (LuaTable)sandboxEnv[LuaEnv.AnimeType]);
        CompareEnums((LuaTable)defsEnv[LuaEnv.TitleType], (LuaTable)sandboxEnv[LuaEnv.TitleType]);
        CompareEnums((LuaTable)defsEnv[LuaEnv.EpisodeType], (LuaTable)sandboxEnv[LuaEnv.EpisodeType]);
        CompareEnums((LuaTable)defsEnv[LuaEnv.ImportFolderType], (LuaTable)sandboxEnv[LuaEnv.ImportFolderType]);
        CompareEnums((LuaTable)defsEnv[LuaEnv.RelationType], (LuaTable)sandboxEnv[LuaEnv.RelationType]);
    }
}
