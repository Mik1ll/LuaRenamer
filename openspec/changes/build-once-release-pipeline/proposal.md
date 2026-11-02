## Why

CI tests a Debug build, while releases ship a Release build compiled separately per runtime, so the bytes users install are never tested. The publish workflow also tests only on Linux x64, and only that Debug build. A release has to be dispatched with a full hand-written tag whose revision and Abstractions version the repository already knows. Build and test logic is duplicated across two workflows, and master has no required check, so a red PR can still merge.

## What Changes

- Build each runtime's archive once, on one runner, and test those exact bytes on every platform and host-configuration leg.
- Prove in the suite that a leg ran against the packaged build, and load each archive through a strict plugin-style load context that fails if anything resolves from outside the archive and the host.
- **BREAKING** (maintainer workflow only): replace the dispatch `tag` input with `version` (`major.minor.patch`). The revision, Abstractions version, and tag are derived. `commit-ish` and `prerelease` stay, and a new `dry-run` input runs everything but publishing.
- Make tags valid semver after the `v`: `v{major}.{minor}.{patch}[-dev.x]+abstr.a.b.c[.alpha.y]`. Archive names move from the tag to the version, because GitHub renames release assets containing a `+`.
- Treat each `major.minor.patch` as Dev-only or Stable-only, and refuse a version that would break that.
- Make the plugin's version an explicit build input rather than something read from `git describe`, removing the local "Tag HEAD" step.
- Merge `build-and-test.yml` and `publish-release.yml` into one workflow with four jobs: resolve, package, test, publish. Publish runs only on a real dispatch and uploads the tested archives unchanged.
- Limit write access to the publish job. Require a single aggregate check on master.

Out of scope:
- Any check that Stable is built against a final Abstractions release. Stable on a prerelease Abstractions version stays allowed.
- Windows timezone coverage, promoting CA1304/CA1305/CA1310/CA1311 to errors, and osx-arm64.
- Changing how Shoko orders versions.
- Changing when CI runs on code changes. It stays pull-request only, as `fix-test-invariant-gaps` and `ci-platform-matrix` decided, and this change only keeps the existing dispatch.

## Capabilities

### New Capabilities

- `release-pipeline`: how release versions are derived and validated, how archives are built once and published unchanged, and what must pass before a release or a merge.

### Modified Capabilities

- `testing-architecture`: adds a requirement that continuous integration verifies the packaged archives themselves, including loading them the way the host does, rather than a separate build.

## Impact

- **CI**: `build-and-test.yml` and `publish-release.yml` are replaced by one workflow, and `actions.lock` is regenerated to add `actions/download-artifact`. Check names change, which matters only for the new required check.
- **Repository settings**: the master ruleset gains a required status check. This is a manual step.
- **Build**: `LuaRenamer.csproj` loses the `GetGitAssemblyVersion` target. Local builds report the SDK default version, and CI always passes one.
- **Tests**: new packaged-build and plugin-load tests, which skip unless a leg supplies their expectations.
- **Scripts**: `scripts/check-release-version.sh` and its test script become a resolver that derives and validates the version.
- **Release process**: the next Dev has to be 6.4.1 or higher. A `commit-ish` from before this change is refused, because it cannot take an explicit version.
- **Release assets**: new archives are named `LuaRenamer_{version}_{runtime}.zip`, so the filename no longer shows the Abstractions version. The tag and manifest still do.
- **Unaffected**: existing tags, the archive format, manifest schema, release app and its secrets, mutation testing, and the four test legs' platforms and host configurations.
