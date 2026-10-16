using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using LuaRenamer.LuaEnv;
using LuaRenamer.Tests.Fakes;
using Microsoft.Extensions.Logging.Testing;
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

    private const long FourMiB = 4 * 1024 * 1024;

    private static readonly ScriptLimits SmallBudget = new(Instructions: 100_000, MemoryBytes: 32 * 1024 * 1024);

    private static readonly ScriptLimits SmallMemory = new(Instructions: 100_000_000, MemoryBytes: FourMiB);

    private static Task<T> Isolated<T>(ScriptLimits limits, Func<LuaSandbox, T> body, CancellationToken token) =>
        Task.Run(() =>
        {
            using var sandbox = new LuaSandbox(limits);
            return body(sandbox);
        }, token);

    private static Task<string?> RunIsolated(string script, ScriptLimits limits, CancellationToken token) =>
        Isolated(limits, sandbox => sandbox.Run(script), token);

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
        (await RunIsolated(script, SmallBudget, TestContext.Current.CancellationToken)).Should().Contain("instruction budget", why);

    [Fact(Timeout = Timeout)]
    public async Task UnboundedRecursionIsAnError() =>
        (await RunIsolated("local function f() return 1 + f() end f()", SmallBudget, TestContext.Current.CancellationToken)).Should().NotBeNullOrWhiteSpace();

    [Fact(Timeout = Timeout)]
    public async Task TrustedChunksAndCompilationRunWithoutABudget()
    {
        const string spin = "for i = 1, 1000000 do end";
        (await Task.Run(() =>
        {
            using var sandbox = new LuaSandbox(SmallBudget, spin);
            sandbox.CompileFunction($"{spin} return function () end");
            return sandbox.Run("x = 1");
        }, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    [Theory(Timeout = Timeout)]
    [InlineData("local t = {} while true do t[#t + 1] = {} end", "a local table appended to forever")]
    [InlineData("t = {} while true do t[#t + 1] = {} end", "a global table, still reachable after the error")]
    [InlineData("local s = 'x' while true do s = s .. s end", "a string doubled forever")]
    [InlineData("local t = {} while true do pcall(function() t[#t + 1] = ('x'):rep(1000) .. #t end) end",
        "growth retried under pcall")]
    public async Task GradualGrowthStopsAtTheMemoryBound(string script, string why) =>
        (await RunIsolated(script, SmallMemory, TestContext.Current.CancellationToken)).Should().Contain("memory bound", why);

    [Fact(Timeout = Timeout)]
    public async Task AnOversizedAllocationIsRefusedWithoutBeingObtained()
    {
        (var error, var peak) = await Isolated(SmallMemory,
            sandbox => (sandbox.Run("local s = string.rep('x', 1 << 30)"), sandbox.PeakAllocatedBytes), TestContext.Current.CancellationToken);

        error.Should().Contain("memory bound");
        peak.Should().BeLessThanOrEqualTo(FourMiB);
    }

    [Fact(Timeout = Timeout)]
    public async Task TheMemoryBoundAppliesOnlyToTheUntrustedRun()
    {
        // Below what the trusted chunks and a populated env already hold.
        var limits = new ScriptLimits(Instructions: 100_000, MemoryBytes: 64 * 1024);

        (await Task.Run(() =>
        {
            using var sandbox = new LuaSandbox(limits, LuaScripts.LuaLinq, LuaScripts.Utils);
            new ModelTranslator(sandbox).Translate(
                ModelProducers.EnvToModel(RelocationGraph.Populated().Context("x = 1"), new FakeLogger<LuaRenamer>()), sandbox.Env);
            sandbox.PeakAllocatedBytes.Should().BeGreaterThan(limits.MemoryBytes);
            return sandbox.Run("x = 1");
        }, TestContext.Current.CancellationToken)).Should().Contain("memory bound");
    }

    /// <summary>
    /// <c>episode_numbers</c> is C#, and NLua pushes its long result string from inside the managed callback, so
    /// the refusal raises the memory error through .NET frames. This checks each CI platform survives that.
    /// </summary>
    [Fact(Timeout = Timeout)]
    public async Task AnAllocationRefusedInsideAHostCallbackStopsTheScript()
    {
        var limits = new ScriptLimits(Instructions: 100_000_000, MemoryBytes: 8 * 1024 * 1024);

        (await Task.Run(() =>
        {
            using var sandbox = new LuaSandbox(limits, LuaScripts.LuaLinq, LuaScripts.Utils);
            new ModelTranslator(sandbox).Translate(
                ModelProducers.EnvToModel(RelocationGraph.Populated().Context("x = 1"), new FakeLogger<LuaRenamer>()), sandbox.Env);
            return sandbox.Run("local t = {} while true do t[#t + 1] = episode_numbers(5000) end");
        }, TestContext.Current.CancellationToken)).Should().Contain("memory bound");
    }

    /// <summary>
    /// A finalizer would run with the hook off, mid-run or at <c>Dispose</c>, so this one would hang the test
    /// rather than fail it if it were ever registered.
    /// </summary>
    [Fact(Timeout = Timeout)]
    public async Task RegisteringAFinalizerIsRefused() =>
        (await RunIsolated("setmetatable({}, { __gc = function() while true do end end }) for i = 1, 10000 do local t = {} end",
            SmallBudget, TestContext.Current.CancellationToken)).Should().Contain("__gc finalizers are not available");

    [Fact(Timeout = Timeout)]
    public async Task AFinalizerAddedAfterTheMetatableIsSetNeverRuns() =>
        (await RunIsolated(
            "local mt = {} setmetatable({}, mt) mt.__gc = function() while true do end end for i = 1, 10000 do local t = {} end",
            SmallBudget, TestContext.Current.CancellationToken)).Should().BeNull();

    [Theory]
    [InlineData("local t = {} if setmetatable(t, {}) ~= t then error('not returned') end", "the table is returned")]
    [InlineData("if setmetatable({}, { __index = function(_, k) return k .. '!' end }).x ~= 'x!' then error('') end", "__index")]
    [InlineData("if tostring(setmetatable({}, { __tostring = function() return 'T' end })) ~= 'T' then error('') end", "__tostring")]
    [InlineData("if setmetatable({}, { __call = function() return 1 end })() ~= 1 then error('') end", "__call")]
    [InlineData(
        "local closed = false do local x <close> = setmetatable({}, { __close = function() closed = true end }) end if not closed then error('') end",
        "__close")]
    [InlineData("if getmetatable(setmetatable(setmetatable({}, {}), nil)) ~= nil then error('') end", "clearing with nil")]
    public void OtherMetatablesStillWork(string script, string why)
    {
        using var sandbox = new LuaSandbox();
        sandbox.Run(script).Should().BeNull(why);
    }

    /// <summary>
    /// The residual gap: the pattern matcher is C, so its backtracking executes no Lua instructions and the
    /// budget cannot interrupt it. This match backtracks through over half a million positions under a budget
    /// of a thousand instructions.
    /// </summary>
    [Fact(Timeout = Timeout)]
    public async Task APatternMatchIsNotInterruptedByTheInstructionBudget()
    {
        var limits = new ScriptLimits(Instructions: 1_000, MemoryBytes: 32 * 1024 * 1024);

        (await RunIsolated("x = ('a'):rep(150):find(('.-'):rep(3) .. 'b')", limits, TestContext.Current.CancellationToken)).Should().BeNull();
    }
}
