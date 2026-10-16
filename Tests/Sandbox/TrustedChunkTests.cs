using System;
using AwesomeAssertions;
using LuaRenamer.LuaEnv;
using NLua;
using Xunit;

namespace LuaRenamer.Tests.Sandbox;

/// <summary>
/// The shipped chunks (<c>utils.lua</c>, <c>lualinq.lua</c>) reach a user script through <see cref="LuaSandbox.Env"/>.
/// lualinq is vendored and unmodified, so its coverage is deliberately "it loaded and its entry points resolve",
/// not an enumeration of what its operators do. Trusted Lua that fails, whether a chunk or a callable's source,
/// fails the host call that loads it.
/// </summary>
public sealed class TrustedChunkTests : IDisposable
{
    private readonly LuaSandbox _sandbox = new(LuaScripts.LuaLinq, LuaScripts.Utils);

    public void Dispose() => _sandbox.Dispose();

    [Fact]
    public void StringHelpersResolveThroughMethodCallSyntax()
    {
        // utils.lua defines `function string:cleanspaces` into env.string. A method call on a string value
        // resolves through the string metatable, which the sandbox points at env.string in its constructor —
        // so this is the check that the bridge is in place, not that the helper's body is correct.
        _sandbox.Run("cleaned = ('  a   b  '):cleanspaces()").Should().BeNull();
        _sandbox.GetValue("cleaned").Should().Be("a b");

        _sandbox.Run("shortened = ('abcdef'):truncate(3)").Should().BeNull();
        _sandbox.GetValue("shortened").Should().BeOfType<string>();
    }

    [Fact]
    public void VendoredLibraryChunkLoaded() =>
        _sandbox.GetValue("from").Should().BeOfType<LuaFunction>();

    [Fact]
    public void VendoredLibraryEntryPointsResolveAndReturn()
    {
        _sandbox.Run("result = from({ 'a', 'b' }):toArray()").Should().BeNull();
        _sandbox.GetValue("result").Should().BeOfType<LuaTable>();
    }

    [Fact]
    public void VendoredLibraryConstructorsAreReachable()
    {
        foreach (var entry in new[] { "fromArray", "fromDictionary", "fromIterator", "fromNothing", "fromSet", "linqSetLogLevel" })
            _sandbox.GetValue(entry).Should().BeOfType<LuaFunction>(entry);
    }

    [Theory]
    [InlineData("return (", "fails to load")]
    [InlineData("error('boom')", "throws")]
    public void ATrustedChunkThatFailsFailsConstruction(string chunk, string why) =>
        FluentActions.Invoking(() => new LuaSandbox(chunk))
            .Should().Throw<ArgumentException>(why).WithMessage("trusted chunk failed to load: ?*");

    [Fact]
    public void ACallableSourceThatDoesNotCompileReportsTheLuaError() =>
        _sandbox.Invoking(s => s.CompileFunction("return function ("))
            .Should().Throw<ArgumentException>().WithMessage("chunk did not compile: *return function (*");

    [Fact]
    public void ACallableSourceThatReturnsANonFunctionIsRejected() =>
        _sandbox.Invoking(s => s.CompileFunction("return 42"))
            .Should().Throw<ArgumentException>().WithMessage("chunk did not return a function*");
}
