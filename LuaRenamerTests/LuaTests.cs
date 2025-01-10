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
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Events;

// ReSharper disable PossibleUnintendedReferenceComparison
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace LuaRenamerTests;

[TestClass]
public class LuaTests
{
    private static readonly ILogger<LuaRenamer.LuaRenamer> Logmock = Mock.Of<ILogger<LuaRenamer.LuaRenamer>>();

    private static RelocationEventArgs<LuaRenamerSettings> MinimalArgs(string script)
    {
        var importFolder = Mock.Of<IImportFolder>(i => i.Path == Path.Combine("C:", "testimportfolder") &&
                                                       i.DropFolderType == DropFolderType.Destination &&
                                                       i.Name == "testimport");
        var animeMock = new Mock<ISeries>();
        animeMock.SetupGet(a => a.EpisodeCounts).Returns(new EpisodeCounts());
        animeMock.SetupGet(a => a.PreferredTitle).Returns("blah");
        animeMock.SetupGet(a => a.Titles).Returns(new List<AnimeTitle>());
        animeMock.SetupGet(a => a.RelatedSeries).Returns(new List<IRelatedMetadata<ISeries>>());
        return new RelocationEventArgs<LuaRenamerSettings>
        {
            AvailableFolders = new List<IImportFolder>
            {
                importFolder
            },
            File = Mock.Of<IVideoFile>(file =>
                file.Path == Path.Combine("C:", "testimportfolder", "testsubfolder", "testfilename.mp4") &&
                file.RelativePath == Path.Combine("testsubfolder", "testfilename.mp4") &&
                file.FileName == "testfilename.mp4" &&
                file.ImportFolder == importFolder &&
                file.VideoID == 25 &&
                file.Video == Mock.Of<IVideo>(vi => vi.Hashes.ED2K == "abc123")),
            Episodes = new List<IShokoEpisode>
            {
                Mock.Of<IShokoEpisode>(se => se.AnidbEpisode == Mock.Of<IEpisode>(e => e.Titles == new List<AnimeTitle>() && e.Type == EpisodeType.Episode))
            },
            Series = new List<IShokoSeries>
            {
                Mock.Of<IShokoSeries>(s => s.AnidbAnime == animeMock.Object && s.PreferredTitle == "shokoseriesprefname")
            },
            Groups = new List<IShokoGroup>(),
            Settings = new LuaRenamerSettings { Script = script },
            RenameEnabled = true,
            MoveEnabled = true
        };
    }

    [TestMethod]
    public void TestScriptRuns()
    {
        var args = MinimalArgs($@"{LuaEnv.Inst.filename} = 'testfilename'");
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        var res = renamer.GetNewPath(args);
        Assert.AreEqual("testfilename.mp4", res.FileName);
    }

    [TestMethod]
    public void TestAnime()
    {
        var args = MinimalArgs($"{LuaEnv.Inst.filename} = tostring({LuaEnv.anime.typeFn} == {LuaEnv.Inst.AnimeType}.{nameof(AnimeType.Movie)})");
        var animeMock = new Mock<ISeries>();
        animeMock.SetupGet(a => a.EpisodeCounts).Returns(new EpisodeCounts());
        animeMock.SetupGet(a => a.Type).Returns(AnimeType.Movie);
        animeMock.SetupGet(a => a.PreferredTitle).Returns("blah");
        animeMock.SetupGet(a => a.Titles).Returns(new List<AnimeTitle>());
        animeMock.SetupGet(a => a.RelatedSeries).Returns(new List<IRelatedMetadata<ISeries>>());
        args = new RelocationEventArgs<LuaRenamerSettings>
        {
            Settings = args.Settings,
            AvailableFolders = args.AvailableFolders,
            File = args.File,
            Episodes = args.Episodes,
            Series = new[]
            {
                Mock.Of<IShokoSeries>(s => s.AnidbAnime == animeMock.Object && s.PreferredTitle == "shokoseriesprefname")
            },
            Groups = args.Groups,
            MoveEnabled = true,
            RenameEnabled = true
        };

        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        var res = renamer.GetNewPath(args);
        Assert.AreEqual("true.mp4", res.FileName);
    }

