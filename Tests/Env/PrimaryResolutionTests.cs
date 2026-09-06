using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using LuaRenamer.LuaEnv.Models;
using LuaRenamer.Tests.Fakes;
using Microsoft.Extensions.Logging.Testing;
using Shoko.Abstractions.Metadata.Enums;
using Xunit;

namespace LuaRenamer.Tests.Env;

/// <summary>
/// Which series and episode are primary, and the ordering that follows from it. Nothing downstream may trust
/// the order the relocation context happens to arrive in, so the properties here feed it permuted.
/// </summary>
public class PrimaryResolutionTests
{
    /// <summary>A permutation of the series indices, used to shuffle the context's collections.</summary>
    public sealed record Permutation(IReadOnlyList<int> Order)
    {
        public override string ToString() => string.Join(",", Order);
    }

    public static class Permutations
    {
        private const int Size = 4;

        public static Arbitrary<Permutation> Orders() =>
            Gen.Choose(0, 1_000_000).ListOf(Size)
                .Select(keys => new Permutation([.. Enumerable.Range(0, Size).OrderBy(i => keys[i])]))
                .ToArbitrary();
    }

    private static EnvModel Env(RelocationGraph graph) =>
        ModelProducers.EnvToModel(graph.Context(""), new FakeLogger<LuaRenamer>());

    [Property(Arbitrary = [typeof(Permutations)], MaxTest = 30)]
    public void ThePrimarySeriesIsTheLowestSourceIdRegardlessOfArrivalOrder(Permutation order)
    {
        (RelocationGraph graph, IReadOnlyList<int> sourceIds, IReadOnlyList<string> _) = RelocationGraph.MultiSeries(order.Order);

        ModelProducers.PrimarySeries(graph.Context("")).AnidbAnimeID.Should().Be(sourceIds[0]);
    }

    [Property(Arbitrary = [typeof(Permutations)], MaxTest = 30)]
    public void ThePrimaryAnimeAndEpisodeComeFirst(Permutation order)
    {
        (RelocationGraph graph, IReadOnlyList<int> sourceIds, IReadOnlyList<string> _) = RelocationGraph.MultiSeries(order.Order);

        EnvModel env = Env(graph);

        env.animes[0].id.Should().Be(sourceIds[0]);
        env.anime.Should().BeSameAs(env.animes[0]);
        env.episodes[0].animeid.Should().Be(sourceIds[0]);
        env.episode.Should().BeSameAs(env.episodes[0]);
    }

    [Property(Arbitrary = [typeof(Permutations)], MaxTest = 30)]
    public void GroupsContainingThePrimarySeriesComeFirst(Permutation order)
    {
        (RelocationGraph graph, IReadOnlyList<int> _, IReadOnlyList<string> __) =
            RelocationGraph.MultiSeries(order.Order, withGroups: true);

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
    public void TmdbIsSourcedFromThePrimarySeriesNotTheFirstOneListed() =>
        Env(RelocationGraph.TwoSeriesWithTmdbOnThePrimaryOne())
            .tmdb.shows.Should().ContainSingle().Which.id.Should().Be(Ids.TmdbShow);
}
