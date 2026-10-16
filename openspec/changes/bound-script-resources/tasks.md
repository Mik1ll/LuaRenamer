## 1. Measure what ordinary scripts use

- [x] 1.1 With a temporary count hook (step 1) and a temporary counting allocator, run the shipped `default.lua` against `RelocationGraph`'s fully populated graph and record its instruction count and peak state memory during the run here. Remove the instrumentation afterwards
  - Instructions: **2,562** (count hook installed just before `Run`, so it includes the few `SandboxFunction` instructions around the script).
  - Memory, as the state's total (the allocator count seeded from `LUA_GCCOUNT`/`LUA_GCCOUNTB`, and matching it exactly): **172,380 B** before the run (state, libraries, trusted chunks and the translated env), **199,753 B** peak during the run (+27,373 B), 200,502 B after it.
- [x] 1.2 Record the suite's wall-clock time and `DefaultScriptTests`' time on the current code here, as the baseline for task 5.1
  - Windows 10, Debug, `dotnet test --no-build`, three runs each. Suite (315 tests): test-host duration 1.48 s / 1.63 s / 1.82 s, 1.84–2.17 s wall-clock including the `dotnet test` launcher.
  - `DefaultScriptTests` alone: 0.63 s / 0.64 s / 0.82 s, which is almost all host start-up. To make the per-relocation cost visible, a temporary test (removed) timed 500 `GetPath` calls with the default script on the populated graph after 20 warm-up calls: **1.73 / 1.92 / 2.15 ms per `GetPath`**.