    [TestMethod]
    public void TestDateTime()
    {
        var args = MinimalArgs($@"{LuaEnv.Inst.filename} = os.date('%c', os.time({LuaEnv.file.anidb.releasedateFn}))");
        var path = args.File.Path;
        var name = args.File.FileName;
        args = new RelocationEventArgs<LuaRenamerSettings>
        {
            Settings = args.Settings,
            AvailableFolders = args.AvailableFolders,
            File = Mock.Of<IVideoFile>(file =>
                file.Path == path &&
                file.FileName == name &&
                file.ImportFolder == Mock.Of<IImportFolder>() &&
                file.Video == Mock.Of<IVideo>(vi =>
                    vi.Hashes == Mock.Of<IHashes>() &&
                    vi.AniDB == Mock.Of<IAniDBFile>(adb =>
                        adb.ReleaseDate == new DateTime(2022, 02, 03, 5, 3, 2) && adb.MediaInfo == new AniDBMediaData
                            { AudioLanguages = new List<TitleLanguage>(), SubLanguages = new List<TitleLanguage>() }))
            ),
            Episodes = args.Episodes,
            Series = args.Series,
            Groups = args.Groups,
            MoveEnabled = true,
            RenameEnabled = true
        };
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        var res = renamer.GetNewPath(args);
        Assert.AreEqual("Thu Feb  3 05_03_02 2022.mp4", res.FileName);
    }

    [TestMethod]
    public void TestEpisodes()
    {
        var args = MinimalArgs(
            $"{LuaEnv.Inst.filename} = {LuaEnv.Inst.episode.titles[1].name} .. ' ' .. {LuaEnv.Inst.episode.number} .. ' ' .. {LuaEnv.Inst.episode.type}");
        args = new RelocationEventArgs<LuaRenamerSettings>
        {
            Settings = args.Settings,
            AvailableFolders = args.AvailableFolders,
            File = args.File,
            Episodes = new[]
            {
                Mock.Of<IShokoEpisode>(se => se.AnidbEpisode == Mock.Of<IEpisode>(e =>
                    e.Titles == new List<AnimeTitle> { new() { Title = "episodeTitle1" } } &&
                    e.EpisodeNumber == 5 &&
                    e.Type == EpisodeType.Episode))
            },
            Series = args.Series,
            Groups = args.Groups,
            MoveEnabled = true,
            RenameEnabled = true
        };
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        var res = renamer.GetNewPath(args);
        Assert.AreEqual("episodeTitle1 5 Episode.mp4", res.FileName);
    }

