using System.IO;
using System.Linq;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
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

    [Fact]
    public void CrossReferencesInTheGraphAgreeWithWhatTheyPointAt()
    {
        // NSubstitute evaluates a Returns() argument *after* the receiver, so reading one substitute inside
        // another's Returns() silently retargets the stub — the id lands on the thing being read instead of
        // the thing being configured, and both members end up wrong without any test failing outright.
        // These are the cross-references most easily lost that way.
        var graph = RelocationGraph.Default();

        graph.File.ManagedFolderID.Should().Be(graph.Folder.ID);
        graph.File.VideoID.Should().Be(graph.Video.ID);
        graph.File.Path.Should().Be(Path.Combine(graph.Folder.Path, graph.File.RelativePath));
        graph.Folder.Path.Should().NotBe(graph.File.Path, "a folder's path is not its file's path");

        graph.Episode.AnidbEpisodeID.Should().Be(graph.AnidbEpisode.ID);
        graph.Episode.Type.Should().Be(graph.AnidbEpisode.Type);
        graph.Episode.EpisodeNumber.Should().Be(graph.AnidbEpisode.EpisodeNumber);
        graph.Series.AnidbAnimeID.Should().Be(graph.Anime.ID);
        graph.Anime.ShokoSeries.Should().ContainSingle().Which.Should().BeSameAs(graph.Series);
    }

    [Fact]
    public void AGroupPointsAtTheSeriesItWasBuiltFrom()
    {
        var graph = RelocationGraph.Default();
        IShokoGroup group = HostFakes.Group(Ids.ShokoGroup, "group", graph.Series);

        group.MainSeriesID.Should().Be(graph.Series.ID);
        group.MainSeries.Should().BeSameAs(graph.Series);
        group.AllSeries.Should().ContainSingle().Which.Should().BeSameAs(graph.Series);
    }
}
