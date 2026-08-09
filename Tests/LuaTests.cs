using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using LuaRenamer.DefsGenerator;
using LuaRenamer.LuaEnv;
using LuaRenamer.LuaEnv.Names;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NLua;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Hashing;
using Shoko.Abstractions.Video.Release;
using Shoko.Abstractions.Video.Relocation;

namespace LuaRenamer.Tests;

[TestClass]
public class LuaTests
{
    private static readonly EnvNames Env = new EnvNames();
    private static readonly ILogger<LuaRenamer> Logmock = Mock.Of<ILogger<LuaRenamer>>();

    private static RelocationContext<LuaRenamerSettings> MinimalArgs(string script)
    {
        var importFolder = Mock.Of<IManagedFolder>(i => i.Path == Path.Combine("C:", "testimportfolder") &&
            i.DropFolderType == DropFolderType.Destination &&
            i.Name == "testimport");
        var animeMock = new Mock<IAnidbAnime>();
        animeMock.SetupGet(a => a.EpisodeCounts).Returns(new EpisodeCounts());
        animeMock.SetupGet(a => a.Title).Returns("blah");
        var titleMock = Mock.Of<ITitle>(t => t.Value == "blah");
        animeMock.SetupGet(a => a.DefaultTitle).Returns(titleMock);
        animeMock.SetupGet(a => a.Titles).Returns(new List<ITitle>());
        animeMock.SetupGet(a => a.RelatedSeries).Returns(new List<IRelatedMetadata<ISeries, ISeries>>());
        animeMock.SetupGet(a => a.ID).Returns(3);
        var shokoSeries = Mock.Of<IShokoSeries>(s => s.AnidbAnimeID == 3 &&
            s.AnidbAnime == animeMock.Object &&
            s.Title == "shokoseriesprefname" &&
            s.TmdbMovies == new List<ITmdbMovie>() &&
            s.TmdbShows == new List<ITmdbShow>() &&
            s.Tags == new List<IShokoTagForSeries>() &&
            s.DefaultTitle == titleMock);
        animeMock.SetupGet(a => a.ShokoSeries).Returns([shokoSeries]);
        animeMock.SetupGet(a => a.Studios).Returns([]);
        animeMock.SetupGet(a => a.Tags).Returns([]);
        animeMock.SetupGet(a => a.YearlySeasons).Returns([]);
        return new RelocationContext<LuaRenamerSettings>(new RelocationContext
        {
            CancellationToken = CancellationToken.None,
            AvailableFolders = new List<IManagedFolder>
            {
                importFolder,
            },
            File = Mock.Of<IVideoFile>(file =>
                file.Path == Path.Combine("C:", "testimportfolder", "testsubfolder", "testfilename.mp4") &&
                file.RelativePath == Path.Combine("testsubfolder", "testfilename.mp4") &&
                file.FileName == "testfilename.mp4" &&
                file.ManagedFolderID == importFolder.ID &&
                file.ManagedFolder == importFolder &&
                file.VideoID == 25 &&
                file.Video == Mock.Of<IVideo>(vi => vi.ED2K == "abc123" && vi.Hashes == new List<IHashDigest>())),
            Episodes = new List<IShokoEpisode>
            {
                Mock.Of<IShokoEpisode>(se =>
                    se.AnidbEpisode == Mock.Of<IAnidbEpisode>(e => e.SeriesID == 3 &&
                        e.Titles == new List<ITitle>() &&
                        e.Type == EpisodeType.Episode) &&
                    se.TmdbEpisodes == new List<ITmdbEpisode>()),
            },
            Series = new List<IShokoSeries>
            {
                shokoSeries,
            },
            Groups = new List<IShokoGroup>(),
            RenameEnabled = true,
            MoveEnabled = true,
        }, new LuaRenamerSettings { Script = script });
    }

    [TestMethod]
    public void TestScriptRuns()
    {
        var args = MinimalArgs($"{Env.filename} = 'testfilename'");
        var renamer = new LuaRenamer(Logmock);
        var res = renamer.GetPath(args);
        Assert.AreEqual("testfilename.mp4", res.FileName);
    }

    [TestMethod]
    public void TestAnime()
    {
        var args = MinimalArgs($"{Env.filename} = tostring({Env.anime.type} == {Env.AnimeType[AnimeType.Movie]})");
        var animeMock = new Mock<IAnidbAnime>();
        animeMock.SetupGet(a => a.EpisodeCounts).Returns(new EpisodeCounts());
        animeMock.SetupGet(a => a.Type).Returns(AnimeType.Movie);
        animeMock.SetupGet(a => a.Title).Returns("blah");
        var titleMock = Mock.Of<ITitle>(t => t.Value == "blah");
        animeMock.SetupGet(a => a.DefaultTitle).Returns(titleMock);
        animeMock.SetupGet(a => a.Titles).Returns(new List<ITitle>());
        animeMock.SetupGet(a => a.RelatedSeries).Returns(new List<IRelatedMetadata<ISeries, ISeries>>());
        animeMock.SetupGet(a => a.ID).Returns(3);
        animeMock.SetupGet(a => a.Studios).Returns([]);
        animeMock.SetupGet(a => a.Tags).Returns([]);
        animeMock.SetupGet(a => a.YearlySeasons).Returns([]);
        var shokoSeries = Mock.Of<IShokoSeries>(s =>
            s.AnidbAnime == animeMock.Object &&
            s.Title == "shokoseriesprefname" &&
            s.AnidbAnimeID == 3 &&
            s.TmdbMovies == new List<ITmdbMovie>() &&
            s.TmdbShows == new List<ITmdbShow>() &&
            s.Tags == new List<IShokoTagForSeries>() &&
            s.DefaultTitle == titleMock);
        animeMock.SetupGet(a => a.ShokoSeries).Returns([shokoSeries]);
        args = new RelocationContext<LuaRenamerSettings>(new RelocationContext
        {
            AvailableFolders = args.AvailableFolders,
            File = args.File,
            Episodes = args.Episodes,
            Series =
            [
                shokoSeries,
            ],
            Groups = args.Groups,
            MoveEnabled = true,
            RenameEnabled = true,
        }, args.Configuration);

        var renamer = new LuaRenamer(Logmock);
        var res = renamer.GetPath(args);
        Assert.AreEqual("true.mp4", res.FileName);
    }

