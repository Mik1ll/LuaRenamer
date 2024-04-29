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
        var importFolder = Mock.Of<IImportFolder>(i => i.Path == Path.Combine("C:", "testimportfolder") &&
                                                       i.DropFolderType == DropFolderType.Destination &&
                                                       i.Name == "testimport");
        return new MoveEventArgs(new RenameScriptImpl
            {
                Script = script,
                Type = LuaRenamer.LuaRenamer.RenamerId
            }, new List<IImportFolder>
            {
                importFolder
            }, Mock.Of<IVideoFile>(file =>
                file.Path == Path.Combine("C:", "testimportfolder", "testsubfolder", "testfilename.mp4") &&
                file.RelativePath == Path.Combine("testsubfolder", "testfilename.mp4") &&
                file.FileName == "testfilename.mp4" &&
                file.ImportFolder == importFolder &&
                file.VideoID == 25), Mock.Of<IVideo>(vi => vi.Hashes.ED2K == "abc123"),
            new List<IEpisode> { Mock.Of<IEpisode>(e => e.Titles == new List<AnimeTitle>() && e.Type == EpisodeType.Episode) },
            new List<IAnime>
            {
                Mock.Of<IAnime>(a =>
                    a.PreferredTitle == "blah" &&
                    a.Titles == new List<AnimeTitle>() &&
                    a.EpisodeCounts == new EpisodeCounts() &&
                    a.Relations == new List<IRelatedAnime>())
            }, new List<IGroup>());
    }

    private static RenameEventArgs RenameArgs(MoveEventArgs args) =>
        new(args.Script, args.AvailableFolders, args.FileInfo, args.VideoInfo, args.EpisodeInfo, args.AnimeInfo, args.GroupInfo)
        {
            Cancel = args.Cancel
        };

    [TestMethod]
    public void TestScriptRuns()
    {
        var args = MinimalArgs($@"{LuaEnv.filename} = 'testfilename'");
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        renamer.SetupArgs(args);
        var res = renamer.GetInfo();
        Assert.AreEqual("testfilename.mp4", res?.filename);
    }

    [TestMethod]
    public void TestAnime()
    {
        var args = MinimalArgs($"{LuaEnv.filename} = tostring({LuaEnv.anime.typeFn} == {LuaEnv.AnimeType}.{nameof(AnimeType.Movie)})");
        args = new MoveEventArgs(args.Script, args.AvailableFolders, args.FileInfo, args.VideoInfo, args.EpisodeInfo, new[]
        {
            Mock.Of<IAnime>(a => a.Type == AnimeType.Movie &&
                                 a.PreferredTitle == "blah" &&
                                 a.Titles == new List<AnimeTitle>() &&
                                 a.EpisodeCounts == new EpisodeCounts() &&
                                 a.Relations == new List<IRelatedAnime>())
        }, args.GroupInfo);
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        renamer.SetupArgs(args);
        var res = renamer.GetInfo();
        Assert.AreEqual("true.mp4", res?.filename);
    }

    [TestMethod]
    public void TestDateTime()
    {
        var args = MinimalArgs($@"{LuaEnv.filename} = os.date('%c', os.time({LuaEnv.file.anidb.releasedateFn}))");
        var path = args.FileInfo.Path;
        var name = args.FileInfo.FileName;
        args = new MoveEventArgs(args.Script, args.AvailableFolders, Mock.Of<IVideoFile>(file =>
                file.Path == path &&
                file.FileName == name),
            Mock.Of<IVideo>(vi => vi.Hashes == Mock.Of<IHashes>() &&
                                  vi.AniDB == Mock.Of<IAniDBFile>(adb =>
                                      adb.ReleaseDate == new DateTime(2022, 02, 03, 5, 3, 2) && adb.MediaInfo == new AniDBMediaData
                                          { AudioLanguages = new List<TitleLanguage>(), SubLanguages = new List<TitleLanguage>() })), args.EpisodeInfo,
            args.AnimeInfo, args.GroupInfo);
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        renamer.SetupArgs(args);
        var res = renamer.GetInfo();
        Assert.AreEqual("Thu Feb  3 05_03_02 2022.mp4", res?.filename);
    }

    [TestMethod]
    public void TestEpisodes()
    {
        var args = MinimalArgs(
            $"{LuaEnv.filename} = {LuaEnv.episode.titlesFn}[1].{LuaEnv.title.name} .. ' ' .. {LuaEnv.episode.numberFn} .. ' ' .. {LuaEnv.episode.typeFn}");
        args = new MoveEventArgs(args.Script, args.AvailableFolders, args.FileInfo, args.VideoInfo, new[]
        {
            Mock.Of<IEpisode>(e =>
                e.Titles == new List<AnimeTitle> { new() { Title = "episodeTitle1" } } &&
                e.EpisodeNumber == 5 &&
                e.Type == EpisodeType.Episode)
        }, args.AnimeInfo, args.GroupInfo);
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        renamer.SetupArgs(args);
        var res = renamer.GetInfo();
        Assert.AreEqual("episodeTitle1 5 Episode.mp4", res?.filename);
    }

    [TestMethod]
    public void TestImportFolder()
    {
        var args = MinimalArgs(
            $@"
local fld = from({LuaEnv.importfolders}):where('{LuaEnv.importfolder.type}', {LuaEnv.ImportFolderType}.{nameof(DropFolderType.Both)}):first()
{LuaEnv.destination} = fld");
        args = new MoveEventArgs(args.Script,
            args.AvailableFolders.Append(Mock.Of<IImportFolder>(i => i.ID == 1 && i.DropFolderType == DropFolderType.Both && i.Name == "testimport")),
            args.FileInfo,
            args.VideoInfo, args.EpisodeInfo, args.AnimeInfo, args.GroupInfo);
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        renamer.SetupArgs(args);
        var res = renamer.GetInfo();
        Assert.AreSame(args.AvailableFolders[1], res?.destination);
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
        renamer.SetupArgs(args);
        var res = renamer.GetInfo();
        Assert.AreEqual(expected + ".mp4", res?.filename);
    }

    [TestMethod]
    public void TestEpisodeNumbers()
    {
        var args = MinimalArgs($@"{LuaEnv.filename} = {LuaEnv.episode_numbers}(3)");
        var titles = args.EpisodeInfo[0].Titles;
        args = new MoveEventArgs(args.Script, args.AvailableFolders, args.FileInfo, args.VideoInfo, new List<IEpisode>
        {
            Mock.Of<IEpisode>(e => e.Titles == titles && e.EpisodeNumber == 6 && e.Type == EpisodeType.Episode),
            Mock.Of<IEpisode>(e => e.Titles == titles && e.EpisodeNumber == 12 && e.Type == EpisodeType.Other),
            Mock.Of<IEpisode>(e => e.Titles == titles && e.EpisodeNumber == 5 && e.Type == EpisodeType.Episode),
            Mock.Of<IEpisode>(e => e.Titles == titles && e.EpisodeNumber == 2 && e.Type == EpisodeType.Special),
            Mock.Of<IEpisode>(e => e.Titles == titles && e.EpisodeNumber == 5 && e.Type == EpisodeType.Credits),
            Mock.Of<IEpisode>(e => e.Titles == titles && e.EpisodeNumber == 7 && e.Type == EpisodeType.Episode),
            Mock.Of<IEpisode>(e => e.Titles == titles && e.EpisodeNumber == 1 && e.Type == EpisodeType.Other),
            Mock.Of<IEpisode>(e => e.Titles == titles && e.EpisodeNumber == 9 && e.Type == EpisodeType.Other),
            Mock.Of<IEpisode>(e => e.Titles == titles && e.EpisodeNumber == 3 && e.Type == EpisodeType.Episode),
            Mock.Of<IEpisode>(e => e.Titles == titles && e.EpisodeNumber == 2 && e.Type == EpisodeType.Other)
        }, args.AnimeInfo, args.GroupInfo);
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        renamer.SetupArgs(args);
        var res = renamer.GetInfo();
        Assert.AreEqual("003 005-007 C005 S002 O001-002 O009 O012.mp4", res?.filename);
    }

    [TestMethod]
    public void TestGetTitle()
    {
        var args = MinimalArgs(
            $@"{LuaEnv.filename} = {LuaEnv.anime.getnameFn}({LuaEnv.Language}.{nameof(TitleLanguage.English)}) .. {LuaEnv.episode.getnameFn}({LuaEnv.Language}.{nameof(TitleLanguage.English)}, true) .. {LuaEnv.episode.getnameFn}({LuaEnv.Language}.{nameof(TitleLanguage.Romaji)}, true)");
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
                Type = TitleType.Synonym
            }
        });
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        renamer.SetupArgs(args);
        var res = renamer.GetInfo();
        Assert.AreEqual("animeTitle4episdoeTitle2episodeTitle3.mp4", res?.filename);
    }

    [TestMethod]
    public void TestLogging()
    {
        var args = MinimalArgs("log(\"test\")");
        var logmock = new Mock<ILogger<LuaRenamer.LuaRenamer>>();
        var renamer = new LuaRenamer.LuaRenamer(logmock.Object);
        renamer.SetupArgs(args);
        renamer.GetInfo();

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
                {LuaEnv.filename} = (('blah  sdhow  wh '):clean_spaces())");
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        renamer.SetupArgs(args);
        var res = renamer.GetInfo();
        Assert.AreEqual("blah sdhow wh.mp4", res?.filename);
    }

    [TestMethod]
    public void TestLogAbstractionVersion()
    {
        Assert.AreEqual("6.0.0.0", Assembly.GetAssembly(typeof(ILogger))?.GetName().Version?.ToString());
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
        defsEnv.DoFile(Path.Combine(LuaContext.LuaPath, "enums.lua"));
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        renamer.SetupArgs(MinimalArgs(""));
        var sandboxEnv = new LuaContext(Logmock, renamer).RunSandboxed();
        CompareEnums((LuaTable)defsEnv[LuaEnv.Language], (LuaTable)sandboxEnv[LuaEnv.Language]);
        CompareEnums((LuaTable)defsEnv[LuaEnv.AnimeType], (LuaTable)sandboxEnv[LuaEnv.AnimeType]);
        CompareEnums((LuaTable)defsEnv[LuaEnv.TitleType], (LuaTable)sandboxEnv[LuaEnv.TitleType]);
        CompareEnums((LuaTable)defsEnv[LuaEnv.EpisodeType], (LuaTable)sandboxEnv[LuaEnv.EpisodeType]);
        CompareEnums((LuaTable)defsEnv[LuaEnv.ImportFolderType], (LuaTable)sandboxEnv[LuaEnv.ImportFolderType]);
        CompareEnums((LuaTable)defsEnv[LuaEnv.RelationType], (LuaTable)sandboxEnv[LuaEnv.RelationType]);
    }

    [TestMethod]
    public void TestRelations()
    {
        var args = MinimalArgs(
            $"{LuaEnv.filename} = {LuaEnv.anime.relations.Fn}[1].{LuaEnv.anime.relations.anime}.{LuaEnv.anime.preferredname} .. {LuaEnv.anime.relations.Fn}[1].{LuaEnv.anime.relations.type} .. #{LuaEnv.anime.relations.Fn}[1].{LuaEnv.anime.relations.anime}.{LuaEnv.anime.relations.N}");
        ((List<IRelatedAnime>)args.AnimeInfo[0].Relations).Add(Mock.Of<IRelatedAnime>(r =>
            r.RelationType == RelationType.AlternativeSetting &&
            r.RelatedAnime == Mock.Of<IAnime>(a =>
                a.ID == 1 &&
                a.PreferredTitle == "blah2" &&
                a.Titles == new List<AnimeTitle>() &&
                a.EpisodeCounts == new EpisodeCounts() &&
                a.Relations == new List<IRelatedAnime>
                {
                    Mock.Of<IRelatedAnime>(r2 => r2.RelatedAnime == args.AnimeInfo[0] &&
                                                 r2.RelationType == RelationType.Prequel)
                })
        ));
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        renamer.SetupArgs(args);
        var res = renamer.GetInfo();
        Assert.AreEqual("blah2AlternativeSetting0.mp4", res?.filename);
    }

    [TestMethod]
    public void TestApiMethods()
    {
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        var args = MinimalArgs("filename = 'blah'");
        renamer.SetupArgs(args);
        var filename = renamer.GetFilename(RenameArgs(args));
        Assert.AreEqual("blah.mp4", filename);
        var moveres = renamer.GetDestination(MinimalArgs($"{LuaEnv.destination} = 'testimport'\n{LuaEnv.subfolder} = {{'blah'}}"));
        Assert.IsNotNull(moveres.destination);
        Assert.AreEqual("blah", moveres.subfolder);
    }

    [TestMethod]
    public void TestSkipping()
    {
        var renamer = new LuaRenamer.LuaRenamer(Logmock);
        var args = MinimalArgs("filename = 'blah'\nsubfolder = {'blah'}\nskip_rename = true\nskip_move = true");
        renamer.SetupArgs(args);
        var result = renamer.GetFilename(RenameArgs(args));
        Assert.AreEqual(args.FileInfo.FileName, result);
        var dstResult = renamer.GetDestination(args);
        Assert.AreEqual("testsubfolder", dstResult.subfolder);
    }

    [TestMethod]
    public void TestLinqLog()
    {
        var args = MinimalArgs("linqSetLogLevel(3); from({'test1', 'test2'})");
        var logmock = new Mock<ILogger<LuaRenamer.LuaRenamer>>();
        var renamer = new LuaRenamer.LuaRenamer(logmock.Object);
        renamer.SetupArgs(args);
        renamer.GetInfo();

        logmock.Verify(l => l.Log(It.Is<LogLevel>(ll => ll == LogLevel.Debug),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.StartsWith("LuaLinq: after fromArrayInstance => 2 items : q{ test1, test2 }")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
