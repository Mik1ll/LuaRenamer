using System;
using System.Collections.Generic;
using LuaRenamer.LuaEnv;
using LuaRenamer.LuaEnv.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NLua;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Stub;

namespace LuaRenamer.Tests;

/// <summary>
/// Runtime proof for the model env architecture: a plain <see cref="ILuaModel"/>
/// graph materialized by <see cref="ModelTranslator"/> must produce a <see cref="LuaTable"/> that is
/// byte-for-behavior consumable from a real Lua VM — i.e. an equivalent replacement for the
/// write-through <c>*Table</c> builders. Every marshaling rule is exercised: scalars, enum→name,
/// nested model→subtable, null→absent, list→1-based sequence, enum-keyed dict→map, and the
/// <c>getname</c> callable (a Lua function handle marshaled as-is).
/// </summary>
[TestClass]
public class ModelTranslatorTests
{
    private LuaSandbox _lua = null!;
    private ModelTranslator _translator = null!;

    [TestInitialize]
    public void Init()
    {
        // The translator compiles getname against a real sandbox env, so it needs the trusted chunks loaded.
        _lua = new LuaSandbox(LuaScripts.LuaLinq, LuaScripts.Utils);
        _translator = new ModelTranslator(_lua);
    }

    [TestCleanup]
    public void Cleanup() => _lua.Dispose();

    private static AnimeModel MakeAnime(IReadOnlyList<RelationModel> relations) => new()
    {
        airdate = new DateTimeModel
        {
            year = 2020,
            month = 1,
            day = 2,
            yday = 2,
            wday = 5,
            hour = 0,
            min = 0,
            sec = 0,
            isdst = false,
        },
        enddate = null, // null => key absent (Lua nil)
        rating = 8.5,
        restricted = false,
        type = AnimeType.Movie,
        preferredname = "Pref",
        defaultname = "Def",
        id = 42,
        titles =
        [
            new TitleModel { name = "Eng Title", language = TitleLanguage.English, languagecode = "en", type = TitleType.Main },
            new TitleModel { name = "Jap Title", language = TitleLanguage.Japanese, languagecode = "ja", type = TitleType.Official },
        ],
        episodecounts = new Dictionary<EpisodeType, long> { [EpisodeType.Episode] = 12, [EpisodeType.Special] = 2 },
        relations = relations,
        studios = ["Studio A", "Studio B"],
        tags = ["tag1"],
        customtags = [],
        seasons = [new SeasonModel { year = 2020, season = YearlySeason.Winter }],
    };

    private static AnimeModel FullAnime()
    {
        AnimeModel related = MakeAnime([]); // leaf: no further relations
        return MakeAnime([new RelationModel { anime = related, type = RelationType.Sequel }]);
    }

    [TestMethod]
    public void Scalars_And_Enums_Marshal()
    {
        LuaTable table = _translator.Translate(FullAnime());

        Assert.AreEqual(42L, table["id"]);          // long stays long
        Assert.AreEqual(8.5, table["rating"]);      // double stays double
        Assert.AreEqual(false, table["restricted"]); // bool false still written (not treated as absent)
        Assert.AreEqual("Pref", table["preferredname"]);
        Assert.AreEqual("Movie", table["type"]);    // enum -> its name, the one marshaling site for enums
    }

    [TestMethod]
    public void Null_Becomes_Absent()
    {
        LuaTable table = _translator.Translate(FullAnime());

        Assert.IsNull(table["enddate"]); // C# side: key never set
        _lua["anime"] = table;
        Assert.AreEqual(true, _lua.DoString("return anime.enddate == nil")[0]); // Lua side: nil
        Assert.IsNotNull(table["airdate"]); // the non-null sibling IS present
    }

    [TestMethod]
    public void NestedModel_Becomes_Subtable()
    {
        LuaTable table = _translator.Translate(FullAnime());

        var airdate = (LuaTable)table["airdate"];
        Assert.AreEqual(2020L, airdate["year"]);
        Assert.AreEqual(2L, airdate["day"]);
        Assert.AreEqual(false, airdate["isdst"]);
    }

    [TestMethod]
    public void Lists_Are_OneBased_Sequences()
    {
        _lua["anime"] = _translator.Translate(FullAnime());

        Assert.AreEqual(2L, _lua.DoString("return #anime.titles")[0]);
        Assert.AreEqual("Eng Title", _lua.DoString("return anime.titles[1].name")[0]); // 1-based, nested model navigable
        Assert.AreEqual("Japanese", _lua.DoString("return anime.titles[2].language")[0]); // element enum -> name
        Assert.AreEqual("Studio A", _lua.DoString("return anime.studios[1]")[0]);       // scalar element
        Assert.AreEqual(0L, _lua.DoString("return #anime.customtags")[0]);              // empty list -> empty table
    }

