## Why

The current suite (4 files, 1738 lines, MSTest + Moq) tests two thirds of its claims through the wrong layer. Regex behavior, path-parser rejection, and producer mapping are all asserted by running the entire `LuaRenamer.GetPath` pipeline against a hand-built Shoko object graph. Because `Mock.Of<>` freezes its stubs at construction, there is no way to say "the minimal graph, but with one field changed" — so `MinimalArgs` is rebuilt near-verbatim five times, and every new test starts with ~15 lines of mock setup. The arrangement cost is what caps coverage: `GetNewDestination`, `GetExistingAnimeLocation`, and `default.lua` against a populated env are untested because arranging for them is too expensive.

## What Changes

- **BREAKING (tests only)**: delete all four existing test files and every piece of arrangement code in them. Assertions are re-derived from the claims worth keeping, not ported.
- Replace MSTest 4 with **xUnit v3**, Moq with **NSubstitute**, and add **AwesomeAssertions** and **FsCheck 3**.
- Arrangement moves to `Fakes/` — roughly twelve hand-written factories over NSubstitute's auto-stubbing substitutes. Substitutes stay mutable, so a test tweaks one field in place instead of rebuilding a graph. No parallel spec/builder record hierarchy.
- Test defaults draw Shoko ids and AniDB ids from **disjoint, non-zero ranges**, so cross-id-space mix-ups can never match by coincidence. Today only four tests get this; the current `MinimalArgs` leaves `IShokoSeries.ID` at 0, where such bugs are invisible.
- Tests are organized into one project by **claim level** (`Pure/`, `Sandbox/`, `Env/`, `Renaming/`), so each test runs at the layer it actually makes a claim about.
- Property-based tests via FsCheck at the layers with real invariants: `FilePathCleaner` (6 invariants), `LuaSandbox.ParseKeys`/`GetValue`, the `ModelTranslator` marshaling rules (7 invariants, generated over `ILuaModel` off the existing `LuaSchema` walk), and `ModelProducers.EpisodeNumbers` (round-trip). Example tests stay the primary form at the producer and end-to-end layers.
- `ILogger` interaction assertions move to `FakeLogger<T>` (`Microsoft.Extensions.Diagnostics.Testing`), replacing the `It.Is<It.IsAnyType>((o,t) => o.ToString() == ...)` incantations.
- New coverage for `GetNewDestination`, `GetExistingAnimeLocation`, `default.lua` executed against a fully-populated env, and a pin on the exact set of names reachable in `LuaSandbox.Env`.
- `lualinq` is treated as frozen: its 12-row behavior matrix drops to ~3 rows reframed as "the trusted chunk loads into `Env` and its functions resolve".
- `TestLogAbstractionVersion` is removed and replaced by a NuGet version range `[10.0.0,11.0.0)` on `Microsoft.Extensions.Logging.Abstractions`, so a major bump fails at restore rather than as a test failure after a full build.
- `TestDateTime` splits into a producer-level assertion on `DateTimeModel` fields and a sandbox test using explicit `os.date` specifiers, dropping the dependence on `strftime`'s C-locale `%c` layout and its latent midnight/DST exposure.
- CI's `dotnet test` invocation is updated for Microsoft.Testing.Platform, which xUnit v3 builds on.

## Capabilities

### New Capabilities
- `testing-architecture`: the durable contract for the test suite — which layer a claim is tested at, how arrangement is obtained, which invariants are property-based rather than example-based, and which guards live outside the suite entirely.

### Modified Capabilities
<!-- None. No production behavior changes: this change rebuilds how existing behavior is
     verified. The one production-file edit (a NuGet version range) is a dependency
     constraint, not a behavioral requirement. -->

## Impact

- **Deleted**: `Tests/LuaTests.cs`, `Tests/ModelTranslatorTests.cs`, `Tests/EnvModelTranslatorTests.cs`, `Tests/FileProducerTests.cs`.
- **Rewritten**: `Tests/Tests.csproj` — xUnit v3 (`OutputType` becomes `Exe`), NSubstitute, AwesomeAssertions, FsCheck, `Microsoft.Extensions.Diagnostics.Testing`; MSTest and Moq removed. `Tests/packages.lock.json` regenerates.
- **Edited**: `LuaRenamer/LuaRenamer.csproj` (logging-abstractions version range); `.github/workflows/build-and-test.yml` and `publish-release.yml` (`dotnet test` invocation under Microsoft.Testing.Platform).
- **Unchanged**: all production code under `LuaRenamer/`, `LuaEnv/`, and `DefsGenerator/`, and every `.lua` source. The `LuaEnv`/`LuaRenamer` boundary is enforced by confining Shoko metadata references to `Fakes/` rather than by splitting the project.
- **Risks needing a spike before the bulk of the work**: FsCheck's xUnit v3 adapter availability, reflective construction of `required init` record models, and concurrent native `lua_State`s under xUnit v3's parallel-by-class default.
