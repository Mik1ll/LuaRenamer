## 1. Spike: can Stryker drive this suite

- [ ] 1.1 Add Stryker.NET (latest release, ≥ 4.13) as a local tool in `.config/dotnet-tools.json` and verify `dotnet tool restore` then `dotnet stryker --version` succeed
- [ ] 1.2 Run `dotnet stryker --test-runner mtp` against `LuaEnv` alone and verify the initial test run discovers the same test count `dotnet test` reports and passes. Record the Stryker version, mutant count, and wall-clock time here
- [ ] 1.3 If 1.2 fails, apply the VSTest fallback from design.md, verify `dotnet test` still runs under MTP with an unchanged test count, verify Stryker's initial run passes, and record which runner was used and why
- [ ] 1.4 From 1.2's numbers, estimate a full two-project run's duration on a hosted runner and record it. If it exceeds about 5 hours, record which mitigation from design.md's Risks applies

## 2. Configuration

- [ ] 2.1 Add `stryker-config.json` in solution mode over `LuaEnv` and `LuaRenamer`, with the `**/*.g.cs` `mutate` exclusion and the runner chosen in group 1. Verify a run's report lists no files under `DefsGenerator` and no `LuaNames.g.cs`
- [ ] 2.2 Set `thresholds.break` to 0 and verify a run with survivors exits successfully
- [ ] 2.3 Verify `git status` is clean after a full run, i.e. the build targets didn't rewrite the tracked Lua defs

## 3. Scheduled workflow

- [ ] 3.1 Add `.github/workflows/mutation.yml` on `workflow_dispatch` and a weekly `schedule`, using the same restore/cache steps as `Build and Test`, running Stryker, and uploading `StrykerOutput/**/reports/` as an artifact. Verify by a manual dispatch that produces a downloadable HTML report
- [ ] 3.2 Check whether Dependabot's `nuget` ecosystem updates `.config/dotnet-tools.json` and record the answer. If it doesn't, note that in the manifest's commit message

## 4. Baseline and sandbox-boundary triage

- [ ] 4.1 Record the baseline mutation score and the survivor counts for each file here
- [ ] 4.2 Triage every survivor in `LuaEnv/LuaSandbox.cs`. For each one, either add a test (in the existing `Tests/Sandbox` file for that behavior) that kills it and names the behavior, or add a `// Stryker disable once <mutator> : <reason>` at the site. List each survivor and how it was resolved here
- [ ] 4.3 Triage every survivor in `LuaEnv/ModelTranslator.cs` the same way, with tests in `Tests/Env`, and list them here
- [ ] 4.4 For any survivor that appears in some runs but not others, identify the property whose generator reaches it unreliably and either strengthen that generator or add an example test. Verify by two consecutive scoped runs (`--mutate` on that file) that both report it killed
- [ ] 4.5 Verify a scoped run over the two boundary files reports no survivors without either a killing test or a site annotation

## 5. Documentation

- [ ] 5.1 Add a short "Mutation testing" note for contributors covering a full run, a file-scoped run (`--mutate`), where the report lands, and the annotation convention for equivalent mutants. Put it in the workflow file's header comment, since the repo has no contributor doc. Verify the commands in it run as written
