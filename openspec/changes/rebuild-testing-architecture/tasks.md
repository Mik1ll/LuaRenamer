## 1. Spikes

Nothing else starts until all three are settled — each shapes the surface of the work that follows.

- [x] 1.1 Determine whether FsCheck exposes a working xUnit v3 property attribute; verify by running one trivial property both through the attribute and through `Prop.ForAll(...).QuickCheckThrowOnFailure()` inside a `[Fact]`, and record which form the suite will use
- [x] 1.2 Confirm a real `AnimeModel` (`required init` properties, `IReadOnlyList<T>`, enum, nested `ILuaModel`) can be constructed reflectively; verify by building one instance field-by-field through reflection and translating it without error, and record the fallback used if direct property assignment fails
- [x] 1.3 Stress concurrent `LuaSandbox` instances across threads; verify by running many sandboxes in parallel repeatedly with no crash, no cross-state bleed, and no native-library init failure, and record whether Lua-touching tests need a non-parallel collection

**Findings** (all three settled; no fallback taken):

- **1.1** `FsCheck.Xunit.v3` 3.4.0 ships a working `[Property]` attribute against xunit.v3 4.0.0. Both forms ran green, so the suite uses the attribute; `Prop.ForAll(...).QuickCheckThrowOnFailure()` inside a `[Fact]` stays available for properties that need custom arbitraries inline.
- **1.2** `Activator.CreateInstance(typeof(AnimeModel))` succeeds and every `[LuaField]` `init` property is writable through `PropertyInfo.SetValue` — `required` is compile-time only. Neither fallback (uninitialized-instance construction, per-model constructor functions) is needed.
- **1.3** 5 rounds x 64 parallel sandboxes at DOP 16, plus a second test class running concurrently, ran clean with no crash, no cross-state bleed, and no native init failure. Lua-touching tests need no non-parallel collection.
- **Also settled here**: `dotnet test` reaches Microsoft.Testing.Platform only when the repo opts in via `global.json` (`"test": { "runner": "Microsoft.Testing.Platform" }`); without it the SDK errors out on the retired VSTest path. The existing workflow flags (`--no-restore --no-build --verbosity normal`) keep working once that file exists, and a failing test exits 2.

## 2. Project and CI

- [x] 2.1 Retarget `Tests/Tests.csproj` to xUnit v3, NSubstitute, AwesomeAssertions, FsCheck, and `Microsoft.Extensions.Diagnostics.Testing`; remove MSTest and Moq; verify the project builds and one placeholder test runs
- [x] 2.2 Confirm the assertion library's current namespace and record it; verify by compiling a single assertion in each style the suite will use
- [x] 2.3 Update `dotnet test` invocations in `.github/workflows/build-and-test.yml` and `publish-release.yml` for Microsoft.Testing.Platform; verify by running the same command locally and confirming it reports pass/fail correctly for both a passing and a deliberately failing test
- [x] 2.4 Regenerate `Tests/packages.lock.json`; verify `dotnet restore --locked-mode` succeeds
- [x] 2.5 Constrain `Microsoft.Extensions.Logging.Abstractions` to `[10.0.0,11.0.0)` in `LuaRenamer/LuaRenamer.csproj`; verify restore succeeds on 10.x and fails with a range-naming message when temporarily pointed at 11.x
- [x] 2.6 Delete `TestLogAbstractionVersion`; verify no test asserts a resolved assembly version

**Notes**: the assertion namespace is `AwesomeAssertions` (2.2). The Microsoft.Testing.Platform opt-in turned out to live in `global.json`, not in the `dotnet test` flags, so the workflow commands are unchanged and carry a comment pointing at it (2.3). The range landed as `[10.0.11,11.0.0)` rather than the design's `[10.0.0,11.0.0)`: the lower literal resolves to 10.0.0 and would have silently undone the Dependabot bump to 10.0.11 that the design itself cites (2.5).

## 3. Pure — path cleaning and path normalization

- [x] 3.1 Add the path-cleaning property set (no illegal characters, no leading space, no trailing space or period, no reserved device name, non-empty) across all flag combinations; verify the properties pass, or record any genuine defect they expose as a separate finding rather than weakening the property
- [x] 3.2 Add the idempotence property for cleaned segments; verify it passes over generated input
- [x] 3.3 Add the property that a replacement mapping whose replacement is itself illegal is rejected; verify it passes
- [x] 3.4 Port the device-name regressions as enumerated examples, preserving the asymmetries (`COM1.` rejected, `COM1test.test` accepted, superscript-digit forms rejected) and the embedded-null case; verify each example passes against path cleaning invoked directly, with no Lua state and no host graph
- [x] 3.5 Add `NormPath` coverage (idempotent, no trailing separator); verify over generated paths
- [x] 3.6 Delete `Tests/LuaTests.cs`'s device-name and null-character tests; verify the suite still builds

