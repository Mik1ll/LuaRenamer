## 1. Spike: can Stryker drive this suite

- [x] 1.1 Add Stryker.NET (latest release, ≥ 4.13) as a local tool in `.config/dotnet-tools.json` and verify `dotnet tool restore` then `dotnet stryker --version` succeed
  - Stryker.NET 5.0.0 (released 2026-09-11, the latest). It went into the existing root `dotnet-tools.json` beside `shoko.buildtools`, not a second manifest under `.config/`. `dotnet tool restore` restores it.
  - In 5.0.0, `--version` (`-v`) takes a value: it sets the project version for the dashboard reporter and doesn't print the tool's version. `dotnet stryker --version` therefore fails with "Missing value for option 'version'". The install was verified another way: `dotnet tool list --local` lists `dotnet-stryker 5.0.0`, `dotnet stryker --help` runs, and every run's banner prints `Version: 5.0.0`.
- [x] 1.2 Run `dotnet stryker --test-runner mtp` against `LuaEnv` alone and verify the initial test run discovers the same test count `dotnet test` reports and passes. Record the Stryker version, mutant count, and wall-clock time here
  - The MTP runner drives the suite. `dotnet test` reports 315 tests, and Stryker's initial run finds 315 and passes, then captures per-test coverage for all 315. 5.0.0 added `perTest` coverage to the MTP runner, so design.md's "every covered mutant runs the full suite" no longer holds: each mutant runs only the tests that cover it.
  - Two blockers stood in the way first, both outside the runner itself:
    - `-p LuaEnv.csproj` run from `Tests/` (test-project mode) can't find `LuaEnv`, because `Tests` only reaches it through `LuaRenamer`. Solution mode (`-s LuaRenamer.slnx`, from the repo root) is needed.
    - Stryker analyzes each project with a design-time `Clean;Build` that skips the compiler. That `Clean` deleted `DefsGenerator`'s assembly, so `LuaRenamer`'s design-time `GenerateLuaNames` exited 1 and `LuaRenamer` was never analyzed. Fixed in `Directory.Build.props`: design-time builds get their own `CleanFile`, so they never delete what a real build wrote. Real builds and `dotnet clean` are unaffected.
  - `LuaEnv` alone (`-s LuaRenamer.slnx -p LuaEnv.csproj`, Stryker 5.0.0): 111 mutants, 69 of them tested (20 compile errors, 13 without coverage, 9 ignored). Of those, 55 killed, 12 survived and 2 timed out, a score of 69.51%. Wall clock 29 s on a 20-thread desktop at Stryker's default concurrency of 10.
- [x] 1.3 If 1.2 fails, apply the VSTest fallback from design.md, verify `dotnet test` still runs under MTP with an unchanged test count, verify Stryker's initial run passes, and record which runner was used and why
  - Not needed: the MTP runner worked (1.2), so no VSTest adapter was added.
- [x] 1.4 From 1.2's numbers, estimate a full two-project run's duration on a hosted runner and record it. If it exceeds about 5 hours, record which mitigation from design.md's Risks applies
  - A full two-project run (`LuaEnv` + `LuaRenamer`) creates 765 mutants and tests 368. It took 66 s wall clock locally, about 46 s of that testing mutants at concurrency 10. `ubuntu-latest` has 4 vCPUs, so Stryker would pick concurrency 2. At about half the per-core speed, mutant testing would take about 46 s × 5 × 2 ≈ 8 minutes, plus about a minute to build and analyze. Estimate: under 15 minutes. That is far below 5 hours, so no mitigation is needed.

## 2. Configuration

- [x] 2.1 Add `stryker-config.json` in solution mode over `LuaEnv` and `LuaRenamer`, with the `**/*.g.cs` `mutate` exclusion and the runner chosen in group 1. Verify a run's report lists no files under `DefsGenerator` and no `LuaNames.g.cs`
  - Deviation: `stryker-config.json` sits at the repo root in solution mode over `LuaRenamer.slnx`, and it excludes `DefsGenerator` by file (`!**/DefsGenerator/**/*.cs`) rather than by project. Stryker 5.0.0 can't limit a solution-mode run to a subset of projects:
    - `project` is a single case-insensitive substring of each project's full path. The checkout directory is itself named `LuaRenamer`, so no substring separates `LuaEnv` and `LuaRenamer` from `DefsGenerator`.
    - A `.slnf` is rejected with "Given path is not a solution file".
    - A two-project `.slnx` can't sit at the root, because `dotnet build`/`dotnet test` without arguments would then fail with MSB1011. In a subfolder, its `../` project paths miss Stryker's per-project configuration lookup, which combines paths without normalizing them. The projects then build with `Platform=Any CPU` into `bin/Any CPU/`, and Stryker finds 0 tests. Solution mode also has to run from the solution's own directory.
  - Verified: the report lists `DefsGenerator`'s files, but every mutant in them is `Ignored` ("Removed by mutate filter") or `CompileError`, and neither status counts toward the score. `LuaNames.g.cs` doesn't appear at all. The tested-mutant count, 368, is exactly what a run over a solution holding only `LuaEnv`, `LuaRenamer` and `Tests` produced.
