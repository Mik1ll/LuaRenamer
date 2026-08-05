using System;
using System.Collections.Generic;
using System.Linq;
using LuaRenamer.LuaEnv;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLua;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Enums;

namespace LuaRenamer.Tests;

/// <summary>
/// Runtime proof for the <see cref="EnvModel"/> root and the rest of the ported schema (the env-only
/// concepts the Anime slice didn't cover): free functions (CLR delegates), the bound getname callables
/// (<see cref="AnimeGetName"/>/<see cref="TitleGetName"/>), user-written Output fields (incl. <c>LuaUnion</c>
/// ones, which must stay absent), and the enum
/// tables modeled as <c>IReadOnlyDictionary&lt;TEnum, TEnum&gt;</c>. Generator byte-equality proves the
/// schema's *shape*; this proves the serialized env is actually Lua-consumable.
/// </summary>
[TestClass]
public class EnvSerializerTests
{
    private LuaSandbox _lua = null!;
    private LuaSerializer _serializer = null!;

    [TestInitialize]
    public void Init()
    {
        // The serializer compiles getname against a real sandbox env, so it needs the trusted chunks loaded.
        _lua = new LuaSandbox(LuaScripts.LuaLinq, LuaScripts.Utils);
        _serializer = new LuaSerializer(_lua);
    }

    [TestCleanup]
    public void Cleanup() => _lua.Dispose();

    private static IReadOnlyList<TitleModel> Titles() =>
    [
        new TitleModel { name = "Eng", language = TitleLanguage.English, languagecode = "en", type = TitleType.Main },
        new TitleModel { name = "Jap", language = TitleLanguage.Japanese, languagecode = "ja", type = TitleType.Official },
    ];

    private static DateTimeModel Date(long y) => new()
    {
        year = y, month = 1, day = 2, yday = 2, wday = 5, hour = 0, min = 0, sec = 0, isdst = false,
    };

    private AnimeModel Anime(long id) => new()
    {
        getname = new AnimeGetName(),
        airdate = Date(2020),
        enddate = null,
        rating = 8.5,
        restricted = false,
        type = AnimeType.Movie,
        preferredname = "Pref",
        defaultname = "Def",
        id = id,
        titles = Titles(),
        episodecounts = new Dictionary<EpisodeType, long> { [EpisodeType.Episode] = 12 },
        relations = [],
        studios = ["Studio A"],
        tags = ["tag1"],
        customtags = [],
        seasons = [new SeasonModel { year = 2020, season = YearlySeason.Winter }],
    };

    private EpisodeModel Episode(long id) => new()
    {
        getname = new TitleGetName(),
        duration = 1440,
        number = 1,
        type = EpisodeType.Episode,
        airdate = Date(2020),
        animeid = 42,
        id = id,
        titles = Titles(),
        prefix = "",
    };

    private ImportFolderModel Folder(long id) => new()
    {
        id = id, name = "Import", location = "/data", type = Enum.GetValues<DropFolderType>().First(),
    };

    private FileModel File() => new()
    {
        name = "video", extension = ".mkv", path = "/data/video.mkv", size = 123456789,
        importfolder = Folder(1),
        earliestname = "video.orig",
        media = new MediaModel
        {
            chaptered = true, duration = 1440, bitrate = 5_000_000,
            sublanguages = ["English"],
            audio =
            [
                new AudioModel
                {
                    compressionmode = "Lossy", channels = 5.1, samplingrate = 48000,
                    codec = "AAC", language = "Japanese", title = null,
                },
            ],
            video = new VideoModel
            {
                height = 1080, width = 1920, codec = "h264", res = "1080p",
                bitrate = 4_000_000, bitdepth = 8, framerate = 23.976,
            },
        },
        anidb = new AniDbModel
        {
            id = 555, censored = false, source = "BD", version = 2,
            releasedate = Date(2021), description = "notes",
            releasegroup = new ReleaseGroupModel { name = "Group", shortname = "GRP" },
            media = new AniDbMediaModel
            {
                sublanguages = [TitleLanguage.English], dublanguages = [TitleLanguage.Japanese],
            },
        },
        hashes = new HashesModel { crc = "ABCD1234", md5 = null, ed2k = "ed2khash", sha1 = null },
    };

    private TmdbModel Tmdb() => new()
    {
        movies =
        [
            new TmdbMovieModel
            {
                getname = new TitleGetName(), id = 1, titles = Titles(), defaultname = "MovieDef",
                preferredname = "MoviePref", rating = 7.0, restricted = false, studios = ["Studio X"],
                airdate = Date(2019),
            },
        ],
        shows =
        [
            new TmdbShowModel
            {
                getname = new TitleGetName(), id = 2, titles = Titles(), defaultname = "ShowDef",
                preferredname = "ShowPref", rating = 9.0, restricted = false, studios = ["Studio Y"],
                episodecount = 24, airdate = Date(2018), enddate = Date(2019),
                seasons = [new SeasonModel { year = 2018, season = YearlySeason.Fall }],
            },
        ],
        episodes =
        [
            new TmdbEpisodeModel
            {
                getname = new TitleGetName(), id = 3, showid = 2, titles = Titles(), defaultname = "EpDef",
                preferredname = "EpPref", type = EpisodeType.Episode, number = 1, seasonnumber = 1,
                airdate = Date(2018),
            },
        ],
    };

    private readonly List<string> _logged = [];

