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
  - **Gap found (open, needs a decision):** finalizers escape the hook. Lua 5.4 runs `__gc` metamethods with hooks disabled (`GCTM` sets `L->allowhook = 0`), and `setmetatable` is published, so `setmetatable({}, { __gc = function() while true do end end })` spins unbounded. It happens either during the run, when a GC step finalizes it, or at `Dispose`, when `lua_close` runs pending finalizers. Temporary tests (removed) confirmed both cases by timing out. A finalizer run at `lua_close` also runs with memory enforcement off. See design.md, Open Questions.

## 3. Memory bound

- [ ] 3.1 Install the counting allocator wrapper from design.md in `LuaSandbox`'s constructor (delegate held in a field, shrinks and frees always delegated, enforcement only during `Run`), and translate a refused allocation into an error naming the memory bound. Verify the existing suite passes unchanged
- [ ] 3.2 Add tests to `ResourceLimitTests` for gradual growth (a table appended to in a loop) and a single oversized `string.rep`, each asserting an error naming the memory bound under a small bound. For the oversized case, assert the wrapper's count never exceeded the bound
- [ ] 3.3 Verify the bound applies only to the untrusted run: construct a sandbox whose bound is below the memory already used by translation and trusted chunks, confirm construction and translation succeed, and confirm a trivial script is then refused with the memory-bound error
- [ ] 3.4 Add a test that documents the residual gap: a CPU-heavy pattern match on a moderate subject completes (not interrupted) under a small instruction budget. Name it after the limitation so the gap is visible in the suite

## 4. End to end

- [ ] 4.1 In `Tests/Renaming`, add tests that `GetPath` with a spinning script, and with a memory-exhausting script, returns a `RelocationResult.Error` naming the limit under the default limits, and that a following `GetPath` with a normal script succeeds. Verify they pass within their timeouts
- [ ] 4.2 Add a test that the shipped default script completes against the fully populated graph under limits one hundredth of the defaults, and verify it passes

## 5. Cost check

- [ ] 5.1 Re-measure the timings from 1.2 and record them here. If the allocator wrapper adds materially to `DefaultScriptTests` or the suite, apply design.md's fallback and record that decision here

## 6. Hang protection

- [ ] 6.1 Add `Microsoft.Testing.Extensions.HangDump` to `Tests.csproj`, regenerate `packages.lock.json`, and verify locked-mode restore succeeds
- [ ] 6.2 Verify the extension works locally: add a temporary test that blocks forever, run `dotnet test --hangdump --hangdump-timeout 30s`, and confirm the run fails, a dump is written under `TestResults`, and the hang report names the blocked test. Then delete the temporary test
- [ ] 6.3 In `build-and-test.yml`, add `--hangdump --hangdump-timeout <T>` to the test step and `timeout-minutes` to the job, sized per design.md from the measured suite time, plus an `if: failure()` upload of `TestResults/**` with 7-day retention. Verify with a PR run that passes and uploads nothing

## 7. Documentation

- [ ] 7.1 In `README.md` "The Environment", state that scripts run under fixed execution and memory limits, that exceeding one is reported as the rename error naming the limit, and that long CPU-bound pattern matches are not interrupted. Also repoint that paragraph's stale `LuaContext.cs` link to `LuaEnv/LuaSandbox.cs`. Verify the link resolves on GitHub
