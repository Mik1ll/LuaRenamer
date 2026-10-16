using System;
using System.Collections.Generic;
using System.Globalization;
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
/// Episode-number collapsing: padded, type-prefixed and range-compressed. Range compression is invertible, so
/// the round-trip is a real oracle rather than a reimplementation of the producer.
/// </summary>
public class EpisodeNumbersTests
{
    /// <summary>A set of episodes belonging to the primary series, distinct by type and number.</summary>
    public sealed record EpisodeSet(IReadOnlyList<(EpisodeType Type, int Number)> Episodes, int Pad)
    {
        public override string ToString() =>
            $"pad {Pad}: " + string.Join(" ", Episodes.Select(e => $"{e.Type}{e.Number}"));
    }

    public static class Sets
    {
        public static Arbitrary<EpisodeSet> EpisodeSets() =>
            Gen.Choose(1, 40).ListOf(12).SelectMany(numbers =>
                    Gen.Choose(0, 5).ListOf(12).SelectMany(types =>
                        Gen.Choose(1, 4).Select(pad => new EpisodeSet(
                            [
                                .. numbers.Zip(types, (n, t) => (Type: (EpisodeType)(t + 1), Number: n))
                                    .DistinctBy(e => (e.Type, e.Number)),
                            ], pad))))
                .ToArbitrary();
    }

    /// <summary>Runs the env's <c>episode_numbers</c> free function over the given episodes.</summary>
    private static string Collapse(IReadOnlyList<(EpisodeType Type, int Number)> episodes, int pad)
    {
        var graph = RelocationGraph.Default();
        graph.SetEpisodes([.. episodes.Select(e => (e.Number, e.Type))]);

        EnvModel env = ModelProducers.EnvToModel(graph.Context(""), new FakeLogger<LuaRenamer>());
        return env.episode_numbers(pad);
    }

    /// <summary>The inverse: parses the collapsed form back into the set of type-and-number pairs it names.</summary>
    private static List<(EpisodeType Type, int Number)> Parse(string collapsed)
    {
        var byPrefix = Utils.EpPrefix.ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.Ordinal);
        var parsed = new List<(EpisodeType Type, int Number)>();
        foreach (var token in collapsed.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var digits = token.IndexOfAny(['0', '1', '2', '3', '4', '5', '6', '7', '8', '9']);
            EpisodeType type = byPrefix[token[..digits]];
            var range = token[digits..].Split('-');
            var first = int.Parse(range[0], CultureInfo.InvariantCulture);
            var last = int.Parse(range[^1], CultureInfo.InvariantCulture);
            for (var n = first; n <= last; n++)
                parsed.Add((type, n));
        }

        return parsed;
    }

    [Property(Arbitrary = [typeof(Sets)], MaxTest = 40)]
    public void CollapsingRoundTrips(EpisodeSet set)
    {
        if (set.Episodes.Count == 0) return; // the producer requires at least one episode

        Parse(Collapse(set.Episodes, set.Pad)).Should().BeEquivalentTo(set.Episodes);
    }

    [Theory]
    // Recorded collapsings, including the padding width, the type prefixes and where a range starts and ends.
    // The extra series ids are episodes of *other* anime, which must not reach the primary series' numbering.
    [InlineData(new[] { 1, 1, 1 }, new[] { 1, 3, 5 }, new byte[] { 1, 1, 1 }, 2, "01 03 05")]
    [InlineData(new[] { 1, 1, 1, 1 }, new[] { 1, 2, 1, 2 }, new byte[] { 1, 1, 2, 2 }, 2, "01-02 C01-02")]
    [InlineData(new[] { 1, 1, 1, 1, 1 }, new[] { 5, 1, 3, 2, 4 }, new byte[] { 1, 1, 1, 1, 1 }, 2, "01-05")]
    [InlineData(new[] { 1, 1, 1, 1 }, new[] { 10, 11, 12, 13 }, new byte[] { 1, 1, 2, 2 }, 2, "10-11 C12-13")]
    [InlineData(new[] { 1, 1 }, new[] { 1, 2 }, new byte[] { 1, 2 }, 2, "01 C02")]
    [InlineData(
        new[] { 1, 1, 1, 2, 1, 3, 1, 1, 1, 4, 1, 1, 1 },
        new[] { 6, 12, 5, 22, 2, 20, 5, 7, 1, 4, 9, 3, 2 },
        new byte[] { 1, 6, 1, 1, 3, 1, 2, 1, 6, 1, 6, 1, 6 },
        3, "003 005-007 C005 S002 O001-002 O009 O012")]
    public void RecordedCollapsings(int[] whichSeries, int[] numbers, byte[] types, int pad, string expected)
    {
        var graph = RelocationGraph.Default();
        // Series 1 is the primary (lowest source id); the rest exist only to be filtered out.
        graph.SetSeriesAndEpisodes(4,
            [.. whichSeries.Select((s, i) => (Series: s, Number: numbers[i], Type: (EpisodeType)types[i]))]);

        EnvModel env = ModelProducers.EnvToModel(graph.Context(""), new FakeLogger<LuaRenamer>());

        env.episode_numbers(pad).Should().Be(expected);
    }
}
