## Context

See proposal.md for the motivation. The current state that shapes this, checked on 2026-11-01 by reading `shoko-build` and Shoko Server at master and by packing real archives on win-x64:

- `shoko-build` runs the `Build` target once per entry in `RuntimeIdentifiers`, with `EnableDynamicLoading=true`, and zips the output. It does not publish. It packs a `publish/` folder only if one already exists, which only Rider's Publish run configurations create.
- The archive puts the native library at its root (`lua54.dll`, `liblua54.so`), while the non-RID test output keeps it under `runtimes/<rid>/native/`.
- A build stamps `RuntimeIdentifier=any`. An archive stamps its RID.
- `shoko-build` takes the release version from the built assembly and matches manifest entries by that version alone. `-p:Version` is overridden by the `GetGitAssemblyVersion` target, so a packed build at an untagged HEAD reused 6.4.0 and overwrote that entry in a copy of the manifest.
- Shoko refuses plugin DLLs at version 0.0.0, and records only `major.minor.patch` of the Abstractions version in the manifest.
- `master` has a ruleset for pull requests, but no required status checks.

## Goals / Non-Goals

**Goals**
- A red run can never publish, and a green run publishes exactly what was tested.
- Pull requests exercise the release path, so packaging breaks show up before release day.

**Non-Goals**
- Reproducible builds. Build-once removes the need for them.
- A faster pipeline. Run time is about what it is today plus the packaging job.

## Decisions

### One workflow with a straight line of jobs

`build-test-release.yml` replaces both workflows, triggered by `pull_request` and `workflow_dispatch`:

| Job | Runs | Does |
|---|---|---|
| `resolve` | always | Runs the resolver's tests, fixes the commit, and outputs version, tag, channel, and SHA |
| `package` | always, Linux | Locked restore, `shoko-build` for all RIDs, Lua drift check, version check, uploads zips and manifest |
| `test` | always, 4 legs | Downloads the zips, builds Tests in Release, overlays the archive, runs the suite |
| `ci-ok` | always | Fails unless every job above succeeded. This is the required check |
| `publish` | real dispatch only | Generates notes, patches the manifest, verifies checksums, releases, pushes the manifest |

`resolve` always runs, with a throwaway version on pull requests, so no job depends on a skipped job. That avoids the `if: !cancelled() && ...` guards a dispatch-only `resolve` would need.

*Alternative considered:* a reusable workflow called by a CI workflow and a release workflow. It gets the same sharing and needs no conditions, but it splits the dispatch form from the jobs it drives, and adds an output-passing layer for no behavior gain.

### Build on one runner, test on all

Packaging compiles no native code: the native binaries come from the KeraLua package, so Linux can pack all three archives. This also matches how releases work today.

*Alternative considered:* packing on each native runner. That produces three builds on three machines, loses the single-artifact property, and needs three manifest merges.

### Overlay the archive onto the test output

Each leg builds Tests in Release with the same `-p:Version`, then expands its zip over `Tests/bin/Release/net10.0` and moves the native library into `runtimes/<rid>/native/`, the path `deps.json` names. The step checks for the library first, because `Move-Item` silently does nothing when its wildcard matches nothing. On win-x64 this ran the full suite, 327 passed with the 2 expected skips, with the DLL's hash matching the archive's.

*Alternatives considered:*
- `dotnet build Tests -r <rid>` would place the native library at the root, but fails with NETSDK1047. `DefsGenerator` is an Exe reached through a ProjectReference, so the RID flows to it, and it declares no RIDs.
- Swapping the ProjectReference for a file Reference would compete with the `LuaRenamer.LuaEnv` that `DefsGenerator` brings in. Not tried.
- Shipping a prebuilt test output between jobs would still need the native library moved, plus more moving parts for the same result. Not tried.

### Canary, then a strict load context, both in Tests

Two tests follow the canary pattern from `ci-platform-matrix`. Expectations come from the matrix (`LUARENAMER_EXPECT_RID`, `LUARENAMER_EXPECT_VERSION`, `LUARENAMER_ARCHIVE`), so a leg that lost its setup fails rather than skips.

- The canary asserts that the loaded `LuaRenamer` assembly carries the expected RID and version.
- The load test extracts the zip to a fresh temporary directory and loads it in an `AssemblyLoadContext` using `AssemblyDependencyResolver`, the same resolver Shoko's `PluginLoadContext` uses. Because `RelocationContext<T>` is generic over the plugin's own settings type, the test builds it by reflection, then reads a `RelocationResult`, which is shared through Abstractions.

The load context has to be stricter than Shoko's. In a spike, deleting `lua54.dll` from the archive still passed, because the runtime fell back to the test output's copy. The same applies to managed assemblies through the default context. So:
- `Load` returns null only for the framework and for `Shoko.*` and `Microsoft.Extensions.*`. Anything else must resolve from the archive, or the test throws.
- The test asserts that a loaded `lua54` module sits inside the extraction directory. It checks the result rather than failing when the resolver returns null, so it doesn't depend on how each platform resolves the name.

*Alternative considered:* a separate project with no reference to the plugin, which would have no fallback to guard against. It needs its own lock file and a copy of the host fakes, for a guarantee that a few lines of strictness give here.

