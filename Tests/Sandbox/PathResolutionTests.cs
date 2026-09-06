using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using LuaRenamer.LuaEnv;
using NLua;
using Xunit;

namespace LuaRenamer.Tests.Sandbox;

/// <summary>
/// <see cref="LuaSandbox.GetValue(string)"/> is total over the path space: every input either resolves,
/// returns no value, or raises a path-validation error. Nothing here builds a host graph — the fixture is
/// seeded as plain Lua tables (see <see cref="EnvFixture"/>).
/// </summary>
public sealed class PathResolutionTests : IDisposable
{
    private readonly LuaSandbox _sandbox = EnvFixture.Seeded();

    public void Dispose() => _sandbox.Dispose();

    #region Generated path spaces

    /// <summary>A path the fixture makes resolvable.</summary>
    public sealed record WellFormed(string Path, EnvFixture.Node Node);

    /// <summary>A well-formed path that names nothing.</summary>
    public sealed record NamesNothing(string Path);

    /// <summary>A path outside the accepted grammar.</summary>
    public sealed record Malformed(string Path);

    public static class Paths
    {
        public static Arbitrary<WellFormed> WellFormedPaths() =>
            Gen.Elements(EnvFixture.AllPaths.Select(p => new WellFormed(p.Path, p.Node))).ToArbitrary();

        public static Arbitrary<NamesNothing> PathsNamingNothing() =>
            Gen.Elements(
            [
                // An absent key on a table that does exist.
                .. EnvFixture.MapPaths.Select(p => new NamesNothing(p + ".nosuchkey")),
                // An index past the end of a sequence that does exist.
                .. EnvFixture.SeqPaths.Select(p => new NamesNothing(FormattableString.Invariant($"{p}[9999]"))),
                // Traversal through a scalar leaf, by name and by index.
                .. EnvFixture.LeafPaths.Select(p => new NamesNothing(p + ".nosuchkey")),
                .. EnvFixture.LeafPaths.Select(p => new NamesNothing(p + "[1]")),
                new NamesNothing("nosuchglobal"),
            ]).ToArbitrary();

        public static Arbitrary<Malformed> MalformedPaths() =>
            Gen.Elements(EnvFixture.AllPaths.Select(p => p.Path))
                .SelectMany(p => Gen.Elements(Malformations(p)))
                .Select(p => new Malformed(p))
                .ToArbitrary();

        private static IEnumerable<string> Malformations(string path)
        {
            yield return "";                          // empty path
            yield return "." + path;                  // leading dot
            yield return path + ".";                  // trailing dot
            yield return path + "[1";                 // unclosed bracket
            yield return path + "[x]";                // non-numeric index
            yield return path + "[]";                 // empty index
            yield return path + "[1]x";               // trailing content after an index
            yield return path + ":getname(Language.English)"; // a call, not a value
            yield return path + "(2)";                // likewise
            if (path.Contains('.', StringComparison.Ordinal))
                yield return path.Replace(".", "..", StringComparison.Ordinal); // empty segment
        }
    }

    #endregion

    [Property(Arbitrary = [typeof(Paths)])]
    public void WellFormedPathsResolve(WellFormed path)
    {
        var value = _sandbox.GetValue(path.Path);
        if (path.Node is EnvFixture.Leaf leaf)
            value.Should().Be(leaf.Value);
        else
            value.Should().BeOfType<LuaTable>();
    }

    [Property(Arbitrary = [typeof(Paths)])]
    public void PathsNamingNothingReturnNoValue(NamesNothing path) =>
        _sandbox.GetValue(path.Path).Should().BeNull();

    [Property(Arbitrary = [typeof(Paths)])]
    public void MalformedPathsRaiseAPathValidationError(Malformed path)
    {
        // ArgumentException specifically: an index or reference error would mean the walk started before the
        // path was validated, which is what makes a bad path report as a Lua failure instead of a bad path.
        Exception thrown = FluentActions.Invoking(() => _sandbox.GetValue(path.Path))
            .Should().Throw<ArgumentException>().Which;
        thrown.Should().NotBeOfType<ArgumentNullException>();
        thrown.Should().NotBeOfType<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("", "empty path")]
    [InlineData("anime..type", "empty segment")]
    [InlineData(".anime", "leading dot")]
    [InlineData("anime.", "trailing dot")]
    [InlineData("anime[1", "unclosed bracket")]
    [InlineData("anime[x]", "non-numeric index")]
    [InlineData("anime[]", "empty index")]
    [InlineData("anime[1]x", "trailing junk after an index")]
    [InlineData("anime:getname(Language.English)", "method call")]
    [InlineData("episode_numbers(2)", "function call")]
    public void RecordedMalformedPaths(string path, string why) =>
        FluentActions.Invoking(() => _sandbox.GetValue(path))
            .Should().Throw<ArgumentException>(why);

    [Fact]
    public void ResolvesInteriorNodesAndScalarsWithoutCoercion()
    {
        // No long->double coercion; that lives only in NLua's Lua.this[string], which GetValue deliberately avoids.
        _sandbox.GetValue("alpha.num").Should().Be(7L);
        _sandbox.GetValue("alpha.flag").Should().Be(true);
        _sandbox.GetValue("gamma").Should().Be("top-leaf");
        _sandbox.GetValue("alpha.inner.items[2].name").Should().Be("item-two");
        _sandbox.GetValue("beta.grid[1][1].name").Should().Be("deep");
        _sandbox.GetValue("alpha.inner").Should().BeOfType<LuaTable>();
    }
}
