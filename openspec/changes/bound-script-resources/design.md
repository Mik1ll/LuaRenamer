## Context

See proposal.md for the motivation. The current state that shapes the approach:

- `LuaSandbox.Run` calls a trusted Lua function (`SandboxFunction`) that `load`s the user chunk against `env` and `pcall`s it. That function runs with the real globals, so `debug` is reachable from it, while `env` publishes neither `debug` nor `coroutine`. `EnvSurfaceTests.TopLevelNamesAreExactly` already pins that.
- The same `_runSandboxed` function also loads the trusted chunks (`LoadChunk`) and compiles the static `getname` functions (`CompileFunction`). Limits must apply to the untrusted run only.
- `LuaRenamer.GetPath` builds a fresh `LuaSandbox` per relocation and disposes it afterwards, so per-relocation reset comes free if the limits are per-sandbox.
- APIs available: NLua 1.7.9 exposes `SetDebugHook(LuaHookMask, int)` with a managed `DebugHook` event. KeraLua 1.4.10 exposes `SetHook`, `GetAllocFunction` and `SetAllocFunction`.

## Goals / Non-Goals

**Goals**
- No script can hold the relocation thread or the process's memory indefinitely, by any route through the published surface.
- A limit appears as an ordinary script error. `GetPath`'s error path needs no new case.

**Non-Goals**
- User-configurable limits. They are fixed constants for now. If users report legitimate scripts hitting them, adding a setting is a separate change.
- Wall-clock limits. Instruction counting is deterministic, so a test can name the exact behavior. A wall-clock bound would make the same script pass or fail depending on machine load.
- CPU time spent inside a single C library call. See Risks.

## Decisions

### Instruction budget: a Lua-side count hook, installed by the trusted run function

`SandboxFunction` takes an optional budget. When one is given, it calls `debug.sethook(hook, "", step)` immediately before `pcall(untrusted_function)` and `debug.sethook()` immediately after. `hook` is a Lua closure that subtracts `step` from the remaining budget and calls `error(...)` once the budget is exhausted.

The hook is a **Lua** function, not a managed `DebugHook` handler, for safety. Raising a Lua error from a managed callback means `lua_error` longjmp-ing across .NET frames, which is undefined behavior and crashes on some platforms. Raising it from a Lua hook is an ordinary Lua error that `pcall` handles the normal way.

`step` is 1,000 instructions, so the hook costs one call per thousand instructions and the overhead is negligible. The budget is accurate to within that step, which is far finer than any budget worth setting.

*Alternative considered:* NLua's `SetDebugHook` plus the `DebugHook` event, calling `State.Error` from the handler. Rejected because of the longjmp hazard above.

### The hook stays armed after exhaustion

A budget that only errors once can be defeated:

```lua
while true do pcall(function() while true do end end) end
```

The inner `pcall` catches the error and the outer loop carries on. So when the budget is exhausted, the hook re-installs itself with `step = 1` and errors on *every* instruction from then on. When the inner `pcall` returns, the very next instruction in the enclosing Lua function raises, and the error escapes one level for each enclosing function until it reaches `SandboxFunction`'s own `pcall`. Nesting only adds levels to unwind; it doesn't give the script any more work.

*Alternative considered:* wrapping the published `pcall` so it re-raises a limit sentinel. Rejected. It changes a published function's identity and behavior, and the sentinel is a value a script can inspect or forge.

The hook is per Lua thread. That is safe only because `coroutine` is unpublished, so a script can't create a thread the hook doesn't cover. The existing exact surface pin already guards this. A comment at the hook installation records the dependency, so anyone publishing `coroutine` later sees what it breaks.

### Memory bound: a counting allocator wrapped around the state's own

At construction, `LuaSandbox` reads the state's allocator with `GetAllocFunction` and installs a wrapper with `SetAllocFunction`. The wrapper keeps a running byte count from `osize`/`nsize` and delegates every call to the original allocator.

While enforcement is on, the wrapper refuses a request that would push the count over the bound by returning `NULL`, without calling the original. Lua turns that into a `LUA_ERRMEM` "not enough memory" error, which `pcall` catches like any other. Shrinks and frees are always delegated, since Lua requires them to succeed. `Run` turns a memory error raised during an enforced run into a message naming the bound. It knows the error was the bound's because the wrapper records a refusal.