    [TestMethod]
    public void TestDateTime()
    {
        var args = MinimalArgs($"{Env.filename} = os.date('%c', os.time({Env.file.anidb.releasedate}))");
        var path = args.File.Path;
        var name = args.File.FileName;
        args = new RelocationContext<LuaRenamerSettings>(new RelocationContext
        {
            AvailableFolders = args.AvailableFolders,
            File = Mock.Of<IVideoFile>(file =>
                file.Path == path &&
                file.FileName == name &&
                file.ManagedFolder == Mock.Of<IManagedFolder>() &&
                file.Video == Mock.Of<IVideo>(vi =>
                    vi.ED2K == "abc123" &&
                    vi.Hashes == new List<IHashDigest>() &&
                    vi.ReleaseInfo == Mock.Of<IReleaseInfo>(adb =>
                        adb.ReleaseURI == "https://anidb.net/file/1234" &&
                        adb.ReleasedAt == new DateOnly(2022, 02, 03) && adb.MediaInfo == Mock.Of<IReleaseMediaInfo>(m =>
                            m.AudioLanguages == new List<TitleLanguage>() && m.SubtitleLanguages == new List<TitleLanguage>())))
            ),
            Episodes = args.Episodes,
            Series = args.Series,
            Groups = args.Groups,
            MoveEnabled = true,
            RenameEnabled = true,
        }, args.Configuration);
        var renamer = new LuaRenamer(Logmock);
        var res = renamer.GetPath(args);
        Assert.AreEqual("Thu Feb  3 00_00_00 2022.mp4", res.FileName);
    }

    [TestMethod]
    public void TestEpisodes()
    {
        var args = MinimalArgs(
            $"{Env.filename} = {Env.episode.titles[1].name} .. ' ' .. {Env.episode.number} .. ' ' .. {Env.episode.type}");
        args = new RelocationContext<LuaRenamerSettings>(new RelocationContext
        {
            AvailableFolders = args.AvailableFolders,
            File = args.File,
            Episodes =
            [
                Mock.Of<IShokoEpisode>(se => se.AnidbEpisode == Mock.Of<IAnidbEpisode>(e =>
                        e.Titles == new List<ITitle>
                            { new TitleStub { Value = "episodeTitle1", Language = TitleLanguage.Unknown, LanguageCode = "unk", Source = DataSource.User } } &&
                        e.EpisodeNumber == 5 &&
                        e.Type == EpisodeType.Episode &&
                        e.SeriesID == 3) &&
                    se.TmdbEpisodes == new List<ITmdbEpisode>()),
            ],
            Series = args.Series,
            Groups = args.Groups,
            MoveEnabled = true,
            RenameEnabled = true,
        }, args.Configuration);
        var renamer = new LuaRenamer(Logmock);
        var res = renamer.GetPath(args);
        Assert.AreEqual("episodeTitle1 5 Episode.mp4", res.FileName);
    }

    [TestMethod]
    public void TestImportFolder()
    {
        var args = MinimalArgs(
            $"""
            local fld = from({Env.importfolders.Fn}):where('{nameof(ImportFolderNames.type)}', {Env.ImportFolderType[DropFolderType.Both]}):first()
            {Env.destination} = fld
            """);
        args = new RelocationContext<LuaRenamerSettings>(new RelocationContext
        {
            AvailableFolders =
                args.AvailableFolders.Append(Mock.Of<IManagedFolder>(i => i.ID == 1 && i.DropFolderType == DropFolderType.Both && i.Name == "testimport"))
                    .ToList(),
            File = args.File,
            Episodes = args.Episodes,
            Series = args.Series,
            Groups = args.Groups,
            MoveEnabled = true,
            RenameEnabled = true,
        }, args.Configuration);
        var renamer = new LuaRenamer(Logmock);
        var res = renamer.GetPath(args);
        Assert.AreSame(args.AvailableFolders[1], res.ManagedFolder);
    }

