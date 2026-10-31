## Context

See proposal.md for the motivation. The current state that shapes the approach:

- `Tests` is an xUnit v3 4.0 project running as a self-hosted Microsoft Testing Platform executable (`OutputType=Exe`, `global.json` `test.runner = Microsoft.Testing.Platform`). xUnit v3 4.0 targets MTP v2. There is no VSTest adapter.
- Stryker.NET added an MTP runner in 4.13, in preview (announced 2026-03-13). Its coverage analysis is partial: it can skip mutants no test covers, but it cannot narrow a covered mutant down to the tests that cover it. **Every covered mutant runs the full suite.**
  - *Implementation note:* 5.0.0 (2026-09-11) added `perTest` coverage to the MTP runner, and the spike confirmed it captures per-test coverage for all 315 tests. A covered mutant now runs only the tests that cover it.
- `LuaRenamer`'s design-time build also runs `GenerateLuaNames`, and Stryker's project analysis is a design-time `Clean;Build` of each project. *Implementation note:* that `Clean` deleted `DefsGenerator`'s real assembly before `LuaRenamer` was analyzed. `Directory.Build.props` now gives design-time builds their own clean list.
- `LuaRenamer`'s build runs `GenerateLuaDefs` and `GenerateLuaNames`. Stryker compiles mutants in memory with Roslyn after a single initial MSBuild build, so those targets run once against unmutated models. The tracked defs don't change, and no mutant reaches the generated `*Names` DSL.
- Several of the strongest checks are FsCheck properties with random seeds. A mutant that only some generated inputs expose can be killed in one run and survive the next.

## Goals / Non-Goals

**Goals**
- A repeatable way to see which production changes the suite fails to notice, starting with the code where that matters most.
- Equivalent mutants are justified where the code is, so the justification is reviewed alongside the code it's about.

**Non-Goals**
- A merge gate or minimum score. A threshold invites tests written to kill mutants rather than to state behavior, which goes against what the testing-architecture spec asks of tests.
- Incremental per-PR runs (`--since`). Worth revisiting once full-run cost and noise are known.
- Mutating Lua. Stryker mutates C#; the shipped Lua scripts are covered end-to-end by `DefaultScriptTests` and `TrustedChunkTests`.
- Triaging every survivor. Only the sandbox boundary is triaged in this change; the rest are measured.

## Decisions

### MTP runner first, VSTest adapter as fallback

Use `"test-runner": "mtp"`. Task 1 is a spike that checks Stryker's initial test run discovers and passes the full suite under xUnit v3 4.0 / MTP v2.

If that fails, the fallback is adding `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk` to `Tests.csproj` so Stryker's mature VSTest runner can drive it. `dotnet test` keeps using MTP through `global.json`; the adapter would exist only for Stryker. It's the fallback rather than the default because it gives the project two runner stacks that can disagree, for example on test discovery or on dynamic skips. If the fallback is taken, the spike records the discrepancy check it ran.

*Alternative considered:* waiting for the MTP runner to leave preview. Rejected. The spike costs an hour and settles the question either way.

### Scope by project, exclude by file

Stryker runs in solution mode over `LuaEnv` and `LuaRenamer`, with a `mutate` exclusion for `**/*.g.cs` (which covers the `LuaNames.g.cs` that `GenerateLuaNames` writes to `obj/`) and no reference to `DefsGenerator`.

`DefsGenerator` is excluded deliberately. Its output is already checked by CI's `git diff --exit-code` on the tracked defs, which is a stronger check than any test of the generator would be, and mutating it would measure the wrong thing.

*Implementation note:* Stryker 5.0.0 can't run solution mode over a subset of a solution. Its `project` filter is one substring of a project's full path, it rejects `.slnf`, and a two-project `.slnx` in a subfolder trips an unnormalized `../` lookup (tasks.md 2.1 has the details). So the run is over `LuaRenamer.slnx`, and `DefsGenerator` is excluded by file with `!**/DefsGenerator/**/*.cs`. Its files still appear in the report, but every mutant in them is `Ignored` or `CompileError`, so they don't count toward the score.

### The sandbox boundary is the triage scope

For this change, the sandbox boundary is `LuaEnv/LuaSandbox.cs` (environment construction, the run function, path resolution) and `LuaEnv/ModelTranslator.cs` (what a model becomes when a script sees it). These decide what an untrusted script can reach. A surviving mutant there is the gap most worth closing, and the set of survivors is small enough to finish within one change.

### Equivalent mutants are marked at the site

Use Stryker's inline form, `// Stryker disable once <mutator> : <reason>`, with the reason saying why the mutation is unobservable. For example, `// Stryker disable once Equality : index is always ≥ 1 here, so < and <= agree`. That keeps the justification next to the code and visible in review. A central ignore list would keep working after the code it describes had changed.

### Flicker is a finding, not noise

Fixing FsCheck seeds during mutation runs would make the report reproducible. But it would also hide what flicker reveals: a property whose generator reaches the code being mutated only some of the time. A mutant that survives in some runs is triaged like any other survivor. The fix is usually a generator that reaches the case reliably, or an example test that pins it.

### Weekly and on demand, not per PR

Because the MTP runner runs the full suite for every covered mutant, a full run costs roughly *mutants × suite time ÷ concurrency*. The spike measures this. A weekly scheduled run plus `workflow_dispatch` keeps the report current without putting that cost on every PR. Locally, `--mutate <file>` scopes a run to the file being worked on.

## Risks / Trade-offs

- **The MTP runner can't drive xUnit v3 4.0 / MTP v2** → the VSTest fallback above. If neither works, the change stops after the spike and the finding is recorded; nothing else in it is worth doing without a runner.
- **Full runs are too slow for a hosted runner's 6-hour limit** → measured by the spike. Mitigate by splitting the run per project into separate jobs, or narrowing the scheduled run to the sandbox boundary and running the rest on demand.
- **Mutants that loop forever** → Stryker gives each mutant a timeout based on the initial run's duration and reports those mutants as `Timeout`, which counts as killed. No suite-side handling is needed.
- **Tests written to kill a mutant rather than to state a behavior** → The spec requires a killing test to name the behavior it protects. Review holds new tests to that.
- **Dependabot doesn't update the tool manifest** → Check whether Dependabot's `nuget` ecosystem picks up `.config/dotnet-tools.json`. If it doesn't, the pinned version is bumped by hand, and that is noted in the manifest's commit.