    private EnvModel BuildEnv() => new()
    {
        episode_numbers = (EpisodeNumbersDelegate)(pad => $"E{pad}"),
        logdebug = (LogDelegate)(m => _logged.Add("D:" + m)),
        log = (LogDelegate)(m => _logged.Add("I:" + m)),
        logwarn = (LogDelegate)(m => _logged.Add("W:" + m)),
        logerror = (LogDelegate)(m => _logged.Add("E:" + m)),
        file = File(),
        anime = Anime(42),
        animes = [Anime(42), Anime(99)],
        episode = Episode(7),
        episodes = [Episode(7)],
        importfolders = [Folder(1), Folder(2)],
        group = new GroupModel { name = "Grp", mainanime = Anime(42), animes = [Anime(42)] },
        groups = [new GroupModel { name = "Grp", mainanime = Anime(42), animes = [Anime(42)] }],
        tmdb = Tmdb(),
        // Output fields left unset: the user script writes these. They must serialize as absent.
        use_existing_anime_location = true,
        replace_illegal_chars = true,
        remove_illegal_chars = false,
        skip_rename = false,
        skip_move = false,
        illegal_chars_map = new Dictionary<string, string> { ["<"] = "(" },
        ImportFolderType = ModelProducers.EnumTable<DropFolderType>(),
        AnimeType = ModelProducers.EnumTable<AnimeType>(),
        EpisodeType = ModelProducers.EnumTable<EpisodeType>(),
        TitleType = ModelProducers.EnumTable<TitleType>(),
        Language = ModelProducers.EnumTable<TitleLanguage>(),
        RelationType = ModelProducers.EnumTable<RelationType>(),
        SeasonName = ModelProducers.EnumTable<YearlySeason>(),
    };

    private void Load() => _lua["env"] = _serializer.Serialize(BuildEnv());

    [TestMethod]
    public void NestedGraph_Is_Navigable()
    {
        Load();
        Assert.AreEqual("video", _lua.DoString("return env.file.name")[0]);
        Assert.AreEqual("/data", _lua.DoString("return env.file.importfolder.location")[0]);
        Assert.AreEqual("h264", _lua.DoString("return env.file.media.video.codec")[0]);
        Assert.AreEqual(5.1, _lua.DoString("return env.file.media.audio[1].channels")[0]);
        Assert.AreEqual("ed2khash", _lua.DoString("return env.file.hashes.ed2k")[0]);
        Assert.AreEqual(true, _lua.DoString("return env.file.hashes.md5 == nil")[0]); // null leaf absent
        Assert.AreEqual("GRP", _lua.DoString("return env.file.anidb.releasegroup.shortname")[0]);
        Assert.AreEqual("English", _lua.DoString("return env.file.anidb.media.sublanguages[1]")[0]); // enum element -> name

        Assert.AreEqual(42L, _lua.DoString("return env.anime.id")[0]);
        Assert.AreEqual(2L, _lua.DoString("return #env.animes")[0]);
        Assert.AreEqual(99L, _lua.DoString("return env.animes[2].id")[0]);
        Assert.AreEqual(1L, _lua.DoString("return env.episode.number")[0]);
        Assert.AreEqual("Grp", _lua.DoString("return env.group.name")[0]);
        Assert.AreEqual("MoviePref", _lua.DoString("return env.tmdb.movies[1].preferredname")[0]);
        Assert.AreEqual("Fall", _lua.DoString("return env.tmdb.shows[1].seasons[1].season")[0]);
    }

    [TestMethod]
    public void Getname_Works_On_Multiple_Models()
    {
        Load();
        Assert.AreEqual("Eng", _lua.DoString("return env.anime:getname('English')")[0]);
        Assert.AreEqual("Jap", _lua.DoString("return env.episode:getname('Japanese')")[0]);
        Assert.AreEqual("Eng", _lua.DoString("return env.tmdb.shows[1]:getname('English')")[0]);
    }

    [TestMethod]
    public void FreeFunctions_Are_Callable_Clr_Delegates()
    {
        Load();
        Assert.AreEqual("E3", _lua.DoString("return env.episode_numbers(3)")[0]); // '.' plain-call syntax
        _lua.DoString("env.log('hello'); env.logerror('boom')");
        CollectionAssert.AreEqual(new[] { "I:hello", "E:boom" }, _logged);
    }

    [TestMethod]
    public void OutputFields_Are_Absent_Until_Script_Writes_Them()
    {
        Load();
        Assert.AreEqual(true, _lua.DoString("return env.filename == nil")[0]);
        Assert.AreEqual(true, _lua.DoString("return env.destination == nil")[0]);   // LuaUnion, never produced
        Assert.AreEqual(true, _lua.DoString("return env.subfolder == nil")[0]);     // LuaUnion, never produced
        // bool outputs ARE produced (host defaults), incl. false ones
        Assert.AreEqual(true, _lua.DoString("return env.use_existing_anime_location")[0]);
        Assert.AreEqual(false, _lua.DoString("return env.skip_rename")[0]);
        Assert.AreEqual("(", _lua.DoString("return env.illegal_chars_map['<']")[0]);
    }

    [TestMethod]
    public void EnumTables_Marshal_To_Identity_Name_Maps()
    {
        Load();
        // matching key/value enum -> { Name = "Name" }; round-trip a known member of each enum
        AssertEnumTable<DropFolderType>("ImportFolderType");
        AssertEnumTable<AnimeType>("AnimeType");
        AssertEnumTable<EpisodeType>("EpisodeType");
        AssertEnumTable<TitleType>("TitleType");
        AssertEnumTable<TitleLanguage>("Language");
        AssertEnumTable<RelationType>("RelationType");
        AssertEnumTable<YearlySeason>("SeasonName");
    }

    private void AssertEnumTable<T>(string luaName) where T : struct, Enum
    {
        foreach (var name in Enum.GetValues<T>().Distinct().Select(v => Enum.GetName(v)!))
            Assert.AreEqual(name, _lua.DoString($"return env.{luaName}['{name}']")[0], $"{luaName}.{name}");
    }
}
