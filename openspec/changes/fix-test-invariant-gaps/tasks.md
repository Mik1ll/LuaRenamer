## 1. The failing-on-Linux assertion

- [x] 1.1 Restate `NormPathTests.UsesOnlyThePlatformSeparator` as "the result is a prefix of the input with alternate separators rewritten" per design.md, and verify it passes on Windows, including for inputs ending in one or more separators
- [x] 1.2 Verify the new form is satisfiable where the separator constants coincide, by evaluating the assertion's condition against a Unix-semantics stand-in (both separators `/`) and confirming it holds for inputs containing `/`
- [x] 1.3 Confirm no other assertion in the suite compares against `Path.AltDirectorySeparatorChar` or otherwise assumes the two separators differ — grep `Tests/` and check each hit
- [ ] 1.4 Push and verify `Build and Test` on PR #175 passes on `ubuntu-latest`, where it currently fails on `UsesOnlyThePlatformSeparator`

## 2. Close the sandbox leak

- [x] 2.1 In `LuaEnv/LuaSandbox.cs`, replace `setmetatable(string, {__index = env.string})` with `getmetatable('').__index = env.string`, keeping the comment's explanation of why the bridge exists accurate to the new direction
- [x] 2.2 Run the full suite and verify `TrustedChunkTests` still passes — `('  a   b  '):cleanspaces()` and `('abcdef'):truncate(3)` are the check that the bridge still reaches `utils.lua`'s helpers
- [x] 2.3 Verify no trusted chunk needs an unpublished string name: run the suite and, if `lualinq.lua` or `utils.lua` fails, publish the specific name into `env.string` and record which name and why in this file
  - None needed. The real string library differs from `env.string` only by `dump`, and the one `dump` in the shipped Lua is lualinq's own `Linq:dump()` table method.
- [x] 2.4 Verify `DefaultScriptTests` still passes, confirming the shipped `default.lua` uses only published names
- [x] 2.5 Verify the leak is closed: a script evaluating `('x').dump` resolves to nothing, while `('x'):upper()` still works

## 3. Pin the surface that is actually reachable

- [x] 3.1 Add an assertion to `EnvSurfaceTests` that resolves names through a string value's metatable and requires that set to be a subset of the published `string` names, per design.md
- [x] 3.2 Verify the new assertion bites: temporarily restore the old `setmetatable(string, ...)` bridge and confirm the test fails naming `dump`, then restore the fix
- [x] 3.3 Add `("x").dump` to the unreachable-names coverage alongside the existing process-global mutators, and verify it passes

## 4. Marshaling assertions that can fail for the stated reason

- [x] 4.1 Add `case ILuaFunc:` to `MarshalingTests.AssertValue`, routing to `AssertCallable` alongside `case Delegate:`, and verify the suite passes
- [x] 4.2 Verify it bites: temporarily make the translator emit a `LuaFunc`'s source string instead of a compiled handle, confirm the marshaling property fails, then revert
- [x] 4.3 Make `AssertMap` pin the exact key set the way `AssertSequence` pins length, and verify it fails when an extra key is added to a marshaled map
- [x] 4.4 Replace `TheGeneratorCoversEveryModelInTheSchema`'s `HaveCountGreaterThan(15)` with a `[Theory]` over `ModelGraphs.ModelTypes` that generates and checks each type, and verify every model type appears as a distinct test case
- [x] 4.5 Add the termination guard to `ModelGraphs` per design.md — a one-time schema walk that throws naming any required model-to-model reference not mediated by a collection — and verify it passes against the current schema

## 5. Arrangements that can discriminate

- [x] 5.1 Reverse the Shoko id assignment in `RelocationGraph.MultiSeries` so Shoko and AniDB orderings disagree, and verify the arrangement's ids are ordered oppositely
- [x] 5.2 Run the suite and, for each test that now fails, determine which identifier production actually uses and correct the assertion to name it — record each in this file rather than re-pinning to the new arrangement
  - None failed: every assertion already named the identifier production uses (AniDB for series and episode selection, Shoko `LocalID` for group membership and TMDB episode filtering). Reversing the Shoko ids for both series and episodes changed no result. Temporarily ordering `PrimarySeries` by `LocalID` then failed all three permutation properties, and after 5.4, `TmdbIsSourcedFromThePrimarySeriesNotTheFirstOneListed` too.
- [x] 5.3 Verify `ThePrimarySeriesIsTheLowestSourceIdRegardlessOfArrivalOrder` now discriminates: flip the assertion to the other identifier space and confirm it fails, then flip it back
- [x] 5.4 Apply the same treatment to `TwoSeriesWithTmdbOnThePrimaryOne` and verify `TmdbIsSourcedFromThePrimarySeriesNotTheFirstOneListed` still asserts the primary series rather than the first-listed one

## 6. Remove the unfounded identifier test

- [x] 6.1 Delete `FakesTests.IdentifierSpacesCannotCollide` and verify the suite passes with one fewer test
- [x] 6.2 Update the doc comment on `Tests/Fakes/Ids.cs` so the bands read as an arrangement convention that keeps cross-space mix-ups observable, not as an asserted system invariant, and verify `Ids.cs` itself is otherwise unchanged