### The version is an explicit build input

Delete `GetGitAssemblyVersion` and pass `-p:Version` from `resolve`. This removes the "Tag HEAD" step, its deletion of other tags on HEAD, and the failure mode where an untagged build silently takes a neighbouring version. Local builds report the SDK default, 1.0.0.

*Alternative considered:* gating the target on a new property. That keeps a fallback nothing in CI should reach, and it was the source of the overwrite above.

### Version derivation

`scripts/resolve-release-version.sh` replaces `check-release-version.sh`. It takes the requested version, the channel, the manifest, the lock file, and the remote tags, then prints version, tag, and channel. Revisions come from the manifest and the tags together, so a release whose manifest push failed can't have its revision reused.

Tags become valid semver after the `v`: `v6.5.0-dev.1+abstr.6.0.0.alpha.108`, and `v6.5.0+abstr.6.0.0` for Stable. The Abstractions version moves into build metadata, where it doesn't affect precedence, with `-` written as `.`. Existing tags stay as they are, and the resolver reads both forms.

The version stays four-part (`6.5.0.1`) in the assembly and manifest, because an assembly version can only hold numbers and `shoko-build` writes the manifest from it. So `-dev.N` appears only in the tag.

Shoko treats tags as opaque, and its schema leaves them unconstrained. Download URLs work with `+` as it stands: both `+` and `%2B` returned the file for a release of `k3s-io/k3s`. Asset names don't, because GitHub renames assets with special characters, while workflow artifacts keep their names, so CI wouldn't see the break. Archives are therefore named `LuaRenamer_{version}_{runtime}.zip`, not by tag. Pull requests use a tag with build metadata too, so they exercise the `+`.

*Alternative considered:* the tag with its build metadata removed, as in `LuaRenamer_v6.5.0-dev.1_linux-x64.zip`. It reads closer to the tag, but needs one more output from `resolve`, because workflow expressions can't cut a string at a character. `{version}` is `shoko-build`'s own template and matches the manifest.

The Dev-or-Stable rule exists because of a gap in Shoko. `SemverVersionComparer`'s summary says a revision sorts below its base version, but a three-part `Version` has `Revision == -1`, not 0, so the comparison falls through and `6.5.0` sorts below `6.5.0.3` (run against a copy of that logic). The rule is safe under either reading.

### `commit-ish` is kept and fixed once

`resolve` checks out the workflow's own ref for the resolver, and the commit-ish at a subpath for its lock file, then outputs `git rev-parse HEAD`. Every later job checks out that SHA. The resolver comes from the workflow ref so a release always applies the current rules.

`package` then asserts that the manifest's new entry has the resolved version. A commit from before this change still has the `git describe` target and would otherwise land on the wrong version, so it fails here instead.

### Release metadata belongs to `publish`

`POST /releases/generate-notes` needs write access, so notes are generated in `publish` and patched into the manifest with `jq`, next to `source_revision`, which `shoko-build` still doesn't record. That leaves `package` read-only. The workflow defaults to `contents: read`, and only `publish` gets `contents: write` and the app token.

### Concurrency and re-runs

Workflow-level concurrency uses a fixed group for dispatches, and `github.run_id` otherwise, so pull requests never queue. Publishing keeps `gh release upload --clobber` for the case where the release exists. Because the archives come from the run's artifact, a job re-run re-uploads the same bytes that the manifest describes.

`package` records the commit its manifest came from, and `publish` commits on top of that one. If anything else moved the branch in between, the push fails rather than overwriting it.

### Throwaway versions on pull requests

Pull requests use `0.0.1.<run_number>` and the tag `v0.0.1-dev.<run_number>+abstr.ci`. The version is non-zero because Shoko skips 0.0.0, so a pull request's archive can be installed by hand.

## Risks / Trade-offs

- [The overlay and load test have only run on win-x64] → The first pull request run covers Linux x64 and arm64. If the native library resolves differently there, the design already asserts the outcome rather than the resolver.
- [`workflow_dispatch` needs the new file on the default branch] → The pull request proves everything but publishing. The first dispatch after merge is a dry run.
- [A pending dispatch is replaced by a newer one, as GitHub keeps only one pending run per group] → Acceptable with one maintainer. The replaced run publishes nothing.
- [Re-running all jobs after a partial publish derives the next revision, leaving an orphan release] → The workflow's header comment says to use "Re-run failed jobs".
- [Shoko changes its comparer to match its docs] → The Dev-or-Stable rule stays correct, and only the 6.4.0 legacy entry would sort differently.
- [`shoko-build` changes how it reports or matches versions] → The version assertion in `package` fails loudly, and the tool's version is pinned in `dotnet-tools.json`.

## Migration Plan

1. Land the archive tests and the resolver. Both are inert until a leg or workflow uses them.
2. Land the workflow replacement and the csproj change together, because `publish-release.yml` depends on the target being removed.
3. Add `ci-ok` as a required check in the master ruleset.
4. Dispatch a dry run on master, then the next real release, which must be 6.4.1 or higher.

Rollback is a revert of step 2. The manifest format is unchanged and existing tags stay as they are, so earlier releases are unaffected.
