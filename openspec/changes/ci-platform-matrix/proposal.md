## Why

The testing-architecture spec requires that assertions hold on every supported platform and are independent of host locale and timezone, but CI checks neither: `Build and Test` runs once, on `ubuntu-latest`, under the runner's default culture and UTC. Windows is exercised only on the maintainer's machine, so Dependabot PRs and any contributor working on Linux are never tested there. linux-arm64 is a declared runtime identifier with its own native Lua binary, and it is never run at all. The requirements are written as guarantees, but nothing enforces them.

## What Changes

- Run `Build and Test` as a matrix covering each supported platform: Windows x64, Linux x64, and Linux arm64.
- Add a host-configuration leg on Linux x64 that runs under a culture whose case mapping and decimal separator differ from the invariant culture (`tr-TR`), and under a timezone with a non-whole-hour offset that observes DST (`America/St_Johns`).
- Add a canary test that asserts the culture and timezone the leg was told to expect are the ones the test process actually observes. Without it, a misconfigured leg would pass without testing anything. It skips when no expectation is supplied.
- Report every leg independently (no fail-fast), so a failure on one configuration does not hide the others.
- Triage whatever the new legs expose. A failure is fixed in production code if the behavior really is platform- or locale-dependent, and in the assertion if the test was.

Out of scope: changing when `Build and Test` triggers. This was considered and left alone in `fix-test-invariant-gaps`, and this change doesn't revisit it.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `testing-architecture`: adds a requirement that platform and host-configuration independence is exercised by continuous integration rather than assumed, and that each configured leg proves its configuration took effect.

## Impact

- **CI**: `.github/workflows/build-and-test.yml` changes from one job to four matrix legs. Wall-clock time per PR stays about the same because the legs run in parallel. Total runner minutes roughly quadruple, which is free for a public repository.
- **Tests**: one new test, the host-configuration canary. Any further test changes come from triaging what the new legs expose.
- **Production**: none planned. Any change would come from a real platform or locale defect found during triage, and would be recorded in tasks.md.
- **Unaffected**: package versions, `packages.lock.json` (it already resolves all three runtime identifiers), and the generated Lua defs check, which now also runs on every leg.