Enforcement covers only the untrusted run, but the count is the state's total. The bound is therefore absolute, and environment translation and trusted chunk loading are never refused. The wrapper delegate is held in a field so the GC can't collect it while native code holds the pointer.

The wrapper delegates rather than allocating itself because the state's existing blocks came from the native library's allocator. On Windows that may be a statically linked CRT heap that `NativeMemory` doesn't share, and freeing a block on the wrong heap corrupts memory.

*Alternatives considered:*
- Checking `collectgarbage("count")` from the instruction hook. Rejected. It sees only what has already been allocated, so a single `string.rep('x', 2^33)` inside one C call commits (on Linux, overcommits) and fills 8 GiB before any hook runs.
- Constructing the state on KeraLua's `Lua(LuaAlloc, IntPtr)` from the start and wrapping it with NLua's `Lua(KeraLua.Lua)` constructor. This would work, but it moves library opening and state ownership into `LuaSandbox`, for no benefit over wrapping the existing allocator.

### Limit values come from a measurement

Task 1 measures the shipped `default.lua` against a fully populated environment: the instructions it executes, and the peak state memory during its run. The constants are set to at least 100× those figures, rounded up to round numbers.

`LuaSandbox` takes the limits as an optional constructor argument that defaults to those constants. That lets the ordinary-scripts scenario run the default script under one hundredth of the limits, and lets hostile-script tests use small limits so they finish in milliseconds.

### Hostile-script tests can't hang the run

xUnit v3 enforces `Timeout` only on async tests. Each hostile-script test is therefore `async` with a `Timeout`, and runs its script on `Task.Run` against a sandbox created and disposed **inside** that task. If a regression makes the script spin, the test fails at its timeout, and the spinning thread keeps a state nothing else touches.

Disposing a test-class-owned sandbox from `Dispose()` while another thread is still executing in it would close the native state under a running interpreter, which is why these tests don't use one.

### Run-level hang protection: HangDump plus a job timeout

`Microsoft.Testing.Extensions.HangDump` is added to `Tests.csproj`, and CI runs `dotnet test` with `--hangdump --hangdump-timeout <T>`. On a hang, it writes a process dump and the list of tests in flight to `TestResults`, and the workflow uploads `TestResults/**` with `if: failure()`.

`T` is set comfortably above the full suite's measured wall-clock time on the slowest leg, so it bounds the whole run whichever way the extension measures it. The job also gets `timeout-minutes` above `T`, as the backstop for hangs outside the test host, such as restore or build.

The hang-dump arguments go on the CI command line rather than in project configuration. Local runs and Stryker's runs (`add-mutation-testing`), which manage their own timeouts, are unaffected.

## Risks / Trade-offs

- **CPU time inside one C call isn't bounded** → Lua's pattern matcher, `table.sort` without a comparator, and `table.concat` execute no Lua instructions, so the count hook can't interrupt them. Memory-heavy cases are covered by the allocator bound. CPU-heavy pattern matching (e.g. many `.-` against a long subject) is the residual gap. It's accepted, recorded in a test that documents the limit by name, and noted in the README. Closing it would mean wrapping the published string functions, which is a separate change.
- **Per-allocation managed transitions slow every script** → Each allocation crosses into managed code and back out to the native allocator. Task 5 measures suite and default-script timing before and after, and records it. If the cost is material, the fallback is enforcing through the hook-time `collectgarbage("count")` check for gradual growth, with the allocator wrapper kept only to refuse single oversized requests (a size threshold check with no running count).
- **A legitimate heavy script hits a limit** → The 100× margin, plus an error that names the limit, so the user knows what happened. Configurable limits are the follow-up if this happens in practice.
- **`step = 1` after exhaustion is slow for scripts unwinding deep `pcall` nests** → Each level costs a few instructions once the budget is gone. Hook calls per level are bounded by nesting depth, which Lua's C stack limit (about 200 levels) bounds in turn.
- **Hang dumps are large** → Uploaded only on failure, with a short artifact retention (7 days).