- [x] 2.2 Set `thresholds.break` to 0 and verify a run with survivors exits successfully
  - Verified: a full run with 115 survivors exits 0.
- [x] 2.3 Verify `git status` is clean after a full run, i.e. the build targets didn't rewrite the tracked Lua defs
  - Verified: after a full run, the working tree shows only this change's own edits, and `LuaRenamer/lua/` is untouched. Stryker gives `StrykerOutput/` its own `*` ignore file, and the root ignore file now lists `StrykerOutput/` as well.

## 3. Scheduled workflow

- [ ] 3.1 Add `.github/workflows/mutation.yml` on `workflow_dispatch` and a weekly `schedule`, using the same restore/cache steps as `Build and Test`, running Stryker, and uploading `StrykerOutput/**/reports/` as an artifact. Verify by a manual dispatch that produces a downloadable HTML report
  - Added. actionlint 1.7.7 reports nothing on it. The job's commands were run locally in order with `CI=true`: `dotnet restore --locked-mode`, `dotnet tool restore`, then `dotnet stryker`. The run exited 0 and wrote `StrykerOutput/<timestamp>/reports/mutation-report.html` and `.json`, which the upload step's glob matches.
  - Still open: the manual-dispatch check. GitHub offers `workflow_dispatch`, and fires `schedule`, only for workflows that exist on the default branch, and `mutation.yml` reaches `master` only when this change merges. Once it does, dispatch it on `master`, confirm the `mutation-report` artifact holds the HTML report, and record the run here. The workflow has no other triggers, by design.
- [x] 3.2 Check whether Dependabot's `nuget` ecosystem updates `.config/dotnet-tools.json` and record the answer. If it doesn't, note that in the manifest's commit message
  - No. The manifest is the root `dotnet-tools.json` (see 1.1). It already pins `shoko.buildtools` 0.5.1, and 0.5.2 was published on 2026-10-13. Dependabot's `nuget` runs on 10-14 and 10-15 opened package bumps but none for the tool. The manifest's commit ("Add Stryker.NET 5.0.0 as a local tool") says `dotnet-stryker` is bumped by hand. Moving the manifest to `.config/` might change this but is untested, and it would also move `shoko.buildtools`, which the release workflow restores.

## 4. Baseline and sandbox-boundary triage

- [x] 4.1 Record the baseline mutation score and the survivor counts for each file here
  - Baseline, a full `dotnet stryker` run on `4054967` + this change's tooling, before any triage. Score **63.20%**: 246 killed, 117 survived, 3 timeouts, 2 runtime errors and 28 with no coverage. A further 89 compile errors and 35 ignored mutants in `LuaEnv`/`LuaRenamer` don't count toward the score. Other full runs on the same code scored 63.71% (115 survivors). See 4.4 for that spread.
  - Survivors per file (no-coverage mutants in parentheses):

    | File | Survived | No coverage |
    |---|---|---|
    | `LuaRenamer/ModelProducers.cs` | 44 | 1 |
    | `LuaRenamer/LuaRenamer.cs` | 23 | 14 |
    | `LuaRenamer/FilePathCleaner.cs` | 18 | 0 |
    | `LuaRenamer/LuaScripts.cs` | 12 | 0 |
    | `LuaEnv/LuaSandbox.cs` | 10 | 5 |
    | `LuaRenamer/Utils.cs` | 4 | 0 |
    | `LuaEnv/ILuaModel.cs` | 3 | 0 |
    | `LuaRenamer/LuaRenamerSettings.cs` | 1 | 0 |
    | `LuaEnv/LuaFieldAttribute.cs` | 1 | 0 |
    | `LuaEnv/Names/NamesNode.cs` | 1 | 8 |
    | `LuaEnv/ModelTranslator.cs` | 0 | 0 |

  - After the triage below, a full run scores **66.92%** (258 killed, 106 survived, 23 with no coverage). Every remaining survivor is outside the sandbox boundary.
