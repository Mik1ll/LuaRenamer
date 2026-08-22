using System.Linq;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Relocation;
using Xunit;

namespace LuaRenamer.Tests.Fakes;

/// <summary>Proof that the arrangement factories hold up the properties the rest of the suite relies on.</summary>
public class FakesTests
{
    [Fact]
    public void DefaultGraphDrivesGetPathToCompletion()
    {
        var graph = RelocationGraph.Default();
        RelocationResult result = new LuaRenamer(new FakeLogger<LuaRenamer>()).GetPath(graph.Context("filename = 'out'"));

        result.Error.Should().BeNull();
        result.FileName.Should().Be("out.mp4");
        result.ManagedFolder.Should().BeSameAs(graph.Folder);
    }

    [Fact]
    public void IdentifierSpacesCannotCollide()
    {
        var graph = RelocationGraph.Default();

        int[] shoko = [graph.Series.ID, graph.Episode.ID, graph.Folder.ID];
        int[] anidb = [graph.Anime.ID, graph.AnidbEpisode.ID, graph.AnidbEpisode.SeriesID];

        shoko.Should().OnlyContain(id => id != 0);
        anidb.Should().OnlyContain(id => id != 0);
        shoko.Intersect(anidb).Should().BeEmpty("comparing one identifier space against another must never match by coincidence");
    }

    [Fact]
    public void OneFieldIsOverriddenWithoutRestatingArrangement()
    {
        var graph = RelocationGraph.Default();
        graph.Anime.Type.Returns(AnimeType.Movie);

        RelocationResult result = new LuaRenamer(new FakeLogger<LuaRenamer>())
            .GetPath(graph.Context("filename = anime.type"));

        result.Error.Should().BeNull();
        result.FileName.Should().Be("Movie.mp4");
    }

    [Fact]
    public void FakeLoggerCapturesLevelAndMessage()
    {
        var logger = new FakeLogger<LuaRenamer>();
        new LuaRenamer(logger).GetPath(RelocationGraph.Default().Context("log('from the script')"));

        FakeLogRecord record = logger.Collector.GetSnapshot().Single(r => r.Message == "from the script");
        record.Level.Should().Be(LogLevel.Information);
    }
}