    [TestMethod]
    public void TestImportFolder()
    {
        var args = MinimalArgs(
            $@"
local fld = from({LuaEnv.Inst.importfolders.Fn}):where('{nameof(importfolder.type)}', {LuaEnv.Inst.ImportFolderType}.{nameof(DropFolderType.Both)}):first()
{LuaEnv.Inst.destination} = fld");
        args = new RelocationEventArgs<LuaRenamerSettings>
        {
            Settings = args.Settings,
            AvailableFolders =
                args.AvailableFolders.Append(Mock.Of<IImportFolder>(i => i.ID == 1 && i.DropFolderType == DropFolderType.Both && i.Name == "testimport"))
                    .ToList(),
            File = args.File,
            Episodes = args.Episodes,
            Series = args.Series,
            Groups = args.Groups,
            MoveEnabled = true,
            RenameEnabled = true
        };
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        var res = renamer.GetNewPath(args);
        Assert.AreSame(args.AvailableFolders[1], res.DestinationImportFolder);
    }

    [TestMethod]
    [DataRow("local array = { \"ciao\", \"hello\", \"au revoir\" }\n" +
             "filename = from(array):select(function(v) return #v; end):dump()", "q{ 4, 5, 9 }")]
    [DataRow("local array = { { say=\"ciao\", lang=\"ita\" }, { say=\"hello\", lang=\"eng\" }, }\n" +
             "filename = from(array):select(\"say\"):dump()", "q{ ciao, hello }")]
    [DataRow("local array = { \"ciao\", \"hello\", \"au revoir\" }\n" +
             "filename = from(array):selectMany(function(v) return { v, #v }; end):dump()  ", "q{ ciao, 4, hello, ...6 }")]
    [DataRow("local array = { \"ciao\", \"hello\", \"au revoir\" }\n" +
             "filename = ''\n" +
             "from(array):foreach(function (a, blah) filename = filename .. a .. blah end, 'blah')", "ciaoblahhelloblahau revoirblah")]
    [DataRow("local array = { { say=\"ciao\", lang=\"ita\" }, { say=\"hello\", lang=\"eng\" }, { say=\"au revoir\", lang=\"fre\" }}\n" +
             "array = from(array):where(\"lang\", \"ita\", \"fre\"):toArray()\n" +
             "filename = array[1].say .. array[2].say .. (array[3] and array[3].say or '')", "ciaoau revoir")]
    [DataRow("local array = { \"ciao\", \"hello\", \"au revoir\" }\n" +
             "filename = tostring(from(array):whereIndex(function (i, v) return ((i % 2)~=0); end):count())", "2")]
    [DataRow("filename = table.concat(from({'a', 'b', 'c'}):concat({'d', 'e'}):toArray())", "abcde")]
    [DataRow("filename = table.concat(from({'ablah', 'blahb', 'blac'}):where(function(a, extra) return string.find(a, extra) end, 'blah'):toArray())",
        "ablahblahb")]
    [DataRow("filename = table.concat(from({ 1, 2, 3 }):zip({4, 5, 6, 7}):selectMany(function(v) return v end):toArray())", "142536")]
    [DataRow(
        "filename = table.concat(from({{5, 'c'},{1, 'g'},{3, 'c'},{2, 'f'}}):orderBy(function(v) return v[2] end):thenBy(function(v) return v[1] end):selectMany(function(v) return v end):toArray())",
        "3c5c2f1g")]
    [DataRow("filename = table.concat(from({2,3,12,14,4,21,3,1,24}):distinct(function(a,b) return a % 4 == b % 4 end):toArray(), ' ')",
        "2 3 12 21")]
    [DataRow("filename = table.concat(from({0,3,5,2,3,0}):union({3,4,5,7}):toArray())", "035247")]
    [DataRow("filename = table.concat(from({2,4,6,3,2}):except({3,6,5}):toArray())", "242")]
    [DataRow("filename = table.concat(from({2,6,5,3}):intersection({1,2,4,3}):toArray())", "23")]
    [DataRow("filename = table.concat(from({{a=5,b=3},{a=2,b=2},{a=3,b=5}}):exceptBy('b', {5,3,4}):selectMany(function(v) return {v.a,v.b} end):toArray())",
        "22")]
    [DataRow("filename = table.concat(from({5,3,2,7,54,3}):orderBy(function(v) return v end):toArray())",
        "2335754")]
    [DataRow(
        "filename = table.concat(from({{c='I',b='C',a='H'},{c='I',b='K',a='D'},{c='E',b='G',a='G'},{c='A',b='K',a='I'},{c='B',b='H',a='J'},{c='K',b='A',a='C'},{c='B',b='K',a='G'},{c='D',b='C',a='B'},{c='G',b='H',a='B'},{c='C',b='D',a='J'}}):orderBy('a'):thenBy('b'):thenBy('c'):selectMany(function(v) return {v.c, v.b, v.a} end):toArray())",
        "DCBGHBKACIKDEGGBKGICHAKICDJBHJ")]
    [DataRow(
        "filename = table.concat(from({{c='I',b='C',a='H'},{c='I',b='K',a='D'},{c='E',b='G',a='G'},{c='A',b='K',a='I'},{c='B',b='H',a='J'},{c='K',b='A',a='C'},{c='B',b='K',a='G'},{c='D',b='C',a='B'},{c='G',b='H',a='B'},{c='C',b='D',a='J'}}):orderByDesc('a'):thenByDesc('b'):thenByDesc('c'):selectMany(function(v) return {v.c, v.b, v.a} end):toArray())",
        "BHJCDJAKIICHBKGEGGIKDKACGHBDCB")]
    public void TestLuaLinq(string lua, string expected)
    {
        var args = MinimalArgs(lua);
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        var res = renamer.GetNewPath(args);
        Assert.AreEqual(expected + ".mp4", res.FileName);
    }

    [TestMethod]
    public void TestEpisodeNumbers()
    {
        var args = MinimalArgs($@"{LuaEnv.Inst.filename} = {LuaEnv.Inst.episode_numbers}(3)");
        var titles = args.Episodes[0].AnidbEpisode.Titles;
        args = new RelocationEventArgs<LuaRenamerSettings>
        {
            Settings = args.Settings,
            AvailableFolders = args.AvailableFolders,
            File = args.File,
            Episodes = new List<IShokoEpisode>
            {
                Mock.Of<IShokoEpisode>(se =>
                    se.AnidbEpisode == Mock.Of<IEpisode>(e => e.Titles == titles && e.EpisodeNumber == 6 && e.Type == EpisodeType.Episode)),
                Mock.Of<IShokoEpisode>(se =>
                    se.AnidbEpisode == Mock.Of<IEpisode>(e => e.Titles == titles && e.EpisodeNumber == 12 && e.Type == EpisodeType.Other)),
                Mock.Of<IShokoEpisode>(se =>
                    se.AnidbEpisode == Mock.Of<IEpisode>(e => e.Titles == titles && e.EpisodeNumber == 5 && e.Type == EpisodeType.Episode)),
                Mock.Of<IShokoEpisode>(se =>
                    se.AnidbEpisode == Mock.Of<IEpisode>(e => e.Titles == titles && e.EpisodeNumber == 2 && e.Type == EpisodeType.Special)),
                Mock.Of<IShokoEpisode>(se =>
                    se.AnidbEpisode == Mock.Of<IEpisode>(e => e.Titles == titles && e.EpisodeNumber == 5 && e.Type == EpisodeType.Credits)),
                Mock.Of<IShokoEpisode>(se =>
                    se.AnidbEpisode == Mock.Of<IEpisode>(e => e.Titles == titles && e.EpisodeNumber == 7 && e.Type == EpisodeType.Episode)),
                Mock.Of<IShokoEpisode>(se =>
                    se.AnidbEpisode == Mock.Of<IEpisode>(e => e.Titles == titles && e.EpisodeNumber == 1 && e.Type == EpisodeType.Other)),
                Mock.Of<IShokoEpisode>(se =>
                    se.AnidbEpisode == Mock.Of<IEpisode>(e => e.Titles == titles && e.EpisodeNumber == 9 && e.Type == EpisodeType.Other)),
                Mock.Of<IShokoEpisode>(se =>
                    se.AnidbEpisode == Mock.Of<IEpisode>(e => e.Titles == titles && e.EpisodeNumber == 3 && e.Type == EpisodeType.Episode)),
                Mock.Of<IShokoEpisode>(se =>
                    se.AnidbEpisode == Mock.Of<IEpisode>(e => e.Titles == titles && e.EpisodeNumber == 2 && e.Type == EpisodeType.Other))
            },
            Series = args.Series,
            Groups = args.Groups,
            MoveEnabled = true,
            RenameEnabled = true
        };
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        var res = renamer.GetNewPath(args);
        Assert.AreEqual("003 005-007 C005 S002 O001-002 O009 O012.mp4", res.FileName);
    }

    [TestMethod]
    public void TestGetTitle()
    {
        var LuaEnv = new LuaEnv();
        var args = MinimalArgs(
            $@"{LuaEnv.filename} = {LuaEnv.anime.getnameFn}({LuaEnv.Language}.{nameof(TitleLanguage.English)}) .. {LuaEnv.episode.getname}({LuaEnv.Language}.{nameof(TitleLanguage.English)}, true) .. {LuaEnv.episode.getname}({LuaEnv.Language}.{nameof(TitleLanguage.Romaji)}, true)");
        ((List<AnimeTitle>)args.Series[0].AnidbAnime.Titles).AddRange(new AnimeTitle[]
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
        ((List<AnimeTitle>)args.Episodes[0].AnidbEpisode.Titles).AddRange(new List<AnimeTitle>
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
                Type = TitleType.Synonym
            }
        });
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        var res = renamer.GetNewPath(args);
        Assert.AreEqual("animeTitle4episdoeTitle2episodeTitle3.mp4", res.FileName);
    }

