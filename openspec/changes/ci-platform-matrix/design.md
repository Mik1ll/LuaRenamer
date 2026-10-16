## Context

See proposal.md for the motivation. Some of the current state makes this cheaper than it looks:

- `Tests.csproj` already declares `RuntimeIdentifiers` `win-x64;linux-x64;linux-arm64`, so `packages.lock.json` resolves all three. `RestoreLockedMode` (on under `CI=true`) works on every leg with no lock-file change.
- KeraLua 1.4.10 ships native binaries under `runtimes/` for `win-x64`, `linux-x64` and `linux-arm64`, so the arm64 leg exercises a real native binary that is never loaded today.
- `.gitattributes` sets `* text=auto`, so `git diff --exit-code -- LuaRenamer/lua/` compares normalized content. A generator that writes LF on Windows won't produce a false diff on a CRLF checkout.
- `master` has no branch protection, so renaming the check to matrix-suffixed names breaks no required status checks.

Locale reaches the test process through two separate layers. .NET reads `LANG`/`LC_ALL` through ICU, which needs no OS locale installed. The C runtime that Lua's `os.date` and number formatting run on only honors a locale that has been generated on the machine, and only if something in the process calls `setlocale`. Whether that happens in a .NET host is not established, which is why the canary below asserts the .NET layer and task 3.2 investigates the C layer separately.

## Goals / Non-Goals

**Goals**
- Each claim in the spec's platform and locale requirements is checked by at least one CI leg that can actually falsify it.
- A leg that silently fell back to defaults is caught, not counted as coverage.

**Non-Goals**
- Changing the `pull_request` trigger. Decided against in `fix-test-invariant-gaps`.
- macOS. It isn't a declared runtime identifier and Shoko doesn't target it.
- Exhaustive locale or timezone sweeps. One adversarial configuration is enough for each claim the suite makes.

## Decisions

### Four legs, one variable each

| Leg | Runner | Culture | Timezone |
|---|---|---|---|
| windows | `windows-latest` | runner default | runner default |
| linux-x64 | `ubuntu-latest` | runner default | UTC |
| linux-arm64 | `ubuntu-24.04-arm` | runner default | UTC |
| linux-x64-host-config | `ubuntu-latest` | `tr_TR.UTF-8` | `America/St_Johns` |

The `linux-x64` leg is today's job, kept as it is so that a regression on the existing configuration is still reported by itself.

*Alternative considered:* folding the culture and timezone onto the arm64 leg to save a job. Rejected. When a leg differs from the baseline in only one way, its failure tells you which change caused it. A combined leg failing on arm64 + Turkish + Newfoundland would leave three candidates to rule out.

### `tr-TR` and `America/St_Johns`

`tr-TR` is the standard adversarial culture for two independent reasons that both apply here. Its case mapping turns `i` into `İ`, which breaks any culture-sensitive case comparison: device-name matching and case-only-rename detection both do case-insensitive work. It also uses a comma as the decimal separator, which breaks any culture-sensitive number formatting that reaches a path or a Lua literal (`EnvFixture.Render` already goes through `InvariantCulture`; the leg checks that nothing else assumes it).

`America/St_Johns` is UTC−3:30 and observes DST. A half-hour offset catches arithmetic that assumes whole-hour offsets. DST puts real gaps and overlaps into the local timeline, and `DateTimeTests`' `os.time`/`os.date` round trip has to keep to its stated claim around them.

### The canary lives in the suite, and skips when not asked

`HostConfigurationTests` reads the expected culture name and timezone id from environment variables that only the host-config leg sets. It asserts `CultureInfo.CurrentCulture.Name` and `TimeZoneInfo.Local` against them, and when they're unset it skips with `Assert.Skip`.

This sits uneasily with the spec's "guards that can fail earlier do not live in the suite", but this guard cannot fail earlier. The claim is about what the *test process* observes, and only code running in that process can check it. A workflow step that prints `locale` would show the shell's configuration, not what the test host sees.

*Alternative considered:* asserting the configuration in a `dotnet run` file-based script, as `scripts/patch-sourcelink.cs` does. Rejected for the same reason: it's a different process.

### `fail-fast: false`

With GitHub's default, the first failing leg cancels the rest, which hides exactly the information the matrix exists to provide: whether a failure is specific to one platform or general.

### The generated-defs check runs on every leg

It costs a few seconds, and it also checks that defs generation is platform-independent. A generator that emitted platform-specific separators or orderings would show up as a diff on one leg only.

## Risks / Trade-offs

- **The new legs expose real failures** → That's the purpose. Each one goes through task 3.1's triage: decide whether the production code or the assertion is the platform- or locale-dependent one, fix that, and record it. None is fixed by narrowing the leg.
- **The C runtime never sees the locale** → The leg would then cover the .NET layer only. Task 3.2 settles this and records the answer, so the coverage claim matches what is actually exercised.
  - *Settled (task 3.2):* nothing in the process calls `setlocale`, so the leg's culture reaches the .NET layer only. Lua's `tostring` and `os.date` keep the `C` locale even with `tr_TR.UTF-8` generated and `LC_ALL` set. The timezone reaches both layers, because glibc's `localtime` reads `TZ` without any `setlocale` call. The leg therefore checks the .NET layer for culture-sensitive behavior, and both layers for timezone arithmetic.
- **Windows runners are slower** → The suite is small; the Windows leg sets the wall-clock time, but it should stay within a few minutes. If not, it's recorded and revisited, but the leg isn't dropped.
- **arm64 runner availability changes** → Hosted arm64 runners are free for public repositories. If that changes, the leg becomes a cost decision, not a correctness one.
