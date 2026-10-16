## 1. Measure what ordinary scripts use

- [ ] 1.1 With a temporary count hook (step 1) and a temporary counting allocator, run the shipped `default.lua` against `RelocationGraph`'s fully populated graph and record its instruction count and peak state memory during the run here. Remove the instrumentation afterwards
- [ ] 1.2 Record the suite's wall-clock time and `DefaultScriptTests`' time on the current code here, as the baseline for task 5.1
- [ ] 1.3 Set the limit constants to at least 100× the figures from 1.1, rounded up, and record the chosen values and the margin they give here

## 2. Instruction budget

- [ ] 2.1 Add a limits argument to `LuaSandbox`'s constructor, defaulting to the constants from 1.3, and extend `SandboxFunction` so that `Run` alone installs the Lua-side count hook around `pcall(untrusted_function)` and removes it afterwards, with the stay-armed behavior from design.md. Verify the existing suite passes unchanged, and that `LoadChunk`/`CompileFunction` run without a hook installed
- [ ] 2.2 Add `Tests/Sandbox/ResourceLimitTests.cs` with the async, `Task.Run`, sandbox-inside-the-task pattern from design.md. Cover: `while true do end`, a spin inside `pcall`, `while true do pcall(spin) end`, nested `pcall` spins, and unbounded recursion. Each test asserts that `Run` returns an error naming the instruction budget (or, for recursion, any error), under a small budget. Verify they pass
- [ ] 2.3 Verify the stay-armed behavior bites: temporarily make the hook error only once and confirm the `while true do pcall(spin) end` test fails at its timeout without hanging the run, then restore it
- [ ] 2.4 Comment the hook's installation with its reliance on `coroutine` being unpublished, and verify `EnvSurfaceTests.TopLevelNamesAreExactly` still lists no `coroutine`

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
