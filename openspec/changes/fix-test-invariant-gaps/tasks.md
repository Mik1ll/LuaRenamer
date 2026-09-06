## 1. The failing-on-Linux assertion

- [ ] 1.1 Restate `NormPathTests.UsesOnlyThePlatformSeparator` as "the result equals the input with alternate separators rewritten" per design.md, and verify it passes on Windows
- [ ] 1.2 Verify the new form is satisfiable where the separator constants coincide, by evaluating the assertion's condition against a Unix-semantics stand-in (both separators `/`) and confirming it holds for inputs containing `/`
- [ ] 1.3 Confirm no other assertion in the suite compares against `Path.AltDirectorySeparatorChar` or otherwise assumes the two separators differ — grep `Tests/` and check each hit

## 2. Close the sandbox leak

- [ ] 2.1 In `LuaEnv/LuaSandbox.cs`, replace `setmetatable(string, {__index = env.string})` with `getmetatable('').__index = env.string`, keeping the comment's explanation of why the bridge exists accurate to the new direction
- [ ] 2.2 Run the full suite and verify `TrustedChunkTests` still passes — `('  a   b  '):cleanspaces()` and `('abcdef'):truncate(3)` are the check that the bridge still reaches `utils.lua`'s helpers
- [ ] 2.3 Verify no trusted chunk needs an unpublished string name: run the suite and, if `lualinq.lua` or `utils.lua` fails, publish the specific name into `env.string` and record which name and why in this file
- [ ] 2.4 Verify `DefaultScriptTests` still passes, confirming the shipped `default.lua` uses only published names
- [ ] 2.5 Verify the leak is closed: a script evaluating `('x').dump` resolves to nothing, while `('x'):upper()` still works

## 3. Pin the surface that is actually reachable

- [ ] 3.1 Add an assertion to `EnvSurfaceTests` that resolves names through a string value's metatable and requires that set to be a subset of the published `string` names, per design.md
- [ ] 3.2 Verify the new assertion bites: temporarily restore the old `setmetatable(string, ...)` bridge and confirm the test fails naming `dump`, then restore the fix
- [ ] 3.3 Add `("x").dump` to the unreachable-names coverage alongside the existing process-global mutators, and verify it passes

## 4. Marshaling assertions that can fail for the stated reason

- [ ] 4.1 Add `case ILuaFunc:` to `MarshalingTests.AssertValue`, routing to `AssertCallable` alongside `case Delegate:`, and verify the suite passes
- [ ] 4.2 Verify it bites: temporarily make the translator emit a `LuaFunc`'s source string instead of a compiled handle, confirm the marshaling property fails, then revert
- [ ] 4.3 Make `AssertMap` pin the exact key set the way `AssertSequence` pins length, and verify it fails when an extra key is added to a marshaled map
- [ ] 4.4 Replace `TheGeneratorCoversEveryModelInTheSchema`'s `HaveCountGreaterThan(15)` with a `[Theory]` over `ModelGraphs.ModelTypes` that generates and checks each type, and verify every model type appears as a distinct test case
- [ ] 4.5 Add the termination guard to `ModelGraphs` per design.md — a one-time schema walk that throws naming any required model-to-model reference not mediated by a collection — and verify it passes against the current schema

## 5. Arrangements that can discriminate

- [ ] 5.1 Reverse the Shoko id assignment in `RelocationGraph.MultiSeries` so Shoko and AniDB orderings disagree, and verify the arrangement's ids are ordered oppositely
- [ ] 5.2 Run the suite and, for each test that now fails, determine which identifier production actually uses and correct the assertion to name it — record each in this file rather than re-pinning to the new arrangement
- [ ] 5.3 Verify `ThePrimarySeriesIsTheLowestSourceIdRegardlessOfArrivalOrder` now discriminates: flip the assertion to the other identifier space and confirm it fails, then flip it back
- [ ] 5.4 Apply the same treatment to `TwoSeriesWithTmdbOnThePrimaryOne` and verify `TmdbIsSourcedFromThePrimarySeriesNotTheFirstOneListed` still asserts the primary series rather than the first-listed one

## 6. Remove the unfounded identifier test

- [ ] 6.1 Delete `FakesTests.IdentifierSpacesCannotCollide` and verify the suite passes with one fewer test
- [ ] 6.2 Update the doc comment on `Tests/Fakes/Ids.cs` so the bands read as an arrangement convention that keeps cross-space mix-ups observable, not as an asserted system invariant, and verify `Ids.cs` itself is otherwise unchanged

## 7. Assertions that claim more than they check

- [ ] 7.1 Convert `PathCleaningTests.ReplacementThatIsItselfIllegalIsRejected` to a `[Fact]`, since the replacement map is validated before any segment is examined — verify by confirming the outcome is independent of the segment and both flags
- [ ] 7.2 Narrow the `DateTimeTests.ScriptSideFormattingUsesExplicitSpecifiers` comment to what the test establishes: the round-trip holds where the chosen local time exists unambiguously, which is not every zone for every instant
- [ ] 7.3 Replace `PrimaryResolutionTests.Permutations` sampling with a `[Theory]` over all 24 permutations of four series, and verify the count of executed cases

## 8. Coverage gaps

- [ ] 8.1 Add a `FlagsTests` case pinning precedence when `remove_illegal_chars` and `replace_illegal_chars` are both set, and verify it matches the implementation's behavior (remove wins)
- [ ] 8.2 Add an empty sequence, an empty map, and a floating-point leaf to `EnvFixture.Root`, and verify the generated path spaces exercise them — including an index past the end of an empty sequence
- [ ] 8.3 Record in this file that FsCheck's default string generator emits only codepoints 0–127, so non-ASCII path cases (fullwidth replacements, superscript device names) are covered by example rather than by property — no code change, but the limit should be written down where the next reader will look

## 9. Verification

- [ ] 9.1 Run the full suite and verify it passes, noting the new total against the previous 184
- [ ] 9.2 Run the suite with the culture matrix used previously (de-DE, ja-JP, tr-TR, ar-SA, invariant) and verify results are unchanged
- [ ] 9.3 Run the suite 15 consecutive times with randomized ordering and verify no flakes
- [ ] 9.4 Build the full solution and verify zero warnings and zero errors
- [ ] 9.5 Verify the generated Lua defs are unchanged, confirming the sandbox change did not alter the schema or its emitted artifacts
- [ ] 9.6 Walk each requirement in both delta specs and confirm a test or an observable behavior satisfies it; record any that do not
