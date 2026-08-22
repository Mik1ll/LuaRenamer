using System;
using AwesomeAssertions;
using LuaRenamer.LuaEnv;
using Xunit;

namespace LuaRenamer.Tests.Sandbox;

public sealed class ScriptExecutionTests : IDisposable
{
    private readonly LuaSandbox _sandbox = EnvFixture.Seeded();

    public void Dispose() => _sandbox.Dispose();

    [Theory]
    [InlineData("x = 'ok'", "runs to completion")]
    [InlineData("", "empty script")]
    public void SuccessReportsNoError(string script, string why) => _sandbox.Run(script).Should().BeNull(why);

    [Theory]
    [InlineData("return (", "fails to load")]
    [InlineData("error('boom')", "throws a string")]
    [InlineData("error({})", "throws a non-string error object")]
    [InlineData("local x = nil; return x.y", "runtime error")]
    // The non-string case is the one that matters: an error object that stringified to nothing would make a
    // failed script indistinguishable from a successful one, and the renamer would then go on to read
    // whatever the env still held.
    public void FailureReportsANonEmptyMessage(string script, string why) =>
        _sandbox.Run(script).Should().NotBeNullOrWhiteSpace(why);

    [Fact]
    public void LineEndingsAllTerminateAStatement()
    {
        // Asserted at the sandbox rather than through a resulting file name: this is a property of how the
        // chunk is loaded, and nothing downstream of loading can change it.
        _sandbox.Run("x = 'a'\r\nx = 'b'\nx = 'c'\rx = 'd'").Should().BeNull();
        _sandbox.GetValue("x").Should().Be("d");
    }
}
