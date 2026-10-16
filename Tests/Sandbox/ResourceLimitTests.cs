using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using LuaRenamer.LuaEnv;
using Xunit;

namespace LuaRenamer.Tests.Sandbox;

/// <summary>
/// Scripts written to exhaust a <see cref="ScriptLimits"/> bound, run under small limits so each finishes in
/// milliseconds when the bound holds.
/// </summary>
/// <remarks>
/// A regression here means a script that never returns. xUnit only enforces <c>Timeout</c> on async tests,
/// so each one runs on its own task, against a sandbox created and disposed inside that task: if the bound
/// fails, the test times out and the spinning thread keeps a state nothing else touches.
/// </remarks>
public sealed class ResourceLimitTests
{
    private const int Timeout = 10_000;

    private static readonly ScriptLimits Small = new(Instructions: 100_000, MemoryBytes: 32 * 1024 * 1024);

    private static Task<string?> RunIsolated(string script, ScriptLimits limits, CancellationToken token) =>
        Task.Run(() =>
        {
            using var sandbox = new LuaSandbox(limits);
            return sandbox.Run(script);
        }, token);

    [Theory(Timeout = Timeout)]
    [InlineData("while true do end", "a bare loop")]
    [InlineData("local function f() return f() end f()", "tail recursion, which never grows the stack")]
    [InlineData("pcall(function() while true do end end)", "a loop inside pcall")]
    [InlineData("while true do pcall(function() while true do end end) end", "pcall retried forever")]
    [InlineData("while true do pcall(pcall, function() while true do end end) end", "pcall of pcall retried forever")]
    [InlineData(
        "pcall(function() pcall(function() pcall(function() while true do end end) end) end) while true do end",
        "nested pcalls")]
    [InlineData("error(setmetatable({}, { __tostring = function() while true do end end }))",
        "an error object whose __tostring spins")]
    [InlineData("table.sort({ 3, 2, 1 }, function() while true do end end)", "a loop in a callback from a library function")]
    public async Task ANonTerminatingScriptStopsAtTheInstructionBudget(string script, string why) =>
        (await RunIsolated(script, Small, TestContext.Current.CancellationToken)).Should().Contain("instruction budget", why);

    [Fact(Timeout = Timeout)]
    public async Task UnboundedRecursionIsAnError() =>
        (await RunIsolated("local function f() return 1 + f() end f()", Small, TestContext.Current.CancellationToken)).Should().NotBeNullOrWhiteSpace();

    [Fact(Timeout = Timeout)]
    public async Task TrustedChunksAndCompilationRunWithoutABudget()
    {
        const string spin = "for i = 1, 1000000 do end";
        (await Task.Run(() =>
        {
            using var sandbox = new LuaSandbox(Small, spin);
            sandbox.CompileFunction($"{spin} return function () end");
            return sandbox.Run("x = 1");
        }, TestContext.Current.CancellationToken)).Should().BeNull();
    }
}
