## Why

The sandbox limits which names a user script can reach, but not how much work it can do. `LuaSandbox` sets no instruction budget and no memory bound, and the script runs on the Shoko server's relocation thread. So `while true do end`, a mistyped loop condition, or a table that grows without end will hang that thread or exhaust the server process's memory. Nothing reports an error, and nothing recovers short of a restart. The testing-architecture spec pins the sandbox surface exactly, yet no test covers what a script does with that surface. Such a test can't be written today, because it would hang.

That same gap also affects the test run. A test that hangs, whether from a script with no limit, a deadlock, or a future regression, runs until GitHub's 6-hour job limit and leaves no indication of which test was stuck.

## What Changes

- **BREAKING (script-visible)**: bound each user script's execution with an instruction budget. A script that exhausts it stops, and the relocation reports an error naming the limit. Catching the error with the published `pcall` does not let a script continue.
- **BREAKING (script-visible)**: bound the Lua state's memory while a user script runs. An allocation that would exceed the bound fails, whether it comes from gradual growth or from one oversized request such as `string.rep` with a huge count. The relocation reports an error naming the limit.
- **BREAKING (script-visible)**: scripts can't register `__gc` finalizers. The published `setmetatable` raises when given a metatable with a `__gc` field. Lua runs finalizers with debug hooks off, and at state close outside the memory bound, so a finalizer would otherwise escape both limits.
- Limits are reset for every relocation, so a script that hit a limit doesn't affect the next one.
- Limits are fixed constants, set to at least 100× what the shipped default script uses against a fully populated environment. They are not user-configurable in this change.
- Add hostile-script tests for runaway loops, a runaway loop wrapped in `pcall`, unbounded recursion, unbounded growth, a single oversized allocation, and per-relocation reset. Each test is written so that a regression fails that test rather than hanging the run.
- Fail a hung test run promptly: a job-level timeout in CI, plus a hang dump that captures the process and names the tests in flight, uploaded as a run artifact.
- Document the limits in the README's "The Environment" section.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `script-sandbox`: adds a requirement that a script's execution and memory are bounded, that exceeding a bound is reported as the relocation's error, that a script cannot escape a bound with any published name, and that ordinary scripts are unaffected.
- `testing-architecture`: adds a requirement that a hung test run fails within a bounded time and leaves diagnostics that identify the tests in flight.

## Impact

- **Production**: `LuaEnv/LuaSandbox.cs` gains the instruction hook (installed from the trusted run function), a counting allocator wrapper, and a published `setmetatable` that refuses `__gc`. `LuaRenamer.GetPath` is unchanged: a limit surfaces as a script error through the existing path.
- **Tests**: new hostile-script tests in `Tests/Sandbox`, and an end-to-end limit test in `Tests/Renaming`.
- **CI**: `build-and-test.yml` gains `timeout-minutes`, hang-dump arguments to `dotnet test`, and an upload of dumps when a run fails.
- **Dependencies**: `Microsoft.Testing.Extensions.HangDump` in `Tests.csproj`. `packages.lock.json` is regenerated.
- **Docs**: `README.md` "The Environment".
- **Risk**: a user script that legitimately does heavy work could hit a limit. The 100× margin over the default script, and the error message naming the limit, are the mitigation.
- **Interaction with other changes**: `ci-platform-matrix` edits the same workflow file. Whichever lands second carries the other's edits across all legs.