## 4. Sandbox — environment, script execution, path resolution

- [x] 4.1 Add the path-resolution property set: grammar-generated paths resolve, absent keys and out-of-range indices and traversal through leaves return no value, mutations raise a path-validation error and never an index or reference error; verify all pass without constructing a host graph
- [x] 4.2 Port the ten malformed-path rejection cases as named regressions; verify each raises a path-validation error
- [x] 4.3 Cover script execution reporting: success reports no error, load failure and runtime failure and a non-string error object each report a non-empty message; verify the non-string case in particular, since silently succeeding there would let the renamer act on a stale environment
- [x] 4.4 Pin the exact set of names reachable from a user script, including the nested standard-library subsets; verify by adding a name locally and confirming the suite fails, then reverting
- [x] 4.5 Assert that locale mutation is not reachable from a user script; verify the assertion fails if the name is added to the environment
- [x] 4.6 Cover string-method resolution through the environment bridge (`utils.lua` helpers reachable via method-call syntax on a string); verify without running the full pipeline
- [x] 4.7 Add ~3 vendored-library rows reframed as load-and-resolve coverage; verify they assert the chunk loaded and a representative entry point returns, and that they do not enumerate library behavior
- [x] 4.8 Cover line-ending handling in loaded scripts; verify the claim is asserted at the sandbox, not through a resulting file name

## 5. Fakes — arrangement factories

- [x] 5.1 Build the host-graph factories over substitutes, wiring the series/anime cycle internally; verify a factory-produced graph drives `GetPath` to completion with a trivial script
- [x] 5.2 Apply disjoint, non-zero identifier bands per identifier space in the factory defaults; verify by asserting no Shoko id equals any AniDB id in a default graph, and that neither is zero
- [x] 5.3 Confirm single-field override works without restating arrangement; verify by overriding one field on a factory graph and observing the change end-to-end
- [x] 5.4 Add the collecting fake logger; verify it captures level and message for a script `log` call
- [x] 5.5 Confirm host metadata interfaces are referenced only from `Fakes/`; verify by inspecting imports across the other test folders

## 6. Env — producers and marshaling

- [x] 6.1 Build the model-graph generator off `LuaSchema.LuaFields`, with explicit depth and length bounds; verify it produces varied graphs that translate without error
- [x] 6.2 Add the presence/absence, leaf-type, sequence-integrity, and enumeration-fidelity properties; verify all pass over generated graphs
- [x] 6.3 Add the repeatability, schema-conformance, and termination properties; verify schema conformance fails if a key is written that is not a declared field
- [x] 6.4 Cover the title-resolution priority policy (main/official/none ordering, unofficial included only when requested); verify at the marshaling layer
- [x] 6.5 Cover the file-slice producer logic — release-URI identifier parse and its non-matching case, per-type hash lookup with an absent type, the placeholder release-group filter, audio channel arithmetic, stream language naming; verify each against the produced model or materialized environment
- [x] 6.6 Cover the anime-slice producer logic — name precedence between host series and source metadata, title ordering, relation recursion and its pruning, season mapping, absent end date; verify against the produced model
- [x] 6.7 Cover primary-series and primary-episode resolution and the resulting ordering of animes, episodes, and groups; verify with generated permutations that the primary is always first
- [x] 6.8 Add the episode-collapsing round-trip property; verify parsing the collapsed string recovers exactly the input set
- [x] 6.9 Port the six recorded episode-collapsing examples as regressions; verify padding width, type prefixes, and range boundaries all match
- [x] 6.10 Replace the date assertion with producer-level `DateTimeModel` field checks plus a sandbox test using explicit format specifiers; verify no assertion references a locale-defined composite format
- [x] 6.11 Cover the enum tables exposed to scripts (identity name maps, matching the declared enumerations); verify no enumeration is missing or extra
- [x] 6.12 Keep generator determinism coverage for the defs and names emitters; verify repeated generation is byte-identical
- [x] 6.13 Delete `Tests/ModelTranslatorTests.cs`, `Tests/EnvModelTranslatorTests.cs`, and `Tests/FileProducerTests.cs`; verify the suite builds and the replacement coverage is green

## 7. Renaming — end-to-end

- [x] 7.1 Cover the baseline pipeline: a script runs, sets a file name, and yields a result with a destination and subfolder; verify against a factory graph
- [x] 7.2 Cover destination selection by name, by path, by folder reference, and unset; verify each form resolves, and that a non-destination folder is rejected with an error
- [x] 7.3 Cover existing-location reuse: selection among differing candidate locations and the fallback when none qualifies; verify both, as neither has coverage today
- [x] 7.4 Cover subfolder forms — string, array, sparse array, explicit indices, empty — and the default when unset; verify the default matches the name scripts see for the primary series, including its fallback
- [x] 7.5 Cover the skip flags and the rename/move enablement flags; verify a skipped operation yields no value for that field
- [x] 7.6 Cover script failure surfacing: a script that fails to load or throws yields an error result rather than a path; verify the error is populated
- [x] 7.7 Cover the logging surface via the fake logger for both direct script logging and library-emitted diagnostics; verify level and message
- [x] 7.8 Run the shipped default script against a fully populated environment; verify it completes without error and produces a file name and subfolder
- [x] 7.9 Delete `Tests/LuaTests.cs`; verify the suite builds with no remaining MSTest or Moq references

