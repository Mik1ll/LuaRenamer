using System.IO;
using AwesomeAssertions;
using LuaRenamer.Tests.Fakes;
using Microsoft.Extensions.Logging.Testing;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Relocation;
using Xunit;

namespace LuaRenamer.Tests.Renaming;

/// <summary>
/// Reuse of wherever the series' other files already live. This path takes precedence over the script's own
/// destination and subfolder, so both which candidate it picks and what it does when none qualifies matter.
/// </summary>
public class ExistingLocationTests
{
    private const string Script = "use_existing_anime_location = true; subfolder = 'ignored'";

    private static RelocationResult Run(RelocationGraph graph) =>
        new LuaRenamer(new FakeLogger<LuaRenamer>()).GetPath(graph.Context(Script));

    [Fact]
    public void ThePlaceMostOfTheSeriesAlreadyLivesWins()
    {
        var graph = RelocationGraph.Default();
        var crowded = graph.AddFolder("crowded", Path.Combine("D:", "crowded"));
        var sparse = graph.AddFolder("sparse", Path.Combine("E:", "sparse"));
        graph.GiveSeriesExistingFiles(
            ("a", crowded, "Popular Series"),
            ("b", crowded, "Popular Series"),
            ("c", sparse, "Lonely Series"));

        RelocationResult result = Run(graph);

        result.Error.Should().BeNull();
        result.ManagedFolder!.ID.Should().Be(crowded);
        result.Path!.NormPath().Should().Be("Popular Series");
    }

    [Fact]
    public void AnExcludedFolderStillCountsAsAnExistingLocation()
    {
        var graph = RelocationGraph.Default();
        var excluded = graph.AddFolder("excluded", Path.Combine("D:", "excluded"), DropFolderType.Excluded);
        graph.GiveSeriesExistingFiles(("a", excluded, "Kept Together"));

        Run(graph).ManagedFolder!.ID.Should().Be(excluded);
    }

    [Fact]
    public void TheFileBeingRelocatedIsNotItsOwnPrecedent()
    {
        // Same hash as the file under relocation, so it must be excluded from the candidates and the
        // script's own subfolder used instead.
        var graph = RelocationGraph.Default();
        graph.GiveSeriesExistingFiles((graph.FileHash, graph.Folder.ID, "Not A Precedent"));

        Run(graph).Path!.NormPath().Should().Be("ignored");
    }

    [Fact]
    public void FallsBackToTheScriptWhenNoCandidateQualifies()
    {
        // A file sitting at a folder's root has no subfolder to reuse, so it is not a candidate.
        var graph = RelocationGraph.Default();
        graph.GiveSeriesExistingFiles(("a", graph.Folder.ID, ""));

        RelocationResult result = Run(graph);

        result.Error.Should().BeNull();
        result.ManagedFolder!.ID.Should().Be(graph.Folder.ID);
        result.Path!.NormPath().Should().Be("ignored");
    }

    [Fact]
    public void FallsBackWhenTheSeriesHasNoOtherFiles()
    {
        var graph = RelocationGraph.Default();
        graph.GiveSeriesExistingFiles();

        Run(graph).Path!.NormPath().Should().Be("ignored");
    }
}
