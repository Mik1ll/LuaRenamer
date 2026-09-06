using System.IO;
using AwesomeAssertions;
using LuaRenamer.Tests.Fakes;
using Microsoft.Extensions.Logging.Testing;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Relocation;
using Xunit;

namespace LuaRenamer.Tests.Renaming;

/// <summary>Every form a script can name a destination in, and the one folder kind that is refused.</summary>
public class DestinationTests
{
    private static RelocationResult Run(RelocationGraph graph, string script) =>
        new LuaRenamer(new FakeLogger<LuaRenamer>()).GetPath(graph.Context(script));

    /// <summary>The graph's own folder plus a second one the tests select by various means.</summary>
    private static (RelocationGraph Graph, int SecondID) TwoFolders(DropFolderType type = DropFolderType.Destination)
    {
        var graph = RelocationGraph.Default();
        return (graph, graph.AddFolder("secondimport", Path.Combine("D:", "second"), type));
    }

    [Fact]
    public void ByName()
    {
        (RelocationGraph graph, var second) = TwoFolders();

        Run(graph, "destination = 'secondimport'").ManagedFolder!.ID.Should().Be(second);
    }

    [Fact]
    public void ByPath()
    {
        (RelocationGraph graph, var second) = TwoFolders();

        Run(graph, $"destination = [[{graph.FolderPath(second)}]]").ManagedFolder!.ID.Should().Be(second);
    }

    [Fact]
    public void ByFolderReference()
    {
        (RelocationGraph graph, var second) = TwoFolders();

        RelocationResult result = Run(graph, $"destination = from(importfolders):where('id', {second}):first()");

        result.Error.Should().BeNull();
        result.ManagedFolder!.ID.Should().Be(second);
    }

    [Fact]
    public void UnsetPrefersTheFolderSharingTheLongestPrefixWithTheFile()
    {
        // The file lives under the graph's own folder, so that one wins over an unrelated destination.
        (RelocationGraph graph, var _) = TwoFolders();

        Run(graph, "filename = 'x'").ManagedFolder!.ID.Should().Be(graph.Folder.ID);
    }

    [Fact]
    public void AFolderThatIsNotADestinationIsRejected()
    {
        (RelocationGraph graph, var _) = TwoFolders(DropFolderType.Source);

        RelocationResult result = Run(graph, "destination = 'secondimport'");

        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("not a destination folder");
    }

    [Fact]
    public void AnUnknownNameIsRejected() =>
        Run(RelocationGraph.Default(), "destination = 'nosuchfolder'").Error.Should().NotBeNull();

    [Fact]
    public void ATableThatIsNotAnImportFolderIsRejected() =>
        Run(RelocationGraph.Default(), "destination = { 1, 2 }").Error.Should().NotBeNull();

    [Fact]
    public void AValueOfAnUnexpectedTypeIsRejected() =>
        Run(RelocationGraph.Default(), "destination = 42").Error.Should().NotBeNull();

    [Fact]
    public void NoAvailableDestinationIsRejected()
    {
        var graph = RelocationGraph.Default();
        graph.Folders.Clear();
        graph.AddFolder("sourceonly", Path.Combine("C:", "testimportfolder"), DropFolderType.Source);

        Run(graph, "filename = 'x'").Error.Should().NotBeNull();
    }
}