## 7. Assertions that claim more than they check

- [x] 7.1 Convert `PathCleaningTests.ReplacementThatIsItselfIllegalIsRejected` to a `[Fact]`, since the replacement map is validated before any segment is examined — verify by confirming the outcome is independent of the segment and both flags
- [x] 7.2 Narrow the `DateTimeTests.ScriptSideFormattingUsesExplicitSpecifiers` comment to what the test establishes: the round-trip holds where the chosen local time exists unambiguously, which is not every zone for every instant
- [x] 7.3 Replace `PrimaryResolutionTests.Permutations` sampling with a `[Theory]` over all 24 permutations of four series, and verify the count of executed cases

## 8. Coverage gaps

- [x] 8.1 Add a `FlagsTests` case pinning precedence when `remove_illegal_chars` and `replace_illegal_chars` are both set, and verify it matches the implementation's behavior (remove wins)
- [x] 8.2 Add an empty sequence, an empty map, and a floating-point leaf to `EnvFixture.Root`, and verify the generated path spaces exercise them — including an index past the end of an empty sequence
- [x] 8.3 Record in this file that FsCheck's default string generator emits only codepoints 0–127, so non-ASCII path cases (fullwidth replacements, superscript device names) are covered by example rather than by property — no code change, but the limit should be written down where the next reader will look
  - Confirmed against FsCheck 3.4.0: 20,000 sampled default strings drew codepoints 0–127 only (all 128 values seen). Every `NonNull<string>` property in `NormPathTests` and `PathCleaningTests` therefore runs over ASCII only. Fullwidth replacements (`？`, `＼`, `／`) are covered by example in `FlagsTests` and `SubfolderTests`, and superscript device names (`COM¹`–`COM³`) in `PathCleaningTests.ReservedDeviceNames`. A non-ASCII case that needs property coverage needs its own generator.

## 9. Verification

- [x] 9.1 Run the full suite and verify it passes, noting the new total against the previous 199
  - 291 passed. Changes from 199: +2 string-route surface tests, +1 map-key guard, +20 for the per-type coverage theory (21 cases replacing one fact), −1 for the deleted collision test, +69 for the permutation theories (3 × 24 cases replacing three sampled properties), +1 flag-precedence row.
- [x] 9.2 Run the suite with the culture matrix used previously (de-DE, ja-JP, tr-TR, ar-SA, invariant) and verify results are unchanged
  - 291/291 under each culture (xUnit's `-culture`).
- [x] 9.3 Run the suite 15 consecutive times with randomized ordering and verify no flakes
  - 15/15 green, each with a distinct random `:seed`, under `-parallelMode all -maxThreads 16`.
- [x] 9.4 Build the full solution and verify zero warnings and zero errors
  - The first rebuild had one warning, CA1873 at `LuaRenamer/LuaRenamer.cs:92`: the collision-filename `LogInformation` from #172, already present before this change. It is now guarded by `IsEnabled` in a separate commit. The rebuild is clean.
- [x] 9.5 Verify the generated Lua defs are unchanged, confirming the sandbox change did not alter the schema or its emitted artifacts
  - `git diff --exit-code -- LuaRenamer/lua/` is clean after a full rebuild.
- [x] 9.6 Walk each requirement in both delta specs and confirm a test or an observable behavior satisfies it; record any that do not
  - script-sandbox: the published names are pinned by `TopLevelNamesAreExactly` and `NestedStandardLibrarySubsetsAreExactly`. Unpublished and future names on the string route are covered by `NamesReachableThroughAStringValueArePublished` and `StringDumpIsUnreachableThroughAStringValue`. The escape routes are covered by `ProcessGlobalMutatorsAreUnreachable`, and the method-call helpers by `TrustedChunkTests`.
  - **Observed, but no permanent test:** "A script cannot affect a later relocation". It holds by construction (a fresh `LuaSandbox` per `GetPath`, `LuaRenamer.cs:180`), and a throwaway test confirmed it: one relocation mutated the string metatable, `string.lower` and `from`, and the next saw the originals. No test in the suite pins it.
  - **Observed, but no permanent test:** "A schema shape that would not terminate". A throwaway test fed the guard a two-type cycle of required fields; it threw naming `ZzCycleB.a`, and accepted a cycle broken by a nullable field. The suite only shows that the guard passes the current schema.
  - testing-architecture: platform scenarios are covered by the restated `UsesOnlyThePlatformSeparator` (checked against a Unix stand-in, see 1.2) and by `PathCleaningTests.WindowsRules`. "Running on the continuous-integration platform" waits on 1.4. Discrimination is covered by `MultiSeries` and `TwoSeriesWithTmdbOnThePrimaryOne` (see 5.2–5.4). For the cross-space scenario, temporarily comparing `primarySeries.LocalID` against an AniDB anime id in `EnvToModel` failed 129 tests on the default arrangements. No test asserts disjointness any more. The marshaling scenarios are each covered by `MarshalingTests` (callables, exact map keys, and per-type coverage are new here). The surface scenarios are covered by `EnvSurfaceTests`.
