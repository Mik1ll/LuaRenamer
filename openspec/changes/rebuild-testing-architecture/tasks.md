## 1. Spikes

Nothing else starts until all three are settled — each shapes the surface of the work that follows.

- [ ] 1.1 Determine whether FsCheck exposes a working xUnit v3 property attribute; verify by running one trivial property both through the attribute and through `Prop.ForAll(...).QuickCheckThrowOnFailure()` inside a `[Fact]`, and record which form the suite will use
- [ ] 1.2 Confirm a real `AnimeModel` (`required init` properties, `IReadOnlyList<T>`, enum, nested `ILuaModel`) can be constructed reflectively; verify by building one instance field-by-field through reflection and translating it without error, and record the fallback used if direct property assignment fails
- [ ] 1.3 Stress concurrent `LuaSandbox` instances across threads; verify by running many sandboxes in parallel repeatedly with no crash, no cross-state bleed, and no native-library init failure, and record whether Lua-touching tests need a non-parallel collection

## 2. Project and CI

- [ ] 2.1 Retarget `Tests/Tests.csproj` to xUnit v3, NSubstitute, AwesomeAssertions, FsCheck, and `Microsoft.Extensions.Diagnostics.Testing`; remove MSTest and Moq; verify the project builds and one placeholder test runs
- [ ] 2.2 Confirm the assertion library's current namespace and record it; verify by compiling a single assertion in each style the suite will use
- [ ] 2.3 Update `dotnet test` invocations in `.github/workflows/build-and-test.yml` and `publish-release.yml` for Microsoft.Testing.Platform; verify by running the same command locally and confirming it reports pass/fail correctly for both a passing and a deliberately failing test
- [ ] 2.4 Regenerate `Tests/packages.lock.json`; verify `dotnet restore --locked-mode` succeeds
- [ ] 2.5 Constrain `Microsoft.Extensions.Logging.Abstractions` to `[10.0.0,11.0.0)` in `LuaRenamer/LuaRenamer.csproj`; verify restore succeeds on 10.x and fails with a range-naming message when temporarily pointed at 11.x
- [ ] 2.6 Delete `TestLogAbstractionVersion`; verify no test asserts a resolved assembly version

## 3. Pure — path cleaning and path normalization

- [ ] 3.1 Add the path-cleaning property set (no illegal characters, no leading space, no trailing space or period, no reserved device name, non-empty) across all flag combinations; verify the properties pass, or record any genuine defect they expose as a separate finding rather than weakening the property
- [ ] 3.2 Add the idempotence property for cleaned segments; verify it passes over generated input
- [ ] 3.3 Add the property that a replacement mapping whose replacement is itself illegal is rejected; verify it passes
- [ ] 3.4 Port the device-name regressions as enumerated examples, preserving the asymmetries (`COM1.` rejected, `COM1test.test` accepted, superscript-digit forms rejected) and the embedded-null case; verify each example passes against path cleaning invoked directly, with no Lua state and no host graph
- [ ] 3.5 Add `NormPath` coverage (idempotent, no trailing separator); verify over generated paths
- [ ] 3.6 Delete `Tests/LuaTests.cs`'s device-name and null-character tests; verify the suite still builds

## 4. Sandbox — environment, script execution, path resolution

- [ ] 4.1 Add the path-resolution property set: grammar-generated paths resolve, absent keys and out-of-range indices and traversal through leaves return no value, mutations raise a path-validation error and never an index or reference error; verify all pass without constructing a host graph
- [ ] 4.2 Port the ten malformed-path rejection cases as named regressions; verify each raises a path-validation error
- [ ] 4.3 Cover script execution reporting: success reports no error, load failure and runtime failure and a non-string error object each report a non-empty message; verify the non-string case in particular, since silently succeeding there would let the renamer act on a stale environment
- [ ] 4.4 Pin the exact set of names reachable from a user script, including the nested standard-library subsets; verify by adding a name locally and confirming the suite fails, then reverting
- [ ] 4.5 Assert that locale mutation is not reachable from a user script; verify the assertion fails if the name is added to the environment
- [ ] 4.6 Cover string-method resolution through the environment bridge (`utils.lua` helpers reachable via method-call syntax on a string); verify without running the full pipeline
- [ ] 4.7 Add ~3 vendored-library rows reframed as load-and-resolve coverage; verify they assert the chunk loaded and a representative entry point returns, and that they do not enumerate library behavior
- [ ] 4.8 Cover line-ending handling in loaded scripts; verify the claim is asserted at the sandbox, not through a resulting file name

