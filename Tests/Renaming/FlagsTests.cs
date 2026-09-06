using AwesomeAssertions;
using LuaRenamer.Tests.Fakes;
using Microsoft.Extensions.Logging.Testing;
using Shoko.Abstractions.Video.Relocation;
using Xunit;

namespace LuaRenamer.Tests.Renaming;

/// <summary>
/// The two ways an operation is turned off: the host disables it for the whole relocation, or the script opts
/// out of it. Either way the corresponding field of the result must be left unset rather than computed and
/// discarded.
/// </summary>
public class FlagsTests
{
    private const string Outputs = "filename = 'renamed'\nsubfolder = {'sub'}\n";

    private static RelocationResult Run(RelocationGraph graph, string script) =>
        new LuaRenamer(new FakeLogger<LuaRenamer>()).GetPath(graph.Context(script));

    [Fact]
    public void WithNothingSkippedBothFieldsArePopulated()
    {
        RelocationResult result = Run(RelocationGraph.Default(), Outputs);

        result.FileName.Should().Be("renamed.mp4");
        result.Path!.NormPath().Should().Be("sub");
        result.SkipRename.Should().BeFalse();
        result.SkipMove.Should().BeFalse();
    }

    [Fact]
    public void AScriptSkippingTheRenameYieldsNoFileName()
    {
        RelocationResult result = Run(RelocationGraph.Default(), Outputs + "skip_rename = true");

        result.SkipRename.Should().BeTrue();
        result.FileName.Should().BeNull();
        result.Path!.NormPath().Should().Be("sub", "skipping the rename leaves the move alone");
    }

    [Fact]
    public void AScriptSkippingTheMoveYieldsNoPath()
    {
        RelocationResult result = Run(RelocationGraph.Default(), Outputs + "skip_move = true");

        result.SkipMove.Should().BeTrue();
        result.Path.Should().BeNull();
        result.ManagedFolder.Should().BeNull();
        result.FileName.Should().Be("renamed.mp4", "skipping the move leaves the rename alone");
    }

    [Fact]
    public void AHostWithRenamingDisabledYieldsNoFileName()
    {
        var graph = RelocationGraph.Default();
        graph.RenameEnabled = false;

        RelocationResult result = Run(graph, Outputs);

        result.FileName.Should().BeNull();
        result.Path!.NormPath().Should().Be("sub");
    }

    [Fact]
    public void AHostWithMovingDisabledYieldsNoPath()
    {
        var graph = RelocationGraph.Default();
        graph.MoveEnabled = false;

        RelocationResult result = Run(graph, Outputs);

        result.Path.Should().BeNull();
        result.ManagedFolder.Should().BeNull();
        result.FileName.Should().Be("renamed.mp4");
    }

    [Fact]
    public void TheSettingsDefaultsReachTheScript()
    {
        // The host's configuration seeds the output flags, so a script can read them before deciding.
        var graph = RelocationGraph.Default();
        graph.Settings.ReplaceIllegalCharacters = true;
        graph.Settings.RemoveIllegalCharacters = true;
        graph.Settings.UseExistingAnimeLocation = true;

        RelocationResult result = Run(graph,
            "filename = tostring(replace_illegal_chars) .. tostring(remove_illegal_chars) .. tostring(use_existing_anime_location)");

        result.FileName.Should().Be("truetruetrue.mp4");
    }

    [Theory]
    [InlineData("remove_illegal_chars = true", "abc.mp4")]
    [InlineData("replace_illegal_chars = true", "a？b？c.mp4")]
    [InlineData("", "a_b_c.mp4")]
    public void IllegalCharacterHandlingFollowsTheScriptsFlags(string flags, string expected) =>
        Run(RelocationGraph.Default(), $"{flags}\nfilename = 'a?b?c'").FileName.Should().Be(expected);

    [Fact]
    public void AScriptCanOverrideTheReplacementMap() =>
        Run(RelocationGraph.Default(),
                "replace_illegal_chars = true\nillegal_chars_map['?'] = '!'\nfilename = 'a?b'")
            .FileName.Should().Be("a!b.mp4");
}
