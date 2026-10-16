using System.Collections.Generic;
using AwesomeAssertions;
using LuaRenamer.Tests.Fakes;
using Microsoft.Extensions.Logging.Testing;
using Shoko.Abstractions.Video.Relocation;
using Xunit;

namespace LuaRenamer.Tests.Renaming;

/// <summary>Every form a script can name a subfolder in, plus the default when it names none.</summary>
public class SubfolderTests
{
    private static RelocationResult Run(RelocationGraph graph, string script) =>
        new LuaRenamer(new FakeLogger<LuaRenamer>()).GetPath(graph.Context(script));

    private static string? Subfolder(RelocationGraph graph, string script)
    {
        RelocationResult result = Run(graph, script);
        result.Error.Should().BeNull();
        return result.Path?.NormPath();
    }

    [Theory]
    [InlineData("subfolder = 'one'", "one")]
    [InlineData("subfolder = {'one'}", "one")]
    [InlineData("subfolder = {'one', 'two'}", "one/two")]
    // The array walk stops at the first hole, so a sparse array truncates rather than skipping.
    [InlineData("subfolder = {'one', nil, 'two'}", "one")]
    [InlineData("subfolder = {[2] = 'two', [1] = 'one'}", "one/two")]
    [InlineData("subfolder = {}", "")]
    [InlineData("replace_illegal_chars = true ; subfolder = {'one\\\\', 'two'}", "one＼/two")]
    [InlineData("replace_illegal_chars = true ; subfolder = 'one\\\\two/three'", "one＼two／three")]
    public void SubfolderForms(string script, string expected) =>
        (Subfolder(RelocationGraph.Default(), script) ?? "").Should().Be(expected.NormPath());

    [Fact]
    public void AnArrayContainingANonStringIsRejected() =>
        Run(RelocationGraph.Default(), "subfolder = {'one', 2}").Error.Should().NotBeNull();

    [Fact]
    public void AValueOfAnUnexpectedTypeIsRejected() =>
        Run(RelocationGraph.Default(), "subfolder = 42").Error.Should().NotBeNull();

    [Fact]
    public void TheDefaultIsThePrimarySeriesNameScriptsSee()
    {
        // Deliberately not primary-first: the default must re-derive the primary series rather than trust
        // the order the context arrived in.
        (RelocationGraph graph, IReadOnlyList<int> _, IReadOnlyList<string> titles) = RelocationGraph.MultiSeries([1, 0]);

        Subfolder(graph, "filename = 'x'").Should().Be(titles[0]);
    }

    [Fact]
    public void TheDefaultFallsBackTheSameWayThePreferredNameDoes()
    {
        // A blank Shoko title used to reach path cleaning verbatim and fail the whole relocation.
        var graph = RelocationGraph.Default();
        graph.BlankOutTheSeriesTitle();

        Subfolder(graph, "filename = 'x'").Should().Be("anidbTitle");
    }
}