## 5. Fakes — arrangement factories

- [ ] 5.1 Build the host-graph factories over substitutes, wiring the series/anime cycle internally; verify a factory-produced graph drives `GetPath` to completion with a trivial script
- [ ] 5.2 Apply disjoint, non-zero identifier bands per identifier space in the factory defaults; verify by asserting no Shoko id equals any AniDB id in a default graph, and that neither is zero
- [ ] 5.3 Confirm single-field override works without restating arrangement; verify by overriding one field on a factory graph and observing the change end-to-end
- [ ] 5.4 Add the collecting fake logger; verify it captures level and message for a script `log` call
- [ ] 5.5 Confirm host metadata interfaces are referenced only from `Fakes/`; verify by inspecting imports across the other test folders

## 6. Env — producers and marshaling

- [ ] 6.1 Build the model-graph generator off `LuaSchema.LuaFields`, with explicit depth and length bounds; verify it produces varied graphs that translate without error
- [ ] 6.2 Add the presence/absence, leaf-type, sequence-integrity, and enumeration-fidelity properties; verify all pass over generated graphs
- [ ] 6.3 Add the repeatability, schema-conformance, and termination properties; verify schema conformance fails if a key is written that is not a declared field
- [ ] 6.4 Cover the title-resolution priority policy (main/official/none ordering, unofficial included only when requested); verify at the marshaling layer
- [ ] 6.5 Cover the file-slice producer logic — release-URI identifier parse and its non-matching case, per-type hash lookup with an absent type, the placeholder release-group filter, audio channel arithmetic, stream language naming; verify each against the produced model or materialized environment
- [ ] 6.6 Cover the anime-slice producer logic — name precedence between host series and source metadata, title ordering, relation recursion and its pruning, season mapping, absent end date; verify against the produced model
- [ ] 6.7 Cover primary-series and primary-episode resolution and the resulting ordering of animes, episodes, and groups; verify with generated permutations that the primary is always first
- [ ] 6.8 Add the episode-collapsing round-trip property; verify parsing the collapsed string recovers exactly the input set
- [ ] 6.9 Port the six recorded episode-collapsing examples as regressions; verify padding width, type prefixes, and range boundaries all match
- [ ] 6.10 Replace the date assertion with producer-level `DateTimeModel` field checks plus a sandbox test using explicit format specifiers; verify no assertion references a locale-defined composite format
- [ ] 6.11 Cover the enum tables exposed to scripts (identity name maps, matching the declared enumerations); verify no enumeration is missing or extra
- [ ] 6.12 Keep generator determinism coverage for the defs and names emitters; verify repeated generation is byte-identical
- [ ] 6.13 Delete `Tests/ModelTranslatorTests.cs`, `Tests/EnvModelTranslatorTests.cs`, and `Tests/FileProducerTests.cs`; verify the suite builds and the replacement coverage is green

## 7. Renaming — end-to-end

- [ ] 7.1 Cover the baseline pipeline: a script runs, sets a file name, and yields a result with a destination and subfolder; verify against a factory graph
- [ ] 7.2 Cover destination selection by name, by path, by folder reference, and unset; verify each form resolves, and that a non-destination folder is rejected with an error
- [ ] 7.3 Cover existing-location reuse: selection among differing candidate locations and the fallback when none qualifies; verify both, as neither has coverage today
- [ ] 7.4 Cover subfolder forms — string, array, sparse array, explicit indices, empty — and the default when unset; verify the default matches the name scripts see for the primary series, including its fallback
- [ ] 7.5 Cover the skip flags and the rename/move enablement flags; verify a skipped operation yields no value for that field
- [ ] 7.6 Cover script failure surfacing: a script that fails to load or throws yields an error result rather than a path; verify the error is populated
- [ ] 7.7 Cover the logging surface via the fake logger for both direct script logging and library-emitted diagnostics; verify level and message
- [ ] 7.8 Run the shipped default script against a fully populated environment; verify it completes without error and produces a file name and subfolder
- [ ] 7.9 Delete `Tests/LuaTests.cs`; verify the suite builds with no remaining MSTest or Moq references

## 8. Verification

- [ ] 8.1 Confirm every requirement in the change's spec has corresponding coverage; verify by walking each requirement's scenarios against the suite
- [ ] 8.2 Run the full suite repeatedly under parallel execution; verify it is green and stable across runs with no order dependence
- [ ] 8.3 Run the suite under a non-UTC timezone and a non-English locale; verify results are unchanged
- [ ] 8.4 Confirm CI is green end to end, including the generated-defs drift check and locked-mode restore
