## Why

A review of the rebuilt test suite found assertions whose stated claim is stronger than what they check, and one that would fail outright on the platform CI runs on. The suite is currently the project's only evidence that behavior is preserved, so an assertion that cannot fail is worse than an absent one: it occupies the place where coverage is expected to be.

Three findings are load-bearing. `NormPathTests.UsesOnlyThePlatformSeparator` compares against `Path.AltDirectorySeparatorChar`, which on Linux is the same character as the primary separator — so the property asserts that a normalized path contains no `/` at all. Because `Build and Test` triggers only on `pull_request` and the rebuild branch has never been opened as one, the suite has never executed on `ubuntu-latest` and this has gone unobserved. Separately, the pinned "exact set of names a user script can reach" is not the reachable set: Lua's string metatable resolves through the *real* string library, so `('x').dump` resolves even though `dump` is deliberately excluded from the published surface.

## What Changes

- Correct `UsesOnlyThePlatformSeparator` so it states the separator contract on platforms where the primary and alternate separator characters coincide, rather than asserting a condition that is unsatisfiable there.
- **BREAKING (script-visible)**: close the string-metatable leak in `LuaSandbox` by pointing the string value metatable's `__index` at the restricted `env.string` rather than leaving it on the real string library. Names outside the published surface — `string.dump` today, plus anything a future Lua or NLua adds — stop resolving through string values. Scripts calling a real-string-library name absent from the published set will now fail.
- Extend the surface pin to walk names reachable through value metatables, not just `Env`'s own keys, so the assertion covers the surface it claims to.
- Verify callable model fields as callable. `LuaFunc<T>` is not a `Delegate`, so the five static `getname` fields fall through the marshaling property to a leaf check that accepts a raw string — a regression emitting the Lua source instead of a compiled function would pass today.
- Make the primary-series arrangement able to falsify which identifier an ordering uses. Shoko and AniDB ids currently ascend together, so "primary is the lowest AniDB id" and "primary is the lowest Shoko id" are indistinguishable.
- **Remove `FakesTests.IdentifierSpacesCannotCollide`**. Disjointness of identifier bands is a fixture convention, not a property of the system: real Shoko and AniDB identifiers are independent sequences and may legitimately coincide. The test asserts a fact about `Ids.cs` while reading as a claim about the domain. The bands themselves are kept — they remain useful for making cross-space mix-ups observable — but nothing will assert them as a system invariant.
- Pin marshaled maps to their exact key set, matching how sequences are already checked.
- Guarantee that the generated-model property visits every model type, and replace the `HaveCountGreaterThan(15)` proxy with an assertion that actually checks schema coverage.
- Guard the model-graph generator's termination assumption (every model-to-model cycle passes through a collection) so violating it fails with a diagnosis rather than a stack overflow.
- Turn `ReplacementThatIsItselfIllegalIsRejected` into the fact it is — the replacement map is validated before any segment is examined, so its generated segment and flags cannot affect the outcome.
- Close the smaller coverage gaps: illegal-character flag precedence when both `remove` and `replace` are set, an exhaustive rather than sampled permutation check, and empty-collection and floating-point shapes in the path-resolution fixture.
- Narrow the date round-trip's stated claim to what it establishes, since `os.time`/`os.date` agree only where the chosen local time exists unambiguously.

Out of scope: changing the `Build and Test` trigger. The suite will still first run on Linux at pull-request time; that was considered and deliberately left alone.

## Capabilities

### New Capabilities
- `script-sandbox`: what a user script can and cannot reach at runtime — the published surface, and the requirement that it be reachable *only* through the published names. This is production behavior that until now was described only by the test that pinned it.

### Modified Capabilities
- `testing-architecture`: four requirements change. Identifier-space disjointness is reframed from a collision guarantee into a discrimination requirement (arrangements must be able to distinguish which identifier an ordering used). Marshaling invariants gain callable verification and exact map keys. The surface-pin requirement is restated to cover metatable-reachable names. Locale and timezone independence is widened to platform independence, which is the class the separator defect belongs to.

## Impact

- **Production**: `LuaEnv/LuaSandbox.cs` — the `BaseEnv` chunk's metatable bridge. This is a behavior change to the sandbox, unlike the preceding rebuild, which was test-only.
- **Tests**: `Tests/Pure/NormPathTests.cs`, `Tests/Pure/PathCleaningTests.cs`, `Tests/Sandbox/EnvSurfaceTests.cs`, `Tests/Sandbox/EnvFixture.cs`, `Tests/Env/MarshalingTests.cs`, `Tests/Env/ModelGraphs.cs`, `Tests/Env/PrimaryResolutionTests.cs`, `Tests/Env/DateTimeTests.cs`, `Tests/Renaming/FlagsTests.cs`, `Tests/Fakes/FakesTests.cs`, `Tests/Fakes/RelocationGraph.cs`.
- **Risk**: the sandbox change affects the shipped `default.lua` and any user script; the trusted chunks (`lualinq.lua`, `utils.lua`) resolve string methods through the same metatable and must be confirmed to use only published names.
- **Unaffected**: CI workflow definitions, package versions, the generated Lua defs, and `Ids.cs`.
