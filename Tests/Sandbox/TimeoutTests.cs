using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using LuaRenamer.LuaEnv;
using Xunit;

namespace LuaRenamer.Tests.Sandbox;

/// <summary>
/// Scripts that never finish on their own, run under a one-second timeout.
/// </summary>
/// <remarks>
/// A regression here means a script that never returns. xUnit only enforces <c>Timeout</c> on async tests, so
/// each runs on its own task, against a sandbox created and disposed inside it: if the timeout fails, the test
/// fails and the spinning thread keeps a state nothing else touches.
/// </remarks>
public sealed class TimeoutTests
{
    private const int Timeout = 10_000;

    private static Task<string?> RunIsolated(string script, CancellationToken token) =>
        Task.Run(() =>
        {
            using var sandbox = new LuaSandbox { TimeoutSeconds = 1 };
            return sandbox.Run(script);
        }, token);

    [Theory(Timeout = Timeout)]
    [InlineData("while true do end", "a bare loop")]
    [InlineData("while true do pcall(function() while true do end end) end", "pcall retried forever")]
    [InlineData("error(setmetatable({}, { __tostring = function() while true do end end }))",
        "an error object whose __tostring spins")]
    [InlineData("table.sort({ 3, 2, 1 }, function() while true do end end)", "a loop in a callback from a library function")]
    public async Task AScriptThatNeverFinishesIsStopped(string script, string why) =>
        (await RunIsolated(script, TestContext.Current.CancellationToken)).Should().Contain("timed out after 1 seconds", why);

    [Fact]
    public void ScriptsGetFifteenSecondsByDefault()
    {
        using var sandbox = new LuaSandbox();
        sandbox.TimeoutSeconds.Should().Be(15);
    }
}