    [TestMethod]
    public void EnumKeyedDictionary_Becomes_Map()
    {
        _lua["anime"] = _translator.Translate(FullAnime());

        // enum key -> its name; value preserved
        Assert.AreEqual(12L, _lua.DoString("return anime.episodecounts.Episode")[0]);
        Assert.AreEqual(2L, _lua.DoString("return anime.episodecounts.Special")[0]);
    }

    [TestMethod]
    public void Relations_Recurse()
    {
        _lua["anime"] = _translator.Translate(FullAnime());

        Assert.AreEqual("Sequel", _lua.DoString("return anime.relations[1].type")[0]);
        Assert.AreEqual(42L, _lua.DoString("return anime.relations[1].anime.id")[0]);
        Assert.AreEqual(0L, _lua.DoString("return #anime.relations[1].anime.relations")[0]); // leaf has none
    }

    [TestMethod]
    public void GetName_Callable_With_Method_Syntax()
    {
        // The production-faithful case: getname is a Lua function handle (GetName), called as
        // anime:getname(lang) with implicit self. Enum arg arrives as the Lua string "English" and
        // matches the title.language slot (also stored as the name "English").
        _lua["anime"] = _translator.Translate(FullAnime());

        Assert.AreEqual("Eng Title", _lua.DoString("return anime:getname('English')")[0]);
        Assert.AreEqual("Jap Title", _lua.DoString("return anime:getname('Japanese')")[0]);
        Assert.IsNull(_lua.DoString("return anime:getname('Romaji')")[0]); // no match -> nil
    }

    [TestMethod]
    public void GetName_Is_Materialized_As_A_Single_Shared_Handle()
    {
        // getname is a pure descriptor; the translator resolves it out of the sandbox's function library on
        // demand, memoized, so every table that binds the same entry gets the one handle (no per-table closures).
        LuaTable a1 = _translator.Translate(FullAnime());
        LuaTable a2 = _translator.Translate(FullAnime());

        var stored = a1["getname"];
        _ = Assert.IsInstanceOfType<LuaFunction>(stored);
        Assert.IsTrue(((LuaFunction)stored).Equals(a2["getname"])); // same cached handle across translations
    }

    [TestMethod]
    [DataRow(TitleType.Main, true)]
    [DataRow(TitleType.Official, true)]
    [DataRow(TitleType.None, true)]
    [DataRow(TitleType.Synonym, false)] // unofficial: only with the opt-in flag
    [DataRow(TitleType.Short, false)]   // absent from the priority table entirely
    public void GetName_Title_Priority_Policy(TitleType type, bool officialSurface)
    {
        // The getname source names TitleType members and Title/Anime fields as plain Lua strings, so nothing
        // in C# fails to compile if one is renamed. This is the guard that catches it instead.
        AnimeModel anime = MakeAnime([]) with
        {
            titles = [new TitleModel { name = "Only", language = TitleLanguage.English, languagecode = "en", type = type }],
        };
        _lua["anime"] = _translator.Translate(anime);

        Assert.AreEqual(officialSurface ? "Only" : null, _lua.DoString("return anime:getname('English')")[0]);
        Assert.AreEqual(type == TitleType.Short ? null : "Only", _lua.DoString("return anime:getname('English', true)")[0]);
    }

    // ---- Producer side: IAnidbAnime -> AnimeModel -> LuaTable ----

    private static Mock<IAnidbAnime> MinAnime(int id, string anidbTitle, string anidbDefault)
    {
        var m = new Mock<IAnidbAnime>();
        _ = m.SetupGet(a => a.EpisodeCounts).Returns(new EpisodeCounts());
        _ = m.SetupGet(a => a.ID).Returns(id);
        _ = m.SetupGet(a => a.Title).Returns(anidbTitle);
        _ = m.SetupGet(a => a.DefaultTitle).Returns(Mock.Of<ITitle>(t => t.Value == anidbDefault));
        _ = m.SetupGet(a => a.Titles).Returns(new List<ITitle>());
        _ = m.SetupGet(a => a.Studios).Returns([]);
        _ = m.SetupGet(a => a.Tags).Returns([]);
        _ = m.SetupGet(a => a.YearlySeasons).Returns([]);
        _ = m.SetupGet(a => a.RelatedSeries).Returns([]);
        _ = m.SetupGet(a => a.ShokoSeries).Returns([]);
        return m;
    }

    private static TitleStub Title(string value, TitleLanguage lang, string code, TitleType type) =>
        new() { Value = value, Language = lang, LanguageCode = code, Type = type, Source = DataSource.AniDB };

