using System.IO;
using AwesomeAssertions;
using LuaRenamer.Tests.Fakes;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Video.Relocation;
using Xunit;

namespace LuaRenamer.Tests.Renaming;

/// <summary>
/// The script that ships with the plugin, run against an environment with every optional slice filled in.
/// This is the one test that exercises the shipped Lua end to end rather than a purpose-written snippet.
/// </summary>
public class DefaultScriptTests
{
    private static string DefaultScript =>
        File.ReadAllText(Path.Combine(LuaScripts.LuaPath, "default.lua"));

    [Fact]
    public void TheShippedDefaultIsWhatNewSettingsStartFrom()
    {
        var settings = LuaRenamerSettings.New(
            Substitute.For<IConfigurationService>(), Substitute.For<IPluginManager>());

        settings.Script.Should().NotBeNullOrWhiteSpace().And.Be(DefaultScript);
    }

    [Fact]
    public void ItRunsAgainstAFullyPopulatedEnvironment()
    {
        RelocationResult result = new LuaRenamer(new FakeLogger<LuaRenamer>())
            .GetPath(RelocationGraph.Populated().Context(DefaultScript));

        result.Error.Should().BeNull();
        result.FileName.Should().NotBeNullOrWhiteSpace().And.EndWith(".mp4");
        result.Path.Should().NotBeNullOrWhiteSpace();
        result.ManagedFolder.Should().NotBeNull();
    }

    [Fact]
    public void ItsFileNameCarriesTheGroupTitleAndFileInformation()
    {
        RelocationResult result = new LuaRenamer(new FakeLogger<LuaRenamer>())
            .GetPath(RelocationGraph.Populated().Context(DefaultScript));

        result.FileName.Should().StartWith("[GG] Populated Anime");
        result.FileName.Should().Contain("1080p").And.Contain("h264").And.Contain("[ABCD1234]");
        result.Path!.NormPath().Should().Be("Populated Anime");
    }
}