- [x] 4.2 Triage every survivor in `LuaEnv/LuaSandbox.cs`. For each one, either add a test (in the existing `Tests/Sandbox` file for that behavior) that kills it and names the behavior, or add a `// Stryker disable once <mutator> : <reason>` at the site. List each survivor and how it was resolved here
  - No-coverage mutants were triaged too. Each is a change no test notices, which is what the spec means by a survivor. Line numbers are from before the change.
  - `Run` (109), `?? "unknown error"` dropped (NullCoalescing) and `"unknown error"` → `""` (String), both without coverage: **annotated**. The run function returns either `load`'s message or `tostring`'s result, so `error` is always a string and the fallbacks can't run.
  - `CompileFunction` (123), `[not true, …]` → `[not false, …]` (Boolean). This swaps which failure message a bad source gets: **killed** by `TrustedChunkTests.ACallableSourceThatDoesNotCompileReportsTheLuaError` and `ACallableSourceThatReturnsANonFunctionIsRejected`.
  - `CompileFunction` (123), the "chunk did not compile" message emptied, no coverage: **killed** by `ACallableSourceThatDoesNotCompileReportsTheLuaError`. That test also requires the Lua error to be in the message.
  - `CompileFunction` (124), the "chunk did not return a function" message emptied, no coverage: **killed** by `ACallableSourceThatReturnsANonFunctionIsRejected`.
  - `GetValue` (150), `table.Dispose()` removed (Statement): **killed** by `PathResolutionTests.ResolvingAPathReleasesTheTablesItWalksThrough`. It resolves a four-table-deep path 100 times and requires the Lua registry not to grow.
  - `ParseKeys` (174, 177, 184, 186, 193), each of the five path-validation messages emptied (String). Also 186, the offending index text sliced from `i - 1` instead of `i + 1` (Arithmetic). All **killed** by `RecordedMalformedPaths`, which now takes the expected report for each malformed path and requires the message to name the problem and quote the path.
  - `ParseKeys` (176), `IndexOfAny(…) >= 0` → `> 0` (Equality). This only matters when the bad character starts a segment: **killed** by the new `RecordedMalformedPaths` case `":getname()"` (a call with no receiver).
  - `ParseKeys` (183), `close < 0` → `close <= 0` (Equality): **annotated**. A non-empty name precedes every `[`, so `i`, and with it `close`, is at least 1, and the two comparisons agree.
  - `ParseKeys` (193), the "expected '.' after an index" `throw` removed (Statement). With the existing `anime[1]x` case the walk still threw, only for an empty segment: **killed** by the new case `anime[1]name`, which the mutant would accept.
  - `LoadChunk` (210), the `throw` for a failing trusted chunk removed, no coverage: **killed** by `TrustedChunkTests.ATrustedChunkThatFailsFailsConstruction` (a chunk that fails to load, and one that throws).
  - Two scoped runs (`--mutate "**/LuaEnv/LuaSandbox.cs"`) after the triage: 49 killed, 2 timeouts, 0 survived, 0 without coverage, both times.
- [x] 4.3 Triage every survivor in `LuaEnv/ModelTranslator.cs` the same way, with tests in `Tests/Env`, and list them here
  - None: every full and scoped run reported 0 survivors and 0 without coverage. Two scoped runs (`--mutate "**/LuaEnv/ModelTranslator.cs"`): 4 killed, 0 survived.
  - Limit of the measurement: Stryker places only 13 mutants in this file. 7 of them fail to compile (the `is { } v` / `is not Enum e` pattern variables can't be duplicated into Stryker's mutant switch), and it makes no mutation of `WriteValue`'s switch arms. The marshaling rules in this file are therefore barely measured by mutation, and the properties in `Tests/Env/MarshalingTests.cs` remain their real check. `LuaSandbox.cs` has the same compile-error pattern in `Run`, `CompileFunction`'s conditional and `LoadChunk` (9 mutants).
- [x] 4.4 For any survivor that appears in some runs but not others, identify the property whose generator reaches it unreliably and either strengthen that generator or add an example test. Verify by two consecutive scoped runs (`--mutate` on that file) that both report it killed
  - Two boundary mutants flickered across full runs: `CompileFunction`'s `[not false, …]` (123) and `IndexOfAny(…) > 0` (176). No FsCheck generator was involved. Scoped runs reported both as survivors 3 out of 3 times, and no test could detect either. The full runs that "killed" them blamed tests that can't observe them: `SubfolderTests.SubfolderForms` for the first, `EnumTableTests` for the second. Those kills were spurious, so the real state was "survived". The likely cause is a covering test failing for an unrelated reason in that session, such as load from the other concurrent jobs on this machine. Stryker 4.14.2+ reports timed-out or errored MTP tests as failures.
  - Both now have example tests (4.2), and two consecutive scoped runs on `LuaSandbox.cs` report both killed.
  - The same spread shows outside the boundary between full runs (`FilePathCleaner.cs`, `ILuaModel.cs`, `LuaRenamer.cs`, `ModelProducers.cs`, `Utils.cs`, ±1–2 each). That is why scores vary by about half a point. It's left for the later triage of those files.
