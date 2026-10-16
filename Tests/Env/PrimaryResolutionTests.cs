using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using LuaRenamer.LuaEnv.Models;
using LuaRenamer.Tests.Fakes;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using Shoko.Abstractions.Metadata.Enums;
using Xunit;

namespace LuaRenamer.Tests.Env;

/// <summary>
/// Which series and episode are primary, and the ordering that follows from it. Nothing downstream may trust
/// the order the relocation context happens to arrive in, so the theories here feed it in every order.
/// </summary>
public class PrimaryResolutionTests
{
    /// <summary>All 24 orders of four series indices, used to shuffle the context's collections.</summary>
    public static TheoryData<int[]> Orders => [.. Permute([0, 1, 2, 3])];

    private static IEnumerable<int[]> Permute(int[] items) =>
        items.Length <= 1
            ? [items]
            : items.SelectMany(first => Permute([.. items.Where(i => i != first)]).Select(rest => (int[])[first, .. rest]));

    private static EnvModel Env(RelocationGraph graph) =>
        ModelProducers.EnvToModel(graph.Context(""), new FakeLogger<LuaRenamer>());

    [Theory]
    [MemberData(nameof(Orders))]
    public void ThePrimarySeriesIsTheLowestSourceIdRegardlessOfArrivalOrder(int[] order)
    {
        (RelocationGraph graph, IReadOnlyList<int> sourceIds, IReadOnlyList<string> _) = RelocationGraph.MultiSeries(order);

        ModelProducers.PrimarySeries(graph.Context("")).AnidbAnimeID.Should().Be(sourceIds[0]);
    }

    [Theory]
    [MemberData(nameof(Orders))]
    public void ThePrimaryAnimeAndEpisodeComeFirst(int[] order)
    {
        (RelocationGraph graph, IReadOnlyList<int> sourceIds, IReadOnlyList<string> _) = RelocationGraph.MultiSeries(order);

        EnvModel env = Env(graph);

        env.animes[0].id.Should().Be(sourceIds[0]);
        env.anime.Should().BeSameAs(env.animes[0]);
        env.episodes[0].animeid.Should().Be(sourceIds[0]);
        env.episode.Should().BeSameAs(env.episodes[0]);
    }

    [Theory]
    [MemberData(nameof(Orders))]
    public void GroupsContainingThePrimarySeriesComeFirst(int[] order)
    {
        (RelocationGraph graph, IReadOnlyList<int> _, IReadOnlyList<string> __) =
            RelocationGraph.MultiSeries(order, withGroups: true);

        EnvModel env = Env(graph);

        env.groups[0].name.Should().Be("group0");
        env.group.Should().BeSameAs(env.groups[0]);
    }

    [Fact]
    public void ThePrimaryEpisodeIsTheLowestTypeAndNumberWithinThatSeries()
    {
        var graph = RelocationGraph.Default();
        graph.SetEpisodes([(5, EpisodeType.Episode), (2, EpisodeType.Episode), (1, EpisodeType.Special)]);

        Env(graph).episode.number.Should().Be(2, "an Episode outranks a Special, and within a type the lower number wins");
    }

    [Fact]
    public void ThePrimaryEpisodeIsRankedByItsAnidbTypeAndNumberNotShokos()
    {
        var graph = RelocationGraph.Default();
        graph.SetEpisodes([(1, EpisodeType.Special), (2, EpisodeType.Episode)]);
        graph.Episodes[0].Type.Returns(EpisodeType.Episode);
        graph.Episodes[1].Type.Returns(EpisodeType.Special);

        EpisodeModel episode = Env(graph).episode;

        episode.type.Should().Be(EpisodeType.Episode);
        episode.number.Should().Be(2);
    }

    [Fact]
    public void TmdbIsSourcedFromThePrimarySeriesNotTheFirstOneListed() =>
        Env(RelocationGraph.TwoSeriesWithTmdbOnThePrimaryOne())
            .tmdb.shows.Should().ContainSingle().Which.id.Should().Be(Ids.TmdbShow);
}