    [TestMethod]
    public void TestLogging()
    {
        var args = MinimalArgs("log(\"test\")");
        var logmock = new Mock<ILogger<LuaRenamer.LuaRenamer>>();
        var renamer = new LuaRenamer.LuaRenamer(logmock.Object);
        renamer.GetNewPath(args);

        logmock.Verify(l => l.Log(It.Is<LogLevel>(ll => ll == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString() == "test"),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public void TestStringMethod()
    {
        var args = MinimalArgs(
            $@"function string:clean_spaces(char) return (self:match('^%s*(.-)%s*$'):gsub('%s+', char or ' ')) end
                {LuaEnv.Inst.filename} = (('blah  sdhow  wh '):clean_spaces())");
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        var res = renamer.GetNewPath(args);
        Assert.AreEqual("blah sdhow wh.mp4", res.FileName);
    }

    [TestMethod]
    public void TestLogAbstractionVersion()
    {
        Assert.AreEqual("8.0.0.0", Assembly.GetAssembly(typeof(ILogger))?.GetName().Version?.ToString());
    }


    [TestMethod]
    public void TestEnumDefs()
    {
        void CompareEnums(LuaTable enum1, LuaTable enum2)
        {
            var e1Set = new HashSet<string>();
            var e2Set = new HashSet<string>();
            foreach (KeyValuePair<object, object> kvp in enum1)
            {
                Assert.AreEqual(kvp.Key, kvp.Value);
                e1Set.Add((string)kvp.Key);
            }

            foreach (KeyValuePair<object, object> kvp in enum2)
            {
                Assert.AreEqual(kvp.Key, kvp.Value);
                e2Set.Add((string)kvp.Key);
            }

            var e2Missing = e1Set.Except(e2Set).ToList();
            var e1Missing = e2Set.Except(e1Set).ToList();

            Assert.IsFalse(e2Missing.Any());
            Assert.IsFalse(e1Missing.Any());
        }

        var defsEnv = new Lua();
        defsEnv.DoFile(Path.Combine(LuaContext.LuaPath, "enums.lua"));
        var sandboxEnv = new LuaContext(Logmock, MinimalArgs("")).RunSandboxed();
        CompareEnums((LuaTable)defsEnv[LuaEnv.Inst.Language], (LuaTable)sandboxEnv[LuaEnv.Inst.Language]);
        CompareEnums((LuaTable)defsEnv[LuaEnv.Inst.AnimeType], (LuaTable)sandboxEnv[LuaEnv.Inst.AnimeType]);
        CompareEnums((LuaTable)defsEnv[LuaEnv.Inst.TitleType], (LuaTable)sandboxEnv[LuaEnv.Inst.TitleType]);
        CompareEnums((LuaTable)defsEnv[LuaEnv.Inst.EpisodeType], (LuaTable)sandboxEnv[LuaEnv.Inst.EpisodeType]);
        CompareEnums((LuaTable)defsEnv[LuaEnv.Inst.ImportFolderType], (LuaTable)sandboxEnv[LuaEnv.Inst.ImportFolderType]);
        CompareEnums((LuaTable)defsEnv[LuaEnv.Inst.RelationType], (LuaTable)sandboxEnv[LuaEnv.Inst.RelationType]);
    }

    [TestMethod]
    public void TestRelations()
    {
        var args = MinimalArgs(
            $"{LuaEnv.Inst.filename} = {LuaEnv.anime.relations.Fn}[1].{LuaEnv.anime.relations.anime}.{LuaEnv.anime.preferredname} .. {LuaEnv.anime.relations.Fn}[1].{LuaEnv.anime.relations.type} .. #{LuaEnv.anime.relations.Fn}[1].{LuaEnv.anime.relations.anime}.{LuaEnv.anime.relations.N}");
        var animeMock = new Mock<ISeries>();
        animeMock.SetupGet(a => a.EpisodeCounts).Returns(new EpisodeCounts());
        animeMock.SetupGet(a => a.ID).Returns(1);
        animeMock.SetupGet(a => a.PreferredTitle).Returns("blah2");
        animeMock.SetupGet(a => a.Titles).Returns(new List<AnimeTitle>());
        animeMock.SetupGet(a => a.RelatedSeries).Returns(new List<IRelatedMetadata<ISeries>>
        {
            Mock.Of<IRelatedMetadata<ISeries>>(r2 => r2.Related == args.Series[0].AnidbAnime &&
                                                     r2.RelationType == RelationType.Prequel)
        });
        ((List<IRelatedMetadata<ISeries>>)args.Series[0].AnidbAnime.RelatedSeries).Add(Mock.Of<IRelatedMetadata<ISeries>>(r =>
            r.RelationType == RelationType.AlternativeSetting &&
            r.Related == animeMock.Object
        ));
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        var res = renamer.GetNewPath(args);
        Assert.AreEqual("blah2AlternativeSetting0.mp4", res.FileName);
    }

    [TestMethod]
    public void TestApiMethods()
    {
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        var args = MinimalArgs("filename = 'blah'");
        var result = renamer.GetNewPath(args);
        Assert.AreEqual("blah.mp4", result.FileName);
        Assert.IsNotNull(result.DestinationImportFolder);
        Assert.AreEqual("shokoseriesprefname", result.Path);
    }

    [TestMethod]
    public void TestSkipping()
    {
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        var args = MinimalArgs("filename = 'blah'\nsubfolder = {'blah'}\nskip_rename = true\nskip_move = true");
        var result = renamer.GetNewPath(args);
        Assert.AreEqual(null, result.FileName);
        Assert.AreEqual(null, result.Path);
    }

    [TestMethod]
    public void TestLinqLog()
    {
        var args = MinimalArgs("linqSetLogLevel(3); from({'test1', 'test2'})");
        var logmock = new Mock<ILogger<LuaRenamer.LuaRenamer>>();
        var renamer = new LuaRenamer.LuaRenamer(logmock.Object);
        renamer.GetNewPath(args);

        logmock.Verify(l => l.Log(It.Is<LogLevel>(ll => ll == LogLevel.Debug),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.StartsWith("LuaLinq: after fromArrayInstance => 2 items : q{ test1, test2 }")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public void TestLineEndings()
    {
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        var args = MinimalArgs("filename = 'blah'\r\nfilename = 'argle'\nfilename = 'blargle'\rfilename = 'test'");
        var result = renamer.GetNewPath(args);

        Assert.AreEqual("test.mp4", result.FileName);
    }

    [TestMethod]
    public void TestDefaultScript()
    {
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        var defaultScript = renamer.DefaultSettings;
        Assert.IsNotNull(defaultScript?.Script);
    }
}
