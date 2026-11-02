# Tasks

## 1. Packaged-archive tests

- [x] 1.1 Add a canary that asserts the loaded `LuaRenamer` assembly carries `LUARENAMER_EXPECT_RID` and `LUARENAMER_EXPECT_VERSION`, skipping when unset, and verify it skips in a plain `dotnet test`
- [x] 1.2 Add a strict plugin-load test driven by `LUARENAMER_ARCHIVE`: extract to a fresh temporary directory, resolve through `AssemblyDependencyResolver`, throw on any assembly outside the archive, the framework, `Shoko.*`, and `Microsoft.Extensions.*`, run a script through `GetPath`, and assert a `lua54` module loaded from inside that directory. Verify it skips when unset
- [x] 1.3 On win-x64, pack with `shoko-build`, overlay the archive onto `Tests/bin/Release/net10.0`, and verify the full suite passes with both tests running and neither skipped
- [x] 1.4 Verify both tests can fail: the canary against an unmodified build, and the load test against a copy of the archive with `lua54.dll` removed and another with `NLua.dll` removed

## 2. Version resolver

- [x] 2.1 Add `scripts/resolve-release-version.sh`, replacing `check-release-version.sh`: take `major.minor.patch`, channel, manifest, lock file, and remote tags, and print version, tag, and channel. Tags are valid semver after the `v`. Verify it derives version `6.5.0.1` and tag `v6.5.0-dev.1+abstr.6.0.0.alpha.108` from the current manifest and lock file
- [x] 2.2 Port `check-release-version.test.sh` to the resolver, covering first and next revision, a revision known only from a tag in either the new or the older format, Stable, the Dev-or-Stable refusals in both directions, malformed input, and Stable on a prerelease Abstractions version being allowed. Verify `bash scripts/resolve-release-version.test.sh` passes, including a refusal of `6.4.0` Dev
- [x] 2.3 Keep `check-release-version.sh` working until group 3 lands if groups land separately, and verify `publish-release.yml` still references a file that exists

## 3. Workflow and explicit version

These land together, because `publish-release.yml` depends on the target removed in 3.1.

- [x] 3.1 Remove `GetGitAssemblyVersion` from `LuaRenamer/LuaRenamer.csproj`, and verify `shoko-build -p:Version=9.9.9.1` against a manifest copy adds a `9.9.9.1` entry without touching 6.4.0
- [ ] 3.2 Add `.github/workflows/build-test-release.yml` with `resolve`, `package`, the four `test` legs, and `ci-ok`, keeping the existing matrix, `fail-fast: false`, host configuration, and timeouts, and defaulting to `contents: read`. Verify all jobs pass on the pull request and that `publish` is skipped
- [ ] 3.3 In `resolve`, take `version`, `commit-ish`, `prerelease`, and `dry-run` inputs, fix the commit, and output a `0.0.1.<run_number>` version on pull requests. Verify from the logs that every job checks out the same SHA
- [ ] 3.4 In `package`, run the Lua drift check after `shoko-build`, assert the manifest's new entry has the resolved version, name archives by version rather than tag, and upload zips and manifest as an artifact. Verify all three zips appear on the pull request run with no `+` in their names, and that the run's tag carries one
- [ ] 3.5 In `test`, overlay each leg's archive and set the three expectation variables from the matrix, using the linux-x64 archive on the host-configuration leg. Verify in each leg's log that the canary and load test ran rather than skipped, on Windows, Linux x64, and arm64
- [ ] 3.6 Add `publish`, guarded on a real dispatch: generate notes, patch notes and `source_revision` into the manifest, verify zip checksums against it, create or update the release, and push the manifest, with `contents: write` and the app token on this job only. Verify from the rendered job graph that it needs `package` and `test`
- [ ] 3.7 Add dispatch-only concurrency, and a header comment in the workflow covering how to release and to use "Re-run failed jobs". Verify a pull request run is not queued behind another
- [x] 3.8 Delete `build-and-test.yml`, `publish-release.yml`, and `check-release-version.sh` and its test, then run `gh actions-lock`. Verify `actions.lock` lists the new workflow and `actions/download-artifact`, and that nothing still references the deleted files

## 4. Rollout

Steps 4.1 to 4.3 change the live repository and need the maintainer's go-ahead when reached.

- [ ] 4.1 Add `ci-ok` as a required status check in the master ruleset, and verify with `gh api` that the rule is present
- [ ] 4.2 Dispatch a dry run on master, and verify that resolution, packaging, and all legs pass and that no tag, release, or manifest commit appeared
- [ ] 4.3 Publish the next release at 6.4.1 or higher, and verify the published zips' hashes match the run's artifact and the manifest, that each manifest URL downloads its archive, and that the install works on a Shoko `daily` server
