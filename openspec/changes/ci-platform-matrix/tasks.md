## 1. Host-configuration canary

- [ ] 1.1 Add `Tests/Pure/HostConfigurationTests.cs`: read the expected culture name and timezone id from environment variables (e.g. `LUARENAMER_EXPECT_CULTURE`, `LUARENAMER_EXPECT_TZ`), assert them against `CultureInfo.CurrentCulture.Name` and `TimeZoneInfo.Local`, and `Assert.Skip` when unset. Verify that a plain local `dotnet test` reports both as skipped, not passed
- [ ] 1.2 Verify the canary passes locally when the variables are set to the machine's actual culture and timezone
- [ ] 1.3 Verify the canary bites: set `LUARENAMER_EXPECT_CULTURE=tr-TR` without changing the process culture and confirm the test fails naming both cultures

## 2. Matrix

- [ ] 2.1 Convert `build-and-test.yml`'s job to a matrix over the four legs in design.md, with `fail-fast: false` and `runs-on` taken from the matrix. Verify the workflow parses (`actionlint` if available, otherwise the PR run's job list shows four legs)
- [ ] 2.2 On the host-config leg, generate the locale (`sudo locale-gen tr_TR.UTF-8`) and set `LANG`, `LC_ALL`, `TZ` and the two canary variables in the leg's `env`. Verify by the leg's test output listing the canary as passed, not skipped
- [ ] 2.3 Keep restore, build, the generated-defs diff and test identical across legs, and verify each leg's log shows locked-mode restore succeeding without lock-file changes

## 3. Triage what the legs expose

- [ ] 3.1 For each failure on a new leg, decide whether the production code or the assertion is the platform- or locale-dependent one, fix that side per the testing-architecture requirements, and record the test, the leg, the cause and the fix here. Verify by re-running that leg green
- [ ] 3.2 Establish whether Lua's C runtime observes the host-config leg's locale: in a throwaway test on that leg, capture `tostring(1.5)` and `os.date('%x', 0)` from a sandbox and compare them with an invariant-culture run. Record the result here, and if the C layer is unaffected, note in design.md that the leg covers the .NET layer only
- [ ] 3.3 Verify the arm64 leg loads KeraLua's native `linux-arm64` binary, i.e. the `Sandbox` tests run and pass there rather than failing at load

## 4. Integration

- [ ] 4.1 Verify a PR run passes all four legs, and record the run id here