- [x] 1.3 Set the limit constants to at least 100× the figures from 1.1, rounded up, and record the chosen values and the margin they give here
  - Instruction budget: **10,000,000** (≈3,900×). Instruction use varies far more between scripts than memory does (a lualinq query over a long episode or relation list runs many times the default script's count), and a runaway script still stops in a fraction of a second at this size, so the margin is set wider than the 100× floor.
  - Memory bound: **32 MiB** (33,554,432 B, ≈168× the 199,753 B peak). The bound counts the whole state, translated env included, so the margin also covers real environments larger than the fake graph's.

## 2. Instruction budget

- [x] 2.1 Add a limits argument to `LuaSandbox`'s constructor, defaulting to the constants from 1.3, and extend `SandboxFunction` so that `Run` alone installs the Lua-side count hook around `pcall(untrusted_function)` and removes it afterwards, with the stay-armed behavior from design.md. Verify the existing suite passes unchanged, and that `LoadChunk`/`CompileFunction` run without a hook installed
  - `LoadChunk` no longer goes through `Run`: it calls the run function without a budget, the same way `CompileFunction` does. `ResourceLimitTests.TrustedChunksAndCompilationRunWithoutABudget` runs a million-instruction loop as a trusted chunk and in a compiled chunk's body under a 100,000 budget.
  - Two additions to design.md's sketch, both needed so that no published name continues past the budget. (a) Once the budget is spent, the hook lets `SandboxFunction`'s own frame through (`debug.getinfo(2, 'f').func ~= run`). Otherwise the first instruction after `pcall` returns would raise outside any `pcall`, and the hook could never be removed. (b) The error object's `tostring` now runs under the hook, inside a `pcall`, because `error(setmetatable({}, { __tostring = spin }))` would otherwise spin unbudgeted after the hook was removed.
- [x] 2.2 Add `Tests/Sandbox/ResourceLimitTests.cs` with the async, `Task.Run`, sandbox-inside-the-task pattern from design.md. Cover: `while true do end`, a spin inside `pcall`, `while true do pcall(spin) end`, nested `pcall` spins, and unbounded recursion. Each test asserts that `Run` returns an error naming the instruction budget (or, for recursion, any error), under a small budget. Verify they pass
  - Also covers tail recursion (no stack growth, so only the budget stops it), `pcall(pcall, spin)` in a loop, a spinning `__tostring` on the error object, and a spinning `table.sort` comparator (script code called back from C).
- [x] 2.3 Verify the stay-armed behavior bites: temporarily make the hook error only once and confirm the `while true do pcall(spin) end` test fails at its timeout without hanging the run, then restore it
  - With the hook removing itself on exhaustion (`debug.sethook()` in place of re-arming at step 1), the full suite still finished and reported in 30.6 s. Three cases failed at their 10 s timeout: `pcall` retried forever, `pcall(pcall, spin)` retried forever, and nested pcalls. A fourth, a loop inside `pcall`, failed at once because the script then completed without error. The other 321 tests passed. After restoring it, all 325 passed.
- [x] 2.4 Comment the hook's installation with its reliance on `coroutine` being unpublished, and verify `EnvSurfaceTests.TopLevelNamesAreExactly` still lists no `coroutine`
  - **Gap found:** finalizers escape the hook. Lua 5.4 runs `__gc` metamethods with hooks disabled (`GCTM` sets `L->allowhook = 0`), and `setmetatable` is published, so `setmetatable({}, { __gc = function() while true do end end })` spins unbounded. It happens either during the run, when a GC step finalizes it, or at `Dispose`, when `lua_close` runs pending finalizers. Temporary tests (removed) confirmed both cases by timing out. A finalizer run at `lua_close` also runs with memory enforcement off. Resolved by 3.5.

## 3. Memory bound

- [x] 3.1 Install the counting allocator wrapper from design.md in `LuaSandbox`'s constructor (delegate held in a field, shrinks and frees always delegated, enforcement only during `Run`), and translate a refused allocation into an error naming the memory bound. Verify the existing suite passes unchanged
  - The wrapper seeds its count from `LUA_GCCOUNT`/`LUA_GCCOUNTB`, so the bound is on the state's whole size. Two refinements are recorded in design.md: enforcement is switched on from inside the run function, because NLua's argument pushes are unprotected, and a refusal ends the run. The suite passed unchanged (325 tests).
- [x] 3.2 Add tests to `ResourceLimitTests` for gradual growth (a table appended to in a loop) and a single oversized `string.rep`, each asserting an error naming the memory bound under a small bound. For the oversized case, assert the wrapper's count never exceeded the bound
  - Four growth cases: a local table, a global table (still reachable after the error), a doubling string, and growth retried under `pcall`. The oversized case uses `string.rep('x', 1 << 30)`, because Lua 5.4 rejects counts above `INT_MAX` itself. `PeakAllocatedBytes` stays ≤ the 4 MiB bound.
- [x] 3.3 Verify the bound applies only to the untrusted run: construct a sandbox whose bound is below the memory already used by translation and trusted chunks, confirm construction and translation succeed, and confirm a trivial script is then refused with the memory-bound error
  - `TheMemoryBoundAppliesOnlyToTheUntrustedRun`: a 64 KiB bound, with lualinq, utils and the populated env translated (peak well above 64 KiB), then `Run("x = 1")` reports the memory bound. This is also the case that would have aborted the process had enforcement been switched on before NLua pushed the arguments.
- [x] 3.4 Add a test that documents the residual gap: a CPU-heavy pattern match on a moderate subject completes (not interrupted) under a small instruction budget. Name it after the limitation so the gap is visible in the suite
  - `APatternMatchIsNotInterruptedByTheInstructionBudget`: `('a'):rep(150):find(('.-'):rep(3) .. 'b')` completes under a 1,000-instruction budget. The subject was cut from 400, which took 4 s, to 150, which takes about 0.2 s.
- [x] 3.5 Publish a `setmetatable` that raises when the metatable has a raw `__gc` field, per design.md "Finalizers are unavailable to scripts". Test that a finalizer-registering script gets the error, that a `__gc` added after `setmetatable` never runs (mid-run or at `Dispose`), and that ordinary metatables (`__index`, `__tostring`, `__call`, `__close`, clearing with `nil`) still work. Verify it bites by temporarily publishing the raw `setmetatable`
  - With the raw `setmetatable` published, `RegisteringAFinalizerIsRefused` failed at its 10 s timeout and the run still finished and reported (339 of 340 passed). After restoring, 340 of 340 passed, with `EnvSurfaceTests`, `TrustedChunkTests` (lualinq, utils) and `DefaultScriptTests` among them.

## 4. End to end

- [x] 4.1 In `Tests/Renaming`, add tests that `GetPath` with a spinning script, and with a memory-exhausting script, returns a `RelocationResult.Error` naming the limit under the default limits, and that a following `GetPath` with a normal script succeeds. Verify they pass within their timeouts
  - `Tests/Renaming/ScriptLimitTests.cs`: `while true do end` and an endlessly appended table each fail their relocation with an error naming the instruction budget or the memory bound, and a following `GetPath` on the same `LuaRenamer` renames normally. Under the shipped limits (10M instructions, 32 MiB), both cases together take about 0.3 s beyond host start-up.
- [x] 4.2 Add a test that the shipped default script completes against the fully populated graph under limits one hundredth of the defaults, and verify it passes
  - `DefaultScriptTests.ItRunsWithinAHundredthOfTheShippedLimits`: 100,000 instructions and 335,544 B, against the measured 2,562 and 199,753 B. It builds the sandbox as `GetPath` does, since `GetPath` takes no limits.

## 5. Cost check

- [x] 5.1 Re-measure the timings from 1.2 and record them here. If the allocator wrapper adds materially to `DefaultScriptTests` or the suite, apply design.md's fallback and record that decision here
  - Same machine and method as 1.2. Suite (now 343 tests): test-host duration 1.63 s / 1.75 s / 2.39 s. `DefaultScriptTests`: 0.63 s / 0.63 s / 0.66 s.
  - `GetPath` with the default script on the populated graph, A/B in one session by temporarily skipping `SetAllocFunction`. Without the wrapper: 1.76 / 1.87 / 1.87 / 1.90 / 1.91 ms. With it: 1.96 / 1.96 / 1.97 / 1.99 / 2.00 / 2.02 ms, with outliers of 2.3 to 6.0 ms from machine load. That is about 0.1 ms (~5%) per relocation, inside the 1.73 to 2.15 ms spread of the 1.2 baseline. **Not material, so the fallback was not taken.**

## 6. Hang protection

- [x] 6.1 Add `Microsoft.Testing.Extensions.HangDump` to `Tests.csproj`, regenerate `packages.lock.json`, and verify locked-mode restore succeeds
  - Version 2.3.3, matching the Microsoft.Testing.Platform 2.3.3 that xunit.v3 4.0.0 brings in. The lock file gained HangDump and its `Microsoft.Diagnostics.NETCore.Client` 0.2.607501 dependency. `dotnet restore --locked-mode --force` exits 0.
- [x] 6.2 Verify the extension works locally: add a temporary test that blocks forever, run `dotnet test --hangdump --hangdump-timeout 30s`, and confirm the run fails, a dump is written under `TestResults`, and the hang report names the blocked test. Then delete the temporary test
  - `dotnet test --project Tests --no-build --hangdump --hangdump-timeout 30s`, with a temporary `[Fact]` blocked on `ManualResetEventSlim.Wait()`. The run failed with exit code 7 after 32 s. It wrote `TestResults/LuaRenamer.Tests_<pid>_hang.dmp` (211 MB, full dump) and `..._hang.log`, which reads `[00:00:30] LuaRenamer.Tests.Sandbox.TmpHangTests.BlocksForever`. The other 343 tests passed. `TestResults` lands at the working directory, which is the repository root in CI, and `.gitignore` already covers it. The temporary test was deleted.
- [ ] 6.3 In `build-and-test.yml`, add `--hangdump --hangdump-timeout <T>` to the test step and `timeout-minutes` to the job, sized per design.md from the measured suite time, plus an `if: failure()` upload of `TestResults/**` with 7-day retention. Verify with a PR run that passes and uploads nothing

## 7. Documentation

- [ ] 7.1 In `README.md` "The Environment", state that scripts run under fixed execution and memory limits, that exceeding one is reported as the rename error naming the limit, and that long CPU-bound pattern matches are not interrupted. Also repoint that paragraph's stale `LuaContext.cs` link to `LuaEnv/LuaSandbox.cs`. Verify the link resolves on GitHub