- [x] 4.5 Verify a scoped run over the two boundary files reports no survivors without either a killing test or a site annotation
  - `dotnet stryker --mutate "**/LuaEnv/LuaSandbox.cs" --mutate "**/LuaEnv/ModelTranslator.cs"`: 53 killed, 2 timeouts, 0 survived, 0 without coverage. The 5 annotated mutants appear as `Ignored` with their reasons.

- [x] 4.6 Re-triage `LuaSandbox.cs` after stacking this change on `bound-script-resources`, which adds the instruction hook and the counting allocator to it
  - The first scoped run after the merge: 75 killed, 10 survived, 3 timeouts, 2 without coverage (86.67%). All 12 were in the new code.
  - **Killed by new tests in `Tests/Sandbox/ResourceLimitTests.cs`:**
    - The count's seed arithmetic (two mutants), the `held` conditional (three), and counting a failed growth instead of a free (`newSize != 0`) are killed by `TheAllocatorsCountMatchesLuasOwn`. It requires the wrapper's count to equal `LUA_GCCOUNT`/`COUNTB` after construction and translation, after a run, and after a collection. This needed a public `LuaSandbox.AllocatedBytes`.
    - `newSize - held` → `newSize + held` is killed by `AResizeIsChargedOnlyForWhatItAdds`.
    - `>` → `>=` on the bound is killed by `AScriptMayUseMemoryUpToExactlyTheBound`. It finds the smallest bound `x = 1` completes under and requires the run's peak to equal it. To make the peak the run's own, `PeakAllocatedBytes` now resets when `Run` begins.
    - Re-arming the hook at step 1 on a refusal is killed by `ARefusedAllocationStopsTheScriptAtItsNextInstruction`.
    - Clearing the hook when a refusal escapes the run function is killed by `ARefusalAfterTheScriptHasUnwoundLeavesNoHookBehind`. A linked list of small tables leaves no room for the hook's `debug.getinfo`, so the refusal recurs after the script's pcall returns.
    - `ABoundTooSmallForTheRunItselfIsReportedNotThrown` covers refusals in the run function's own allocations before the hook is installed.
  - **Removed by rewriting the code:**
    - `newSize > held && newSize - held > room` is now one signed `growth > room` comparison, so the equivalent `>` → `>=` on the guard no longer exists.
    - The catch path returns the memory-bound message directly, so the `error = ""` placeholder no longer exists either.
    - `Run`'s `?? error?.ToString() ?? "unknown error"` fallbacks are gone (`ErrorOf` casts), together with their annotation from 4.2.
  - **Annotated:** `_enforcing = false` → `true`. A mutant that leaves enforcement on fails the next host-side push outside any pcall, which panics the state, so no test can assert on it.
  - **Annotation scope:** `disable once <Mutator>` silences every mutation of that kind in the whole statement, including an `if`'s body. The first attempt annotated the allocator's `if` and so also hid `newSize < held`, the inverted bound check, and the body's `State.Hook is not { }`. 4.2's `close < 0` annotation likewise hid `close > 0`, so it became `close == -1`, whose mutants are both killed. The workflow header now states this.
  - Scoped run after the triage: 79 killed, 2 timeouts, 0 survived, 0 without coverage (100%). The one annotated mutant is reported as `Ignored`.

## 5. Documentation

- [x] 5.1 Add a short "Mutation testing" note for contributors covering a full run, a file-scoped run (`--mutate`), where the report lands, and the annotation convention for equivalent mutants. Put it in the workflow file's header comment, since the repo has no contributor doc. Verify the commands in it run as written
  - The header comment of `.github/workflows/mutation.yml`. Each command was run as written from the repo root. `dotnet tool restore` succeeds. `dotnet stryker` exits 0 in 69 s. `dotnet stryker --mutate "**/LuaEnv/LuaSandbox.cs"` exits 0 with 0 survivors. Both write `StrykerOutput/<timestamp>/reports/mutation-report.html`. The annotation form is the one used in `LuaSandbox.cs`, and Stryker reports annotated mutants as `Ignored` with the stated reason.