    [TestMethod]
    [DataRow("local array = { 'ciao', 'hello', 'au revoir' }\n" +
        "filename = from(array):select(function(v) return #v; end):dump()", "q{ 4, 5, 9 }")]
    [DataRow("local array = { { say='ciao', lang='ita' }, { say='hello', lang='eng' }, }\n" +
        "filename = from(array):select('say'):dump()", "q{ ciao, hello }")]
    [DataRow("local array = { 'ciao', 'hello', 'au revoir' }\n" +
        "filename = from(array):selectMany(function(v) return { v, #v }; end):dump()  ", "q{ ciao, 4, hello, ...6 }")]
    [DataRow("local array = { 'ciao', 'hello', 'au revoir' }\n" +
        "filename = ''\n" +
        "from(array):foreach(function (a, blah) filename = filename .. a .. blah end, 'blah')", "ciaoblahhelloblahau revoirblah")]
    [DataRow("local array = { { say='ciao', lang='ita' }, { say='hello', lang='eng' }, { say='au revoir', lang='fre' }}\n" +
        "array = from(array):where('lang', 'ita', 'fre'):toArray()\n" +
        "filename = array[1].say .. array[2].say .. (array[3] and array[3].say or '')", "ciaoau revoir")]
    [DataRow("local array = { 'ciao', 'hello', 'au revoir' }\n" +
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
        var renamer = new LuaRenamer(Logmock);
        var res = renamer.GetPath(args);
        Assert.AreEqual(expected + ".mp4", res.FileName);
    }

    [TestMethod]
    
    // @formatter:off
    [DataRow(
        new []     { 3, 3, 3 },
        new []     { 1, 3, 5 },
        new byte[] { 1, 1, 1 },
        2, "01 03 05.mp4")]
    [DataRow(
        new []     { 3, 3, 3, 3 },
        new []     { 1, 2, 1, 2 },
        new byte[] { 1, 1, 2, 2 },
        2, "01-02 C01-02.mp4")]
    [DataRow(
        new []     { 3, 3, 3, 3, 3 },
        new []     { 5, 1, 3, 2, 4 },
        new byte[] { 1, 1, 1, 1, 1 },
        2, "01-05.mp4")]
    [DataRow(
        new []     { 3,   3,  3,  3 },
        new []     { 10, 11, 12, 13 },
        new byte[] {  1,  1,  2,  2 },
        2, "10-11 C12-13.mp4")]
    [DataRow(
        new []     { 3, 3 },
        new []     { 1, 2 },
        new byte[] { 1, 2 },
        2, "01 C02.mp4")]
    [DataRow(
        new[]      {  3,  3, 3,  2, 3,  6, 3, 3, 3, 9, 3, 3, 3 },
        new[]      {  6, 12, 5, 22, 2, 20, 5, 7, 1, 4, 9, 3, 2 },
        new byte[] {  1,  6, 1,  1, 3,  1, 2, 1, 6, 1, 6, 1, 6 },
        3, "003 005-007 C005 S002 O001-002 O009 O012.mp4")]
    // @formatter:on
    public void TestEpisodeNumbers(int[] seriesIds, int[] epNums, byte[] epTypes, int pad, string expected)
    {
        var args = MinimalArgs($"{Env.filename} = {Env.episode_numbers(pad.ToString())}");
        var titles = args.Episodes[0].AnidbEpisode.Titles;
        IEnumerable<(int seriesId, int epNum, EpisodeType epType)> zipped = seriesIds.Zip(epNums, epTypes.Cast<EpisodeType>());
        var eps = zipped.Select(z => Mock.Of<IShokoEpisode>(se =>
            se.AnidbEpisode == Mock.Of<IAnidbEpisode>(e => e.SeriesID == z.seriesId &&
                e.Titles == titles &&
                e.EpisodeNumber == z.epNum &&
                e.Type == z.epType) &&
            se.TmdbEpisodes == new List<ITmdbEpisode>())).ToList();

        args = new RelocationContext<LuaRenamerSettings>(new RelocationContext
        {
            AvailableFolders = args.AvailableFolders,
            File = args.File,
            Episodes = eps,
            Series = args.Series,
            Groups = args.Groups,
            MoveEnabled = true,
            RenameEnabled = true,
        }, args.Configuration);
        var renamer = new LuaRenamer(Logmock);
        var res = renamer.GetPath(args);
        Assert.AreEqual(expected, res.FileName);
    }

    [TestMethod]
    public void TestGetTitle()
    {
        var args = MinimalArgs(
            $"{Env.filename} = {Env.anime.getname(Env.Language[TitleLanguage.English])} .. {Env.episode.getname(Env.Language[TitleLanguage.English])} .. {Env.episode.getname(Env.Language[TitleLanguage.Romaji])}");
        ((List<ITitle>)args.Series[0].AnidbAnime.Titles).AddRange([
            new TitleStub
            {
                Value = "animeTitle1",
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Type = TitleType.Short,
                Source = DataSource.AniDB,
            },
            new TitleStub
            {
                Value = "animeTitle2",
                Language = TitleLanguage.Japanese,
                LanguageCode = "ja",
                Type = TitleType.Official,
                Source = DataSource.AniDB,
            },
            new TitleStub
            {
                Value = "animeTitle3",
                Language = TitleLanguage.Romaji,
                LanguageCode = "x-jat",
                Type = TitleType.Synonym,
                Source = DataSource.AniDB,
            },
            new TitleStub
            {
                Value = "animeTitle4",
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Type = TitleType.Main,
                Source = DataSource.AniDB,
            },
        ]);
        ((List<ITitle>)args.Episodes[0].AnidbEpisode.Titles).AddRange(new List<ITitle>
        {
            new TitleStub
            {
                Value = "episodeTitle1",
                Language = TitleLanguage.Spanish,
                LanguageCode = "es",
                Type = TitleType.None,
                Source = DataSource.AniDB,
            },
            new TitleStub
            {
                Value = "episodeTitle2",
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Type = TitleType.None,
                Source = DataSource.AniDB,
            },
            new TitleStub
            {
                Value = "episodeTitle3",
                Language = TitleLanguage.Romaji,
                LanguageCode = "x-jat",
                Type = TitleType.None,
                Source = DataSource.AniDB,
            },
        });
        var renamer = new LuaRenamer(Logmock);
        var res = renamer.GetPath(args);
        Assert.AreEqual("animeTitle4episodeTitle2episodeTitle3.mp4", res.FileName);
    }

    [TestMethod]
    public void TestLogging()
    {
        var args = MinimalArgs("log('test')");
        var logmock = new Mock<ILogger<LuaRenamer>>();
        var renamer = new LuaRenamer(logmock.Object);
        renamer.GetPath(args);

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
                {Env.filename} = (('blah  sdhow  wh '):clean_spaces())");
        var renamer = new LuaRenamer(Logmock);
        var res = renamer.GetPath(args);
        Assert.AreEqual("blah sdhow wh.mp4", res.FileName);
    }

    [TestMethod]
    public void TestLogAbstractionVersion()
    {
        Assert.AreEqual("10.0.0.0", Assembly.GetAssembly(typeof(ILogger))?.GetName().Version?.ToString());
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
        defsEnv.DoFile(Path.Combine(LuaScripts.LuaPath, "enums.lua"));
        using var sandbox = new LuaSandbox(LuaScripts.LuaLinq, LuaScripts.Utils);
        new ModelTranslator(sandbox).Translate(ModelProducers.EnvToModel(MinimalArgs(""), Logmock), sandbox.Env);
        // enums.lua really does define globals in defsEnv, so the NLua indexer works there; the sandbox side
        // has to go through GetValue, which resolves against Env.
        CompareEnums((LuaTable)defsEnv[Env.Language.Fn], (LuaTable)sandbox.GetValue(Env.Language)!);
        CompareEnums((LuaTable)defsEnv[Env.AnimeType.Fn], (LuaTable)sandbox.GetValue(Env.AnimeType)!);
        CompareEnums((LuaTable)defsEnv[Env.TitleType.Fn], (LuaTable)sandbox.GetValue(Env.TitleType)!);
        CompareEnums((LuaTable)defsEnv[Env.EpisodeType.Fn], (LuaTable)sandbox.GetValue(Env.EpisodeType)!);
        CompareEnums((LuaTable)defsEnv[Env.ImportFolderType.Fn], (LuaTable)sandbox.GetValue(Env.ImportFolderType)!);
        CompareEnums((LuaTable)defsEnv[Env.RelationType.Fn], (LuaTable)sandbox.GetValue(Env.RelationType)!);
        CompareEnums((LuaTable)defsEnv[Env.SeasonName.Fn], (LuaTable)sandbox.GetValue(Env.SeasonName)!);
    }

    [TestMethod]
    public void TestRelations()
    {
        var args = MinimalArgs(
            $"{Env.filename} = {Env.anime.relations[1].anime.preferredname} .. {Env.anime.relations[1].type} .. #{Env.anime.relations[1].anime.relations}");
        var animeMock = new Mock<IAnidbAnime>();
        animeMock.SetupGet(a => a.EpisodeCounts).Returns(new EpisodeCounts());
        animeMock.SetupGet(a => a.ID).Returns(1);
        animeMock.SetupGet(a => a.Title).Returns("blah2");
        animeMock.SetupGet(a => a.DefaultTitle).Returns(Mock.Of<ITitle>(t => t.Value == "blah"));
        animeMock.SetupGet(a => a.Titles).Returns(new List<ITitle>());
        animeMock.SetupGet(a => a.Studios).Returns([]);
        animeMock.SetupGet(a => a.RelatedSeries).Returns(new List<IRelatedMetadata<ISeries, ISeries>>
        {
            Mock.Of<IRelatedMetadata<ISeries, ISeries>>(r2 => r2.Related == args.Series[0].AnidbAnime &&
                r2.RelationType == RelationType.Prequel),
        });
        animeMock.SetupGet(a => a.ID).Returns(4);
        ((List<IRelatedMetadata<ISeries, ISeries>>)args.Series[0].AnidbAnime.RelatedSeries).Add(Mock.Of<IRelatedMetadata<ISeries, ISeries>>(r =>
            r.RelationType == RelationType.AlternativeSetting &&
            r.Related == animeMock.Object
        ));
        animeMock.SetupGet(a => a.ShokoSeries).Returns([]);
        animeMock.SetupGet(a => a.Tags).Returns([]);
        animeMock.SetupGet(a => a.YearlySeasons).Returns([]);
        var renamer = new LuaRenamer(Logmock);
        var res = renamer.GetPath(args);
        Assert.AreEqual("blah2AlternativeSetting0.mp4", res.FileName);
    }

    [TestMethod]
    public void TestApiMethods()
    {
        var renamer = new LuaRenamer(Logmock);
        var args = MinimalArgs("filename = 'blah'");
        var result = renamer.GetPath(args);
        Assert.AreEqual("blah.mp4", result.FileName);
        Assert.IsNotNull(result.ManagedFolder);
        Assert.AreEqual("shokoseriesprefname", result.Path);
    }

    [TestMethod]
    public void TestSkipping()
    {
        var renamer = new LuaRenamer(Logmock);
        var args = MinimalArgs("filename = 'blah'\nsubfolder = {'blah'}\nskip_rename = true\nskip_move = true");
        var result = renamer.GetPath(args);
        Assert.AreEqual(null, result.FileName);
        Assert.AreEqual(null, result.Path);
    }

    [TestMethod]
    public void TestLinqLog()
    {
        var args = MinimalArgs("linqSetLogLevel(3); from({'test1', 'test2'})");
        var logmock = new Mock<ILogger<LuaRenamer>>();
        var renamer = new LuaRenamer(logmock.Object);
        renamer.GetPath(args);

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
        var renamer = new LuaRenamer(Logmock);
        var args = MinimalArgs("filename = 'blah'\r\nfilename = 'argle'\nfilename = 'blargle'\rfilename = 'test'");
        var result = renamer.GetPath(args);

        Assert.AreEqual("test.mp4", result.FileName);
    }

    [TestMethod]
    public void TestDefaultScript()
    {
        var defaultScript = LuaRenamerSettings.New(Mock.Of<IConfigurationService>(), Mock.Of<IPluginManager>());
        Assert.IsNotNull(defaultScript.Script);
    }

    [TestMethod]
    [DataRow("subfolder = {'testfld'}", "testfld")]
    [DataRow("subfolder = {'testfld', 'testfld2'}", "testfld/testfld2")]
    [DataRow("subfolder = {'testfld', nil, 'testfld2'}", "testfld")]
    [DataRow("subfolder = {[2] = 'testfld', [1] = 'testfld2'}", "testfld2/testfld")]
    [DataRow("subfolder = {}", null)]
    [DataRow($$"""{{nameof(Env.replace_illegal_chars)}} = true ; subfolder = {'testfld\\', 'testfld2'}""", "testfld＼/testfld2")]
    [DataRow($$"""{{nameof(Env.replace_illegal_chars)}} = true ; subfolder = 'testfld\\testfld2/testfld3'""", "testfld＼testfld2／testfld3")]
    public void TestSubfolder(string lua, string? expected)
    {
        var args = MinimalArgs(lua);
        var renamer = new LuaRenamer(Logmock);
        var res = renamer.GetPath(args);
        Assert.AreEqual(expected?.NormPath() ?? "", res.Path?.NormPath() ?? "");
    }

    [TestMethod]
    [DataRow("filename = 'com1'", true)]
    [DataRow("filename = 'com1.test'", true)]
    [DataRow("filename = 'com\u00b9'", true)]
    [DataRow("filename = 'NUL'", true)]
    [DataRow("filename = 'CON1'", false)]
    [DataRow("filename = 'COM1test'", false)]
    [DataRow("filename = 'COM1test.test'", false)]
    [DataRow("filename = 'COM1.'", true)]
    public void TestInvalidDeviceNames(string lua, bool error)
    {
        var args = MinimalArgs(lua);
        var renamer = new LuaRenamer(Logmock);
        var res = renamer.GetPath(args);
        if (error)
            Assert.IsNotNull(res.Error);
        else
            Assert.IsNull(res.Error);
    }

    [TestMethod]
    public void InvalidLuaTest()
    {
        var args = MinimalArgs("filename = ");
        var renamer = new LuaRenamer(Logmock);
        var res = renamer.GetPath(args);
        Assert.IsNotNull(res.Error);
    }

    /// <remarks>
    /// This asserts the generator is deterministic and produces plausible output — it cannot check the
    /// generated files against the tracked ones in <c>LuaRenamer/lua/</c>, because the build's
    /// <c>GenerateLuaDefs</c> target has already overwritten those from these same models. Git is the only
    /// non-circular oracle for that, which is what the "Verify generated Lua defs are current" CI step does.
    /// </remarks>
    [TestMethod]
    public void TestLuaDocsGenerator()
    {
        var generator = new ModelDefsGenerator();
        var first = new[] { generator.GenerateDefs(), generator.GenerateEnums(), generator.GenerateEnv() };
        var second = new[] { generator.GenerateDefs(), generator.GenerateEnums(), generator.GenerateEnv() };

        for (var i = 0; i < first.Length; i++)
        {
            Assert.AreEqual(first[i], second[i], "generator output is not deterministic");
            StringAssert.StartsWith(first[i], "---@meta");
        }

        // The env is described by more than its header, and every class/enum in it is annotated.
        StringAssert.Contains(first[0], $"---@class (exact) {nameof(AnimeModel).Replace("Model", "")}");
        StringAssert.Contains(first[1], $"---@enum {nameof(EnvModel.Language)}");
        StringAssert.Contains(first[2], nameof(EnvModel.filename));
    }

    [TestMethod]
    public void TestLuaNamesGenerator()
    {
        var generator = new ModelNamesGenerator();
        Assert.AreEqual(generator.GenerateNames(), generator.GenerateNames(), "generator output is not deterministic");

        // The DSL we reference here was emitted into LuaRenamer by this same generator, so a round-trip
        // through the emitted source is the check that the two agree on the schema.
        StringAssert.Contains(generator.GenerateNames(), $"public sealed class {nameof(EnvNames)} :");
        Assert.AreEqual($"{nameof(EnvModel.anime)}.{nameof(AnimeModel.relations)}[1].{nameof(RelationModel.type)}",
            Env.anime.relations[1].type);
    }

    private static LuaSandbox TranslatedSandbox(RelocationContext<LuaRenamerSettings> args)
    {
        var sandbox = new LuaSandbox(LuaScripts.LuaLinq, LuaScripts.Utils);
        new ModelTranslator(sandbox).Translate(ModelProducers.EnvToModel(args, Logmock), sandbox.Env);
        return sandbox;
    }

    /// <summary>Gives the primary anime a title and a relation, so array paths have something to hit.</summary>
    private static void PopulateTitlesAndRelations(RelocationContext<LuaRenamerSettings> args)
    {
        var anime = args.Series[0].AnidbAnime;
        ((List<ITitle>)anime.Titles).Add(Mock.Of<ITitle>(t => t.Value == "maintitle" &&
                                                              t.Language == TitleLanguage.English &&
                                                              t.LanguageCode == "en" &&
                                                              t.Type == TitleType.Main));

        var relatedMock = new Mock<IAnidbAnime>();
        relatedMock.SetupGet(a => a.EpisodeCounts).Returns(new EpisodeCounts());
        relatedMock.SetupGet(a => a.ID).Returns(4);
        relatedMock.SetupGet(a => a.Title).Returns("relatedname");
        relatedMock.SetupGet(a => a.DefaultTitle).Returns(Mock.Of<ITitle>(t => t.Value == "relatedname"));
        relatedMock.SetupGet(a => a.Titles).Returns(new List<ITitle>());
        relatedMock.SetupGet(a => a.RelatedSeries).Returns(new List<IRelatedMetadata<ISeries, ISeries>>());
        relatedMock.SetupGet(a => a.ShokoSeries).Returns([]);
        relatedMock.SetupGet(a => a.Studios).Returns([]);
        relatedMock.SetupGet(a => a.Tags).Returns([]);
        relatedMock.SetupGet(a => a.YearlySeasons).Returns([]);
        ((List<IRelatedMetadata<ISeries, ISeries>>)anime.RelatedSeries).Add(
            Mock.Of<IRelatedMetadata<ISeries, ISeries>>(r => r.RelationType == RelationType.Prequel &&
                                                             r.Related == relatedMock.Object));
    }

    [TestMethod]
    public void TestGetValueWalksNestedTablesAndArrays()
    {
        var args = MinimalArgs($"{Env.filename} = 'resolved'");
        PopulateTitlesAndRelations(args);
        using var sandbox = TranslatedSandbox(args);

        Assert.IsNull(sandbox.GetValue(Env.filename), "output fields are absent until the script assigns them");
        sandbox.Run(args.Configuration.Script!);
        Assert.AreEqual("resolved", sandbox.GetValue(Env.filename));

        Assert.AreEqual("shokoseriesprefname", sandbox.GetValue(Env.anime.preferredname));
        Assert.AreEqual(nameof(EpisodeType.Episode), sandbox.GetValue(Env.episode.type));

        Assert.AreEqual("maintitle", sandbox.GetValue(Env.anime.titles[1].name));
        Assert.AreEqual(nameof(TitleType.Main), sandbox.GetValue(Env.anime.titles[1].type));
        Assert.AreEqual("testimport", sandbox.GetValue(Env.importfolders[1].name));
        Assert.AreEqual(nameof(RelationType.Prequel), sandbox.GetValue(Env.anime.relations[1].type));
        Assert.AreEqual("relatedname", sandbox.GetValue(Env.anime.relations[1].anime.preferredname));

        Assert.AreEqual(nameof(TitleLanguage.English), sandbox.GetValue(Env.Language[TitleLanguage.English]));
        Assert.IsInstanceOfType<LuaTable>(sandbox.GetValue(Env.importfolders));
        Assert.IsInstanceOfType<LuaTable>(sandbox.GetValue(Env.anime.relations[1].anime));

        // No long->double coercion; that lives only in NLua's Lua.this[string].
        Assert.AreEqual(3L, sandbox.GetValue(Env.anime.id));
    }

    [TestMethod]
    public void TestGetValueIsNilTolerant()
    {
        var args = MinimalArgs("");
        using var sandbox = TranslatedSandbox(args);

        Assert.IsNull(sandbox.GetValue(Env.group.name), "group is nil with no groups, so the whole path is nil");
        Assert.IsNull(sandbox.GetValue(Env.anime.titles[999].name), "index past the end of the array");
        Assert.IsNull(sandbox.GetValue(Env.importfolders[2].name), "only one import folder");
        Assert.IsNull(sandbox.GetValue("nosuchfield"), "absent top-level key");
        Assert.IsNull(sandbox.GetValue($"{Env.anime.preferredname}.nope"), "indexing through a string leaf");
        Assert.IsNull(sandbox.GetValue($"{Env.anime.preferredname}[1]"), "indexing a string leaf as an array");
    }

    [DataRow("", DisplayName = "empty path")]
    [DataRow("anime..type", DisplayName = "empty segment")]
    [DataRow(".anime", DisplayName = "leading dot")]
    [DataRow("anime.", DisplayName = "trailing dot")]
    [DataRow("anime[1", DisplayName = "unclosed bracket")]
    [DataRow("anime[x]", DisplayName = "non-numeric index")]
    [DataRow("anime[]", DisplayName = "empty index")]
    [DataRow("anime[1]x", DisplayName = "trailing junk after an index")]
    [DataRow("anime:getname(Language.English)", DisplayName = "method call")]
    [DataRow("episode_numbers(2)", DisplayName = "function call")]
    [TestMethod]
    public void TestGetValueRejectsNonValuePaths(string path)
    {
        using var sandbox = TranslatedSandbox(MinimalArgs(""));
        Assert.ThrowsExactly<ArgumentException>(() => sandbox.GetValue(path));
    }

    [TestMethod]
    public void NullCharTest()
    {
        var args = MinimalArgs("filename = 'test\\x00test'");
        var renamer = new LuaRenamer(Logmock);
        var res = renamer.GetPath(args);
        Assert.AreEqual("test_test.mp4", res.FileName);
    }

    [TestMethod]
    public void TestSeasons()
    {
        var args = MinimalArgs("filename = anime.seasons[1].year .. anime.seasons[1].season");
        var animeMock = new Mock<IAnidbAnime>();
        animeMock.SetupGet(a => a.EpisodeCounts).Returns(new EpisodeCounts());
        animeMock.SetupGet(a => a.Title).Returns("blah");
        var titleMock = Mock.Of<ITitle>(t => t.Value == "blah");
        animeMock.SetupGet(a => a.DefaultTitle).Returns(titleMock);
        animeMock.SetupGet(a => a.Titles).Returns(new List<ITitle>());
        animeMock.SetupGet(a => a.RelatedSeries).Returns(new List<IRelatedMetadata<ISeries, ISeries>>());
        animeMock.SetupGet(a => a.ID).Returns(3);
        animeMock.SetupGet(a => a.Studios).Returns([]);
        animeMock.SetupGet(a => a.Tags).Returns([]);
        animeMock.SetupGet(a => a.YearlySeasons).Returns([(2024, YearlySeason.Winter)]);
        var shokoSeries = Mock.Of<IShokoSeries>(s =>
            s.AnidbAnime == animeMock.Object &&
            s.Title == "shokoseriesprefname" &&
            s.AnidbAnimeID == 3 &&
            s.TmdbMovies == new List<ITmdbMovie>() &&
            s.TmdbShows == new List<ITmdbShow>() &&
            s.Tags == new List<IShokoTagForSeries>() &&
            s.DefaultTitle == titleMock);
        animeMock.SetupGet(a => a.ShokoSeries).Returns([shokoSeries]);
        args = new RelocationContext<LuaRenamerSettings>(new RelocationContext
        {
            AvailableFolders = args.AvailableFolders,
            File = args.File,
            Episodes = args.Episodes,
            Series = [shokoSeries],
            Groups = args.Groups,
            MoveEnabled = true,
            RenameEnabled = true,
        }, args.Configuration);

        var renamer = new LuaRenamer(Logmock);
        var res = renamer.GetPath(args);
        Assert.AreEqual("2024Winter.mp4", res.FileName);
    }

    [TestMethod]
    public void TestTmdbShowSeasons()
    {
        var args = MinimalArgs("filename = tmdb.shows[1].seasons[1].year .. tmdb.shows[1].seasons[1].season");
        var tmdbShow = new Mock<ITmdbShow>();
        tmdbShow.SetupGet(s => s.Titles).Returns(new List<ITitle>());
        tmdbShow.SetupGet(s => s.Studios).Returns([]);
        tmdbShow.SetupGet(s => s.EpisodeCounts).Returns(new EpisodeCounts());
        tmdbShow.SetupGet(s => s.YearlySeasons).Returns([(2023, YearlySeason.Spring)]);
        var titleMock = Mock.Of<ITitle>(t => t.Value == "blah");
        var shokoSeries = Mock.Of<IShokoSeries>(s =>
            s.AnidbAnime == args.Series[0].AnidbAnime &&
            s.Title == "shokoseriesprefname" &&
            s.AnidbAnimeID == 3 &&
            s.TmdbMovies == new List<ITmdbMovie>() &&
            s.TmdbShows == new List<ITmdbShow> { tmdbShow.Object } &&
            s.Tags == new List<IShokoTagForSeries>() &&
            s.DefaultTitle == titleMock);
        args = new RelocationContext<LuaRenamerSettings>(new RelocationContext
        {
            AvailableFolders = args.AvailableFolders,
            File = args.File,
            Episodes = args.Episodes,
            Series = [shokoSeries],
            Groups = args.Groups,
            MoveEnabled = true,
            RenameEnabled = true,
        }, args.Configuration);

        var renamer = new LuaRenamer(Logmock);
        var res = renamer.GetPath(args);
        Assert.AreEqual("2023Spring.mp4", res.FileName);
    }

    #region Multi-series primary resolution

    // Shoko series ids and AniDB anime ids are disjoint here on purpose: comparing one space against
    // the other can then never match by coincidence, which is what makes the ordering bugs observable.
    // (MinimalArgs leaves IShokoSeries.ID at its default 0, so those mix-ups go unnoticed there.)
    private const int PrimaryAnidbId = 10;
    private const int PrimaryShokoId = 100;
    private const int OtherAnidbId = 20;
    private const int OtherShokoId = 200;

    private static IShokoSeries SeriesMock(int anidbId, int shokoId, string shokoTitle, string anidbTitle,
        IReadOnlyList<ITmdbShow> tmdbShows)
    {
        var animeMock = new Mock<IAnidbAnime>();
        animeMock.SetupGet(a => a.EpisodeCounts).Returns(new EpisodeCounts());
        animeMock.SetupGet(a => a.ID).Returns(anidbId);
        animeMock.SetupGet(a => a.Title).Returns(anidbTitle);
        animeMock.SetupGet(a => a.DefaultTitle).Returns(Mock.Of<ITitle>(t => t.Value == anidbTitle));
        animeMock.SetupGet(a => a.Titles).Returns(new List<ITitle>());
        animeMock.SetupGet(a => a.RelatedSeries).Returns(new List<IRelatedMetadata<ISeries, ISeries>>());
        animeMock.SetupGet(a => a.Studios).Returns([]);
        animeMock.SetupGet(a => a.Tags).Returns([]);
        animeMock.SetupGet(a => a.YearlySeasons).Returns([]);
        var series = Mock.Of<IShokoSeries>(s =>
            s.ID == shokoId &&
            s.AnidbAnimeID == anidbId &&
            s.AnidbAnime == animeMock.Object &&
            s.Title == shokoTitle &&
            s.TmdbMovies == new List<ITmdbMovie>() &&
            s.TmdbShows == tmdbShows &&
            s.Tags == new List<IShokoTagForSeries>() &&
            s.DefaultTitle == Mock.Of<ITitle>(t => t.Value == anidbTitle));
        animeMock.SetupGet(a => a.ShokoSeries).Returns([series]);
        return series;
    }

    private static IShokoGroup GroupMock(string name, IShokoSeries mainSeries) =>
        Mock.Of<IShokoGroup>(g =>
            g.PreferredTitle == Mock.Of<ITitle>(t => t.Value == name) &&
            g.MainSeriesID == mainSeries.ID &&
            g.MainSeries == mainSeries &&
            g.AllSeries == new List<IShokoSeries> { mainSeries });

    /// <summary>
    /// A file linked to two series, where <c>Series[0]</c> is deliberately NOT the primary one (the
    /// primary is the lowest AniDB anime id). Everything downstream — the env model, the default
    /// subfolder — must re-derive the primary series rather than trust this order.
    /// </summary>
    private static RelocationContext<LuaRenamerSettings> MultiSeriesArgs(string script, string primaryShokoTitle = "primaryShoko",
        IReadOnlyList<ITmdbShow>? primaryTmdbShows = null)
    {
        var importFolder = Mock.Of<IManagedFolder>(i => i.Path == Path.Combine("C:", "testimportfolder") &&
            i.DropFolderType == DropFolderType.Destination &&
            i.Name == "testimport");
        var primary = SeriesMock(PrimaryAnidbId, PrimaryShokoId, primaryShokoTitle, "primaryAnidb", primaryTmdbShows ?? []);
        var other = SeriesMock(OtherAnidbId, OtherShokoId, "otherShoko", "otherAnidb", []);
        return new RelocationContext<LuaRenamerSettings>(new RelocationContext
        {
            CancellationToken = CancellationToken.None,
            AvailableFolders = new List<IManagedFolder> { importFolder },
            File = Mock.Of<IVideoFile>(file =>
                file.Path == Path.Combine("C:", "testimportfolder", "testsubfolder", "testfilename.mp4") &&
                file.RelativePath == Path.Combine("testsubfolder", "testfilename.mp4") &&
                file.FileName == "testfilename.mp4" &&
                file.ManagedFolderID == importFolder.ID &&
                file.ManagedFolder == importFolder &&
                file.VideoID == 25 &&
                file.Video == Mock.Of<IVideo>(vi => vi.ED2K == "abc123" && vi.Hashes == new List<IHashDigest>())),
            Episodes = new List<IShokoEpisode>
            {
                Mock.Of<IShokoEpisode>(se =>
                    se.SeriesID == PrimaryShokoId &&
                    se.AnidbEpisode == Mock.Of<IAnidbEpisode>(e => e.SeriesID == PrimaryAnidbId &&
                        e.Titles == new List<ITitle>() &&
                        e.Type == EpisodeType.Episode) &&
                    se.TmdbEpisodes == new List<ITmdbEpisode>()),
            },
            Series = new List<IShokoSeries> { other, primary },
            // Likewise not primary-first, and the two groups' main series differ, so both the
            // "contains the primary series" rule and its id space are exercised.
            Groups = new List<IShokoGroup> { GroupMock("otherGroup", other), GroupMock("primaryGroup", primary) },
            RenameEnabled = true,
            MoveEnabled = true,
        }, new LuaRenamerSettings { Script = script });
    }

    [TestMethod]
    public void TestGroupOrderFollowsPrimarySeries()
    {
        var args = MultiSeriesArgs($"{Env.filename} = {Env.group.name}");
        var renamer = new LuaRenamer(Logmock);
        var res = renamer.GetPath(args);
        Assert.AreEqual("primaryGroup.mp4", res.FileName);
    }

    [TestMethod]
    public void TestTmdbComesFromPrimarySeries()
    {
        var tmdbShow = new Mock<ITmdbShow>();
        tmdbShow.SetupGet(s => s.ID).Returns(555);
        tmdbShow.SetupGet(s => s.Titles).Returns(new List<ITitle>());
        tmdbShow.SetupGet(s => s.Studios).Returns([]);
        tmdbShow.SetupGet(s => s.EpisodeCounts).Returns(new EpisodeCounts());
        tmdbShow.SetupGet(s => s.YearlySeasons).Returns([]);
        // Only the primary series is linked to the show; sourcing tmdb off Series[0] yields an empty list.
        var args = MultiSeriesArgs($"{Env.filename} = tostring({Env.tmdb.shows[1].id})", primaryTmdbShows: [tmdbShow.Object]);
        var renamer = new LuaRenamer(Logmock);
        var res = renamer.GetPath(args);
        Assert.AreEqual("555.mp4", res.FileName);
    }

    [TestMethod]
    public void TestDefaultSubfolderUsesPrimarySeries()
    {
        var args = MultiSeriesArgs($"{Env.filename} = 'blah'");
        var renamer = new LuaRenamer(Logmock);
        var res = renamer.GetPath(args);
        Assert.IsNull(res.Error);
        Assert.AreEqual("primaryShoko", res.Path);
    }

    [TestMethod]
    public void TestDefaultSubfolderFallsBackToAnidbTitle()
    {
        // A blank Shoko title used to reach FilePathCleaner verbatim and fail the whole relocation;
        // it must fall back the same way anime.preferredname does.
        var args = MultiSeriesArgs($"{Env.filename} = 'blah'", primaryShokoTitle: "   ");
        var renamer = new LuaRenamer(Logmock);
        var res = renamer.GetPath(args);
        Assert.IsNull(res.Error);
        Assert.AreEqual("primaryAnidb", res.Path);
    }

    #endregion
}
