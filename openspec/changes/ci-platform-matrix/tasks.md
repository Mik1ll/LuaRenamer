## 1. Host-configuration canary

- [x] 1.1 Add `Tests/Pure/HostConfigurationTests.cs`: read the expected culture name and timezone id from environment variables (e.g. `LUARENAMER_EXPECT_CULTURE`, `LUARENAMER_EXPECT_TZ`), assert them against `CultureInfo.CurrentCulture.Name` and `TimeZoneInfo.Local`, and `Assert.Skip` when unset. Verify that a plain local `dotnet test` reports both as skipped, not passed
- [x] 1.2 Verify the canary passes locally when the variables are set to the machine's actual culture and timezone
- [x] 1.3 Verify the canary bites: set `LUARENAMER_EXPECT_CULTURE=tr-TR` without changing the process culture and confirm the test fails naming both cultures

## 2. Matrix

- [x] 2.1 Convert `build-and-test.yml`'s job to a matrix over the four legs in design.md, with `fail-fast: false` and `runs-on` taken from the matrix. Verify the workflow parses (`actionlint` if available, otherwise the PR run's job list shows four legs)
  - `actionlint` isn't available locally. Run 38882148286 lists four jobs: `Build and Test (windows)`, `(linux-x64)`, `(linux-arm64)` and `(linux-x64-host-config)`.
- [x] 2.2 On the host-config leg, generate the locale (`sudo locale-gen tr_TR.UTF-8`) and set `LANG`, `LC_ALL`, `TZ` and the two canary variables in the leg's `env`. Verify by the leg's test output listing the canary as passed, not skipped
  - Deviation: the variables are written to `$GITHUB_ENV` by a step that runs only on that leg, not set in the job's `env:`. A matrix expression that is empty on the other legs would still set `LANG`/`TZ` to the empty string there.
  - Run 38882148286: `locale-gen` reports `tr_TR.UTF-8... done`. At normal verbosity the platform names only failed and skipped tests. The leg's summary is 318 total, 317 passed, 1 skipped, and the one skip is the 3.2 probe, so both canary tests passed. On the other three legs both canary tests are listed as skipped.
- [x] 2.3 Keep restore, build, the generated-defs diff and test identical across legs, and verify each leg's log shows locked-mode restore succeeding without lock-file changes
  - Run 38882148286: on every leg `dotnet restore --locked-mode` restores all four projects with no lock-file error, and `git diff --exit-code -- LuaRenamer/lua/` passes. On the host-config leg the build runs under tr-TR (MSBuild's output is in Turkish), so defs generation is also shown to be culture-independent.

## 3. Triage what the legs expose

- [x] 3.1 For each failure on a new leg, decide whether the production code or the assertion is the platform- or locale-dependent one, fix that side per the testing-architecture requirements, and record the test, the leg, the cause and the fix here. Verify by re-running that leg green
  - None to fix. On the first matrix run (38882148286) every leg passed: windows, linux-x64 and linux-arm64 each had 318 total, 315 passed, 3 skipped (both canary tests and the probe), and host-config had 317 passed, 1 skipped (the probe).
- [x] 3.2 Establish whether Lua's C runtime observes the host-config leg's locale: in a throwaway test on that leg, capture `tostring(1.5)` and `os.date('%x', 0)` from a sandbox and compare them with an invariant-culture run. Record the result here, and if the C layer is unaffected, note in design.md that the leg covers the .NET layer only
  - Run 38882148286, probe reported through `Assert.Skip`:

    | Leg | .NET culture / tz | .NET `1.5` | Lua `tostring(1.5)` | `os.date('%x', 0)` | `os.date('%c', 0)` |
    |---|---|---|---|---|---|
    | linux-x64 (invariant, `LANG=C.UTF-8`) | `""` / `Etc/UTC` | `1.5` | `1.5` | `01/01/70` | `Thu Jan  1 00:00:00 1970` |
    | host-config | `tr-TR` / `America/St_Johns` | `1,5` | `1.5` | `12/31/69` | `Wed Dec 31 20:30:00 1969` |

  - Locale: the C runtime does not observe it. Lua still writes a decimal point, and `%x`/`%c` keep the C locale's layout and English names, even with `tr_TR.UTF-8` generated and `LC_ALL` set. Nothing in the .NET host calls `setlocale`, so the C library stays in the `C` locale. The leg's culture coverage is therefore .NET-only (noted in design.md).
  - Timezone: the C runtime does observe it. `os.date` renders epoch 0 as 20:30 on the previous day, i.e. UTC−3:30, because glibc's `localtime` reads `TZ` directly. The leg's timezone coverage reaches both layers.
  - The probe was reverted after recording.
- [x] 3.3 Verify the arm64 leg loads KeraLua's native `linux-arm64` binary, i.e. the `Sandbox` tests run and pass there rather than failing at load
  - Run 38882148286: the arm64 leg's test host runs as `(net10.0|arm64)`, and all 318 tests run (315 passed, 3 skipped as on the other default legs), including every `Sandbox` test and the probe's Lua chunk. An arm64 process cannot load the x64 `liblua54.so`, so KeraLua's `linux-arm64` native binary loaded.

## 4. Integration

- [ ] 4.1 Verify a PR run passes all four legs, and record the run id here
