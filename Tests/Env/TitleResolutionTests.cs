using System;
using System.Collections.Generic;
using AwesomeAssertions;
using LuaRenamer.LuaEnv;
using LuaRenamer.LuaEnv.Models;
using NLua;
using Shoko.Abstractions.Metadata.Enums;
using Xunit;

namespace LuaRenamer.Tests.Env;

/// <summary>
/// <c>getname</c>'s priority policy, asserted at the marshaling layer against a hand-built model. The policy
/// lives in a Lua source that names <c>TitleType</c> members and model fields as plain strings, so nothing in
/// C# stops compiling if one of them is renamed — this is what catches it instead.
/// </summary>
public sealed class TitleResolutionTests : IDisposable
{
    private readonly LuaSandbox _sandbox = new(LuaScripts.LuaLinq, LuaScripts.Utils);

    public void Dispose() => _sandbox.Dispose();

    private static AnimeModel AnimeWith(IReadOnlyList<TitleModel> titles) => new()
    {
        airdate = null,
        enddate = null,
        rating = 8.5,
        restricted = false,
        type = AnimeType.Movie,
        preferredname = "Pref",
        defaultname = "Def",
        id = 42,
        titles = titles,
        episodecounts = new Dictionary<EpisodeType, long>(),
        relations = [],
        studios = [],
        tags = [],
        customtags = [],
        seasons = [],
    };

    private static TitleModel Title(string name, TitleLanguage language, TitleType type) =>
        new() { name = name, language = language, languagecode = "x", type = type };

    private object? Resolve(AnimeModel anime, string call)
    {
        _sandbox.Env["anime"] = new ModelTranslator(_sandbox).Translate(anime);
        _sandbox.Run($"resolved = {call}").Should().BeNull();
        return _sandbox.GetValue("resolved");
    }

    [Theory]
    [InlineData(TitleType.Main, true)]
    [InlineData(TitleType.Official, true)]
    [InlineData(TitleType.None, true)]
    [InlineData(TitleType.Synonym, false)] // unofficial: reachable only with the opt-in flag
    [InlineData(TitleType.Short, false)]   // outside the priority table entirely, so never reachable
    public void OnlyOfficialTypesResolveWithoutTheUnofficialOptIn(TitleType type, bool officialSurface)
    {
        AnimeModel anime = AnimeWith([Title("Only", TitleLanguage.English, type)]);

        Resolve(anime, "anime:getname('English')").Should().Be(officialSurface ? "Only" : null);
        Resolve(anime, "anime:getname('English', true)").Should().Be(type == TitleType.Short ? null : "Only");
    }

    [Fact]
    public void MainOutranksOfficialWhichOutranksNone()
    {
        AnimeModel anime = AnimeWith(
        [
            Title("none", TitleLanguage.English, TitleType.None),
            Title("official", TitleLanguage.English, TitleType.Official),
            Title("main", TitleLanguage.English, TitleType.Main),
        ]);

        Resolve(anime, "anime:getname('English')").Should().Be("main");
    }

    [Fact]
    public void UnofficialRanksBelowEveryOfficialType()
    {
        AnimeModel anime = AnimeWith(
        [
            Title("synonym", TitleLanguage.English, TitleType.Synonym),
            Title("none", TitleLanguage.English, TitleType.None),
        ]);

        Resolve(anime, "anime:getname('English', true)").Should().Be("none");
    }

    [Fact]
    public void ResolutionIsScopedToTheRequestedLanguage()
    {
        AnimeModel anime = AnimeWith(
        [
            Title("english", TitleLanguage.English, TitleType.Main),
            Title("japanese", TitleLanguage.Japanese, TitleType.Main),
        ]);

        Resolve(anime, "anime:getname('Japanese')").Should().Be("japanese");
        Resolve(anime, "anime:getname('Romaji')").Should().BeNull();
    }

    [Fact]
    public void TheCallableIsOneSharedHandleAcrossNodes()
    {
        // getname is static on the model, and the sandbox memoizes by source, so every node that binds it
        // gets the same compiled handle rather than a closure per table.
        var translator = new ModelTranslator(_sandbox);
        LuaTable first = translator.Translate(AnimeWith([]));
        LuaTable second = translator.Translate(AnimeWith([]));

        first["getname"].Should().BeOfType<LuaFunction>().And.Be(second["getname"]);
    }
}
