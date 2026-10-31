## Why

The suite already holds itself to a standard that line coverage can't measure: an assertion that cannot fail is worse than none. That is enforced today by hand. Tests like `SchemaConformanceRejectsAnUndeclaredKey` and `MapIntegrityRejectsAnUndeclaredKey` exist only to prove another check can fail, and every task in `fix-test-invariant-gaps` included a "verify it bites" step done by temporarily breaking production code. Mutation testing does the same thing mechanically across all production code: it makes small changes to the code and reports each one no test notices. This turns a discipline applied to a few tests into a measurement over the whole suite.

## What Changes

- Add Stryker.NET as a pinned local dotnet tool, configured to mutate the two production assemblies the suite tests: `LuaEnv` and `LuaRenamer`.
- Exclude code the suite doesn't own. `DefsGenerator` is a build-time tool whose output the generated-defs diff already guards. Generated sources (the `*Names` DSL) are excluded too. The vendored and shipped Lua isn't touched, since Stryker mutates C# only.
- Add a workflow that runs mutation testing on demand and weekly, and publishes the HTML report as a run artifact. It is not a pull-request check and does not gate merges.
- Record a baseline mutation score.
- Triage every surviving mutant in the sandbox boundary, meaning the code that decides what a user script can reach. For each one, either add a test that kills it, or mark it equivalent at the site with the reason. Survivors elsewhere are reported for later triage, not fixed in this change.
- Document how to run a full or file-scoped mutation run locally.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `testing-architecture`: adds a requirement that the suite's ability to detect defects is measured by mutation, that surviving mutants in the sandbox boundary are either killed or justified, and that the measurement is reported rather than enforced as a merge gate.

## Impact

- **Tooling**: new `.config/dotnet-tools.json` (Stryker.NET) and a `stryker-config.json`.
- **CI**: new `.github/workflows/mutation.yml` (`workflow_dispatch` plus a weekly schedule). `Build and Test` is unchanged.
- **Tests**: new tests for each sandbox-boundary survivor that represents a real gap.
- **Production**: no behavior change. Equivalent mutants get a `// Stryker disable once <mutator> : <reason>` comment at the site, saying why the mutation can't be observed.
- **Dependency risk**: Stryker's Microsoft Testing Platform runner is in preview (since 4.13). This repo runs xUnit v3 4.0 on MTP v2, and nothing published yet confirms Stryker works with that combination. The change therefore begins with a spike, and design.md names the fallback.
