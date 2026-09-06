using AwesomeAssertions;
using LuaRenamer.DefsGenerator;
using LuaRenamer.LuaEnv.Models;
using LuaRenamer.LuaEnv.Names;
using Xunit;

namespace LuaRenamer.Tests.Env;

/// <summary>
/// The build-time emitters read the same <c>LuaSchema</c> walk the runtime serializer does. This asserts they
/// are deterministic and describe the schema they were given.
/// </summary>
/// <remarks>
/// It cannot check the emitted files against the tracked ones in <c>LuaRenamer/lua/</c>: the build's
/// <c>GenerateLuaDefs</c> target has already overwritten those from these same models, so the comparison
/// would be circular. Git is the only non-circular oracle, which is what CI's "Verify generated Lua defs are
/// current" step uses.
/// </remarks>
public class GeneratedArtifactTests
{
    [Fact]
    public void DefsEmissionIsDeterministicAndDescribesTheSchema()
    {
        var generator = new ModelDefsGenerator();
        string[] first = [generator.GenerateDefs(), ModelDefsGenerator.GenerateEnums(), generator.GenerateEnv()];
        string[] second = [generator.GenerateDefs(), ModelDefsGenerator.GenerateEnums(), generator.GenerateEnv()];

        first.Should().Equal(second, "generator output is not deterministic");
        first.Should().OnlyContain(text => text.StartsWith("---@meta", System.StringComparison.Ordinal));

        first[0].Should().Contain($"---@class (exact) {nameof(AnimeModel).Replace("Model", "", System.StringComparison.Ordinal)}");
        first[1].Should().Contain($"---@enum {nameof(EnvModel.Language)}");
        first[2].Should().Contain(nameof(EnvModel.filename));
    }

    [Fact]
    public void NamesEmissionIsDeterministicAndRoundTripsThroughTheEmittedSource()
    {
        var generator = new ModelNamesGenerator();

        generator.GenerateNames().Should().Be(generator.GenerateNames(), "generator output is not deterministic");
        // The DSL referenced here was emitted into LuaRenamer by this same generator, so naming the type
        // through it is what checks that the two agree about the schema.
        generator.GenerateNames().Should().Contain($"public sealed class {nameof(EnvNames)} :");
    }
}
