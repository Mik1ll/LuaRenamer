using System;
using System.IO;
using AwesomeAssertions;
using LuaRenamer.Tests.Fakes;
using Microsoft.Extensions.Logging.Testing;
using Shoko.Abstractions.Video.Relocation;
using Xunit;

namespace LuaRenamer.Tests.Renaming;

/// <summary>
/// <c>collision_filename</c> is decided against the real file system, so each test gets its own directory
/// holding a <c>source</c> folder the file starts in and a <c>target</c> folder the script moves it to.
/// </summary>
public sealed class CollisionFilenameTests : IDisposable
{
    private const string Script = "filename = 'episode'; collision_filename = 'episode [anidbfile-123]'; subfolder = 'target'";

    private readonly string _root = Directory.CreateTempSubdirectory("LuaRenamer-").FullName;

    public CollisionFilenameTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "source"));
        Directory.CreateDirectory(Path.Combine(_root, "target"));
    }

    public void Dispose() => Directory.Delete(_root, true);

    private void Occupy(string relativePath, string contents = "occupied") =>
        File.WriteAllText(Path.Combine(_root, relativePath), contents);

    private RelocationResult Run(string relativePath, string script = Script, params string[] copies)
    {
        Occupy(relativePath, "source");
        var graph = RelocationGraph.Default();
        graph.PlaceFile(_root, relativePath, copies);
        return new LuaRenamer(new FakeLogger<LuaRenamer>()).GetPath(graph.Context(script));
    }

    private static string Source => Path.Combine("source", "source.mp4");

    private static string Target(string fileName) => Path.Combine("target", fileName);

    [Fact]
    public void AFreeTargetKeepsTheFilename() =>
        Run(Source).FileName.Should().Be("episode.mp4");

    [Fact]
    public void AnOccupiedTargetTakesTheCollisionFilename()
    {
        Occupy(Target("episode.mp4"));

        Run(Source).FileName.Should().Be("episode [anidbfile-123].mp4");
    }

    [Fact]
    public void WithoutACollisionFilenameAnOccupiedTargetIsLeftToTheHost()
    {
        Occupy(Target("episode.mp4"));

        Run(Source, "filename = 'episode'; subfolder = 'target'").FileName.Should().Be("episode.mp4");
    }

    [Fact]
    public void AFileAlreadyAtTheTargetIsNotACollisionWithItself() =>
        Run(Target("episode.mp4")).FileName.Should().Be("episode.mp4");

    [Fact]
    public void WhenNotMovingTheCurrentDirectoryIsChecked()
    {
        Occupy(Path.Combine("source", "episode.mp4"));
        // Taken only in the folder the file would have moved to, so it must not count.
        Occupy(Target("episode [anidbfile-123].mp4"));

        RelocationResult result = Run(Source, Script + "; skip_move = true");

        result.Error.Should().BeNull();
        result.ManagedFolder.Should().BeNull();
        result.FileName.Should().Be("episode [anidbfile-123].mp4");
    }

    [Fact]
    public void BothNamesTakenIsAnError()
    {
        Occupy(Target("episode.mp4"));
        Occupy(Target("episode [anidbfile-123].mp4"));

        RelocationResult result = Run(Source);

        result.Error.Should().NotBeNull();
        result.FileName.Should().BeNull();
    }

    [Fact]
    public void OnceAppliedTheCollisionFilenameIsKept()
    {
        Occupy(Target("episode.mp4"));

        Run(Target("episode [anidbfile-123].mp4")).FileName.Should().Be("episode [anidbfile-123].mp4");
    }

    [Fact]
    public void ACaseOnlyRenameIsNotACollision() =>
        Run(Target("episode.mp4"), "filename = 'Episode'; collision_filename = 'Episode [anidbfile-123]'; subfolder = 'target'")
            .FileName.Should().Be("Episode.mp4");

    [Fact]
    public void ACopyOfTheSameVideoIsNotACollision()
    {
        Occupy(Target("episode.mp4"), "source copy");

        Run(Source, Script, Target("episode.mp4")).FileName.Should().Be("episode.mp4");
    }
}
