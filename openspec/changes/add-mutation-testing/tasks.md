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
- [x] 3.2 Check whether Dependabot's `nuget` ecosystem updates `.config/dotnet-tools.json` and record the answer. If it doesn't, note that in the manifest's commit message
  - No. The manifest is the root `dotnet-tools.json` (see 1.1). It already pins `shoko.buildtools` 0.5.1, and 0.5.2 was published on 2026-10-13. Dependabot's `nuget` runs on 10-14 and 10-15 opened package bumps but none for the tool. The manifest's commit ("Add Stryker.NET 5.0.0 as a local tool") says `dotnet-stryker` is bumped by hand. Moving the manifest to `.config/` might change this but is untested, and it would also move `shoko.buildtools`, which the release workflow restores.

## 4. Baseline and sandbox-boundary triage

- [ ] 4.1 Record the baseline mutation score and the survivor counts for each file here
- [ ] 4.2 Triage every survivor in `LuaEnv/LuaSandbox.cs`. For each one, either add a test (in the existing `Tests/Sandbox` file for that behavior) that kills it and names the behavior, or add a `// Stryker disable once <mutator> : <reason>` at the site. List each survivor and how it was resolved here
- [ ] 4.3 Triage every survivor in `LuaEnv/ModelTranslator.cs` the same way, with tests in `Tests/Env`, and list them here
- [ ] 4.4 For any survivor that appears in some runs but not others, identify the property whose generator reaches it unreliably and either strengthen that generator or add an example test. Verify by two consecutive scoped runs (`--mutate` on that file) that both report it killed
- [ ] 4.5 Verify a scoped run over the two boundary files reports no survivors without either a killing test or a site annotation

## 5. Documentation

- [ ] 5.1 Add a short "Mutation testing" note for contributors covering a full run, a file-scoped run (`--mutate`), where the report lands, and the annotation convention for equivalent mutants. Put it in the workflow file's header comment, since the repo has no contributor doc. Verify the commands in it run as written
