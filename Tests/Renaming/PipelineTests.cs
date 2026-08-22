using AwesomeAssertions;
using LuaRenamer.Tests.Fakes;
using Microsoft.Extensions.Logging.Testing;
using Shoko.Abstractions.Video.Relocation;
using Xunit;

namespace LuaRenamer.Tests.Renaming;

/// <summary>The whole relocation, end to end: a script runs and its outputs become a result.</summary>
public class PipelineTests
{
    private static RelocationResult Run(RelocationGraph graph, string script) =>
        new LuaRenamer(new FakeLogger<LuaRenamer>()).GetPath(graph.Context(script));

    [Fact]
    public void AScriptSetsAFileNameAndYieldsADestinationAndSubfolder()
    {
        var graph = RelocationGraph.Default();

        RelocationResult result = Run(graph, "filename = 'renamed'");

        result.Error.Should().BeNull();
        result.FileName.Should().Be("renamed.mp4", "the original extension is carried over");
        result.ManagedFolder.Should().BeSameAs(graph.Folder);
        result.Path.Should().Be("shokoTitle", "with no subfolder set, the primary series' name is the default");
    }

    [Fact]
    public void AFileNameOfANonStringLeavesTheOriginalName() =>
        Run(RelocationGraph.Default(), "filename = 42").FileName.Should().Be("testfilename.mp4");

    [Theory]
    [InlineData("return (", "fails to load")]
    [InlineData("error('boom')", "throws")]
    [InlineData("filename = ", "is not valid Lua")]
    public void AFailingScriptYieldsAnErrorRatherThanAPath(string script, string why)
    {
        RelocationResult result = Run(RelocationGraph.Default(), script);

        result.Error.Should().NotBeNull(why);
        result.FileName.Should().BeNull();
        result.Path.Should().BeNull();
    }
}
