using System;
using System.Linq;
using AwesomeAssertions;
using LuaRenamer.LuaEnv.Models;
using LuaRenamer.Tests.Fakes;
using NSubstitute;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Xunit;

namespace LuaRenamer.Tests.Env;

/// <summary>
/// The anime slice of the host mapping, asserted on the produced model: which of the two title sources wins,
/// how titles are ordered, how relations recurse and where that recursion stops, seasons, and dates AniDB
/// knows only in part or not at all.
/// </summary>
public class AnimeProducerTests
{
    [Fact]
    public void TheShokoSeriesTitleWinsOverTheSourceMetadataTitle()
    {
        AnimeModel model = ModelProducers.AnimeToModel(
            HostFakes.Series(shokoTitle: "shokoPref", anidbTitle: "anidbPref").AnidbAnime);

        model.preferredname.Should().Be("shokoPref");
        model.defaultname.Should().Be("shokoPref");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankShokoTitleFallsBackToTheSourceMetadataTitle(string blank) =>
        ModelProducers.AnimeToModel(HostFakes.Series(shokoTitle: blank, anidbTitle: "anidbPref").AnidbAnime)
            .preferredname.Should().Be("anidbPref");

    [Fact]
    public void ABlankShokoDefaultTitleFallsBackToTheSourceDefaultTitle()
    {
        IShokoSeries series = HostFakes.Series(anidbTitle: "anidbDefault");
        series.DefaultTitle.Value.Returns("");

        ModelProducers.AnimeToModel(series.AnidbAnime).defaultname.Should().Be("anidbDefault");
    }

    [Fact]
    public void AnAnimeWithNoShokoSeriesUsesItsOwnTitle() =>
        ModelProducers.AnimeToModel(HostFakes.Anime(title: "onlyAnidb")).preferredname.Should().Be("onlyAnidb");

    [Fact]
    public void TitlesAreOrderedByValue() =>
        ModelProducers.AnimeToModel(HostFakes.AnimeWithTitles(
                ("Zebra", TitleLanguage.English, TitleType.Synonym),
                ("Apple", TitleLanguage.Japanese, TitleType.Official)))
            .titles.Select(t => t.name).Should().Equal("Apple", "Zebra");

    [Fact]
    public void RelationsRecurseOnceAndThenPrune()
    {
        AnimeModel model = ModelProducers.AnimeToModel(
            HostFakes.AnimeInARelationCycle("relatedName", RelationType.AlternativeSetting));

        model.relations.Should().ContainSingle();
        model.relations[0].type.Should().Be(RelationType.AlternativeSetting);
        model.relations[0].anime.preferredname.Should().Be("relatedName");
        model.relations[0].anime.relations.Should().BeEmpty("a nested relation anime is built with relations pruned");
    }

    [Fact]
    public void ARelationBackToTheAnimeItselfIsDropped() =>
        ModelProducers.AnimeToModel(HostFakes.AnimeRelatedToItself()).relations.Should().BeEmpty();

    [Fact]
    public void ARelationToAnAnimeTheHostHasNoRecordOfIsDropped() =>
        ModelProducers.AnimeToModel(HostFakes.AnimeRelatedToOneNotInTheDatabase()).relations.Should().BeEmpty();

    [Fact]
    public void AirAndEndDatesMapToTheirDays()
    {
        AnimeModel model = ModelProducers.AnimeToModel(
            HostFakes.AnimeAired(new PartialDateOnly(2020, 4, 5), new PartialDateOnly(2020, 6, 21)));

        (model.airdate!.year, model.airdate.month, model.airdate.day).Should().Be((2020L, 4L, 5L));
        (model.enddate!.year, model.enddate.month, model.enddate.day).Should().Be((2020L, 6L, 21L));
    }

    [Fact]
    public void ADateAnidbKnowsOnlyToTheYearReadsAsTheFirstOfJanuary()
    {
        // AniDB dates older and unannounced anime by year or month alone; the model has no way to say so.
        DateTimeModel airdate = ModelProducers.AnimeToModel(HostFakes.AnimeAired(new PartialDateOnly(1987))).airdate!;

        (airdate.year, airdate.month, airdate.day).Should().Be((1987L, 1L, 1L));
    }

    [Fact]
    public void SeasonsAreMapped()
    {
        AnimeModel model = ModelProducers.AnimeToModel(HostFakes.AnimeWithSeasons((2024, YearlySeason.Winter)));

        model.seasons.Should().ContainSingle();
        model.seasons[0].year.Should().Be(2024);
        model.seasons[0].season.Should().Be(YearlySeason.Winter);
    }

    [Fact]
    public void AnAbsentEndDateProducesNoEndDate() =>
        ModelProducers.AnimeToModel(HostFakes.Anime()).enddate.Should().BeNull();

    [Fact]
    public void CustomTagsComeFromTheShokoSeriesAndTagsFromTheSourceMetadata()
    {
        AnimeModel model = ModelProducers.AnimeToModel(
            HostFakes.SeriesWithTags(shokoTags: ["custom1"], sourceTags: ["action"]).AnidbAnime);

        model.tags.Should().Equal("action");
        model.customtags.Should().Equal("custom1");
    }

    [Fact]
    public void EpisodeCountsSpanEveryEpisodeType() =>
        ModelProducers.AnimeToModel(HostFakes.Anime()).episodecounts.Keys
            .Should().BeEquivalentTo(Enum.GetValues<EpisodeType>().Distinct());
}