**Note on `GetExistingAnimeLocation`'s folder filter (7.3):** the filter reads
`DropFolderType.HasFlag(Destination) || DropFolderType.HasFlag(Excluded)`, and because `DropFolderType.Excluded` is `0` today, `HasFlag(Excluded)` is true for every value — so in the current package version *any* folder qualifies, a Source-only one included. Confirmed intentional: the clause is written against the enum's *names* rather than its numeric values, so it keeps stating the intent if Shoko renumbers the enum. `AnExcludedFolderStillCountsAsAnExistingLocation` covers the intent; the "no candidate qualifies" fallback is exercised with a file at a folder root (no subfolder to reuse) instead of a Source folder, so the coverage does not depend on which reading holds.


## 8. Verification

- [x] 8.1 Confirm every requirement in the change's spec has corresponding coverage; verify by walking each requirement's scenarios against the suite
- [x] 8.2 Run the full suite repeatedly under parallel execution; verify it is green and stable across runs with no order dependence
- [x] 8.3 Run the suite under a non-UTC timezone and a non-English locale; verify results are unchanged
- [x] 8.4 Confirm CI is green end to end, including the generated-defs drift check and locked-mode restore

**Verification notes:**

- **8.1** The walk found one real gap: the `Env/` and `Renaming/` tests had grown their own host-graph arrangement, breaking the "host metadata interfaces only in `Fakes/`" requirement. Fixed by moving that arrangement into `HostFakes`/`RelocationGraph` as focused factories (`AnimeWithTitles`, `AnimeInARelationCycle`, `SeriesWithTags`, `FileWith`, `FileReleasedOn`, `MultiSeries`, `SetEpisodes`, `SetSeriesAndEpisodes`, `AddFolder`, `GiveSeriesExistingFiles`). The metadata-interface namespaces are now imported only by `Tests/Fakes/HostFakes.cs` and `Tests/Fakes/RelocationGraph.cs`; what remains elsewhere is the shared enum namespaces (which `LuaEnv` itself depends on) and `RelocationResult`.
- **8.2** 20 consecutive runs green (re-run after the factory fix below), plus `-parallelMode all -maxThreads 16` and randomized execution order (`:seed`). No order dependence.
- **8.3** Green under `de-DE`, `ja-JP`, `tr-TR`, `ar-SA` and `invariant` (xUnit's `-culture`). Timezone: this host runs US/Pacific (non-UTC, DST-observing) and CI runs UTC, so the two together cover the dimension. Note that `TZ` is *not* honored on Windows by either the CLR or the C runtime Lua uses, so setting it here proves nothing — the CI run is what supplies the second zone.
  - **8.3 finding**: the first culture sweep exposed a flaky property, which turned out to be locale-independent — see the `NormPath` finding below.
- **8.4** The workflow's steps run green locally with `CI=true`: `dotnet restore --locked-mode`, `dotnet build --no-restore`, `git diff --exit-code -- LuaRenamer/lua/` (no defs drift), `dotnet test --no-restore --no-build --verbosity normal` (184 passed).

**Finding, filed as [Mik1ll/LuaRenamer#170](https://github.com/Mik1ll/LuaRenamer/issues/170):** `Utils.NormPath` is built on `Path.TrimEndingDirectorySeparator`, which trims exactly one trailing separator. A path ending in repeated separators therefore stays separator-terminated (`"a\\"` -> `"a\"`) and normalizing again changes it, so `NormPath` is neither idempotent nor trailing-separator-free for that input. Reproduced end to end: a `destination` path ending in a doubled separator fails to match the folder it names. Surfaced by the 3.5 properties, which exclude that input explicitly and point at the issue; removing the exclusion is the regression test.

**Finding, fixed in this change:** the arrangement factories hit NSubstitute's last-call rule from the *argument* side — `file.ManagedFolderID.Returns(folder.ID)` binds the value to `folder.ID`, because a `Returns()` argument is evaluated after the receiver. Seven members were affected; the damaging one left `graph.Folder.Path` holding the *file's* path, and the rest left cross-reference ids at `0`, which quietly weakened the episode- and group-ordering coverage without failing anything. Fixed by hoisting the reads, and `CrossReferencesInTheGraphAgreeWithWhatTheyPointAt` now pins every one of them.