    [TestMethod]
    public void Producer_Maps_AnidbAnime_To_LuaConsumable_Table()
    {
        // A related anime with NO ShokoSeries — so preferredname/defaultname fall back to AniDB,
        // and (since it's reached through a relation) its own relations are pruned.
        IAnidbAnime related = MinAnime(99, "Related", "RelatedDef").Object;

        Mock<IAnidbAnime> anime = MinAnime(42, "AnidbTitle", "AnidbDefault");
        _ = anime.SetupGet(a => a.Type).Returns(AnimeType.Movie);
        _ = anime.SetupGet(a => a.Rating).Returns(8.5);
        _ = anime.SetupGet(a => a.Restricted).Returns(true);
        _ = anime.SetupGet(a => a.AirDate).Returns(new PartialDateOnly(new DateTime(2020, 1, 2)));
        _ = anime.SetupGet(a => a.Titles).Returns(new List<ITitle>
        {
            Title("Zebra", TitleLanguage.English, "en", TitleType.Synonym),
            Title("Apple", TitleLanguage.Japanese, "ja", TitleType.Official),
        });
        _ = anime.SetupGet(a => a.Studios).Returns([Mock.Of<IStudio>(s => s.Name == "Studio A"), Mock.Of<IStudio>(s => s.Name == "Studio B")]);
        _ = anime.SetupGet(a => a.Tags).Returns([Mock.Of<IAnidbTagForAnime>(t => t.Name == "action")]);
        _ = anime.SetupGet(a => a.YearlySeasons).Returns([(2020, YearlySeason.Winter)]);
        _ = anime.SetupGet(a => a.RelatedSeries).Returns(
            [Mock.Of<IRelatedMetadata<ISeries, ISeries>>(r => r.Related == related && r.RelationType == RelationType.Sequel)]);
        // ShokoSeries present => preferredname/defaultname/customtags come from the Shoko series.
        _ = anime.SetupGet(a => a.ShokoSeries).Returns(
            [
                Mock.Of<IShokoSeries>(s => s.Title == "ShokoPref" &&
                    s.DefaultTitle == Mock.Of<ITitle>(t => t.Value == "ShokoDef") &&
                    s.Tags == new List<IShokoTagForSeries> { Mock.Of<IShokoTagForSeries>(t => t.Name == "custom1") }),
            ]);

        AnimeModel model = ModelProducers.AnimeToModel(anime.Object);
        _lua["anime"] = _translator.Translate(model);

        // scalars / enum / Shoko-vs-AniDB name precedence
        Assert.AreEqual(42L, _lua.DoString("return anime.id")[0]);
        Assert.AreEqual("Movie", _lua.DoString("return anime.type")[0]);
        Assert.AreEqual(8.5, _lua.DoString("return anime.rating")[0]);
        Assert.AreEqual(true, _lua.DoString("return anime.restricted")[0]);
        Assert.AreEqual("ShokoPref", _lua.DoString("return anime.preferredname")[0]);
        Assert.AreEqual("ShokoDef", _lua.DoString("return anime.defaultname")[0]);

        // titles ordered by Value (Apple before Zebra); element enum -> name
        Assert.AreEqual(2L, _lua.DoString("return #anime.titles")[0]);
        Assert.AreEqual("Apple", _lua.DoString("return anime.titles[1].name")[0]);
        Assert.AreEqual("Zebra", _lua.DoString("return anime.titles[2].name")[0]);
        Assert.AreEqual("English", _lua.DoString("return anime.titles[2].language")[0]);

        // scalar lists from Shoko collections
        Assert.AreEqual("Studio A", _lua.DoString("return anime.studios[1]")[0]);
        Assert.AreEqual("action", _lua.DoString("return anime.tags[1]")[0]);
        Assert.AreEqual("custom1", _lua.DoString("return anime.customtags[1]")[0]); // from series.Tags

        // seasons + date mapping from PartialDateOnly; absent end date
        Assert.AreEqual(2020L, _lua.DoString("return anime.seasons[1].year")[0]);
        Assert.AreEqual("Winter", _lua.DoString("return anime.seasons[1].season")[0]);
        Assert.AreEqual(2020L, _lua.DoString("return anime.airdate.year")[0]);
        Assert.AreEqual(1L, _lua.DoString("return anime.airdate.month")[0]);
        Assert.AreEqual(2L, _lua.DoString("return anime.airdate.day")[0]);
        Assert.AreEqual(true, _lua.DoString("return anime.enddate == nil")[0]);

        // episodecounts spans every EpisodeType (default counts are 0)
        Assert.AreEqual(0L, _lua.DoString("return anime.episodecounts.Episode")[0]);

        // relation recursion + pruning of the nested anime's relations; AniDB-name fallback
        Assert.AreEqual("Sequel", _lua.DoString("return anime.relations[1].type")[0]);
        Assert.AreEqual("Related", _lua.DoString("return anime.relations[1].anime.preferredname")[0]);
        Assert.AreEqual(0L, _lua.DoString("return #anime.relations[1].anime.relations")[0]);

        // getname works against the produced titles. The only English title is a Synonym, so the real
        // (lualinq priority) resolver returns it only when include_unofficial is passed.
        Assert.IsNull(_lua.DoString("return anime:getname('English')")[0]);
        Assert.AreEqual("Zebra", _lua.DoString("return anime:getname('English', true)")[0]);
    }
}
