## Context

See proposal.md — Why. Three structural facts shape the approach:

- **The layers are already clean; only the tests cross them.** `FilePathCleaner` and `Utils.NormPath` are pure. `LuaSandbox` needs a Lua state but no host types. `ModelTranslator` maps `ILuaModel` → `LuaTable`. `ModelProducers` maps host interfaces → `ILuaModel`. `LuaRenamer.GetPath` composes all four. Nothing needs refactoring to be testable at its own level.
- **`LuaSchema.LuaFields(Type)` already exists** as the single reflective description of "which properties of a model are Lua fields, in what order". Both the runtime serializer and the build-time emitters read models through it. A generator for model graphs can read the same walk, which is what makes property testing at the marshaling layer cheap rather than a parallel schema.
- **`Shoko.Abstractions` is a fast-moving prerelease** (alpha.43 in tree, alpha.81 already proposed). Today's Dependabot failures are lua-defs drift rather than test breakage, but arrangement duplicated across 34 tests is a latent multiplier for the first bump that does change an interface member.

## Goals / Non-Goals

**Goals:**

- Make the cost of adding a test proportional to the claim, not to the layer the claim happens to sit under.
- Give the marshaling rules a verification that is derived from the schema, so a schema change cannot silently pass.
- Confine host-interface contact to one folder, so a host bump breaks in one place.

**Non-Goals:**

- No production code changes beyond a dependency version range. The layering is adequate; this change does not refactor it.
- No coverage target. Coverage is an outcome of putting claims at the right level, not a goal to steer by.
- No Lua-level test harness. `lualinq` is frozen (see spec — *Vendored Lua libraries are not re-specified*), and `utils.lua`/`default.lua` are small enough to cover from the C# side.
- No second test project. See Decisions.

## Decisions

### Arrangement: mutable substitutes behind hand-written factories

Substitutes are configured after construction, which is the property the current `Mock.Of<>` arrangement lacks and the reason `MinimalArgs` is rebuilt five times. A factory returns a fully wired graph; a test overrides the one field it cares about in place. Cycles (`IShokoSeries.AnidbAnime ↔ IAnidbAnime.ShokoSeries`) are wired inside the factory in two passes.

Two substitution behaviors are load-bearing and are confirmed in Spike 1 before the factories are written: recursive auto-substitutes for interface-returning members, and empty results for collection-returning members. If either does not hold as expected, the factories set those members explicitly — more lines, no design change.

*Alternative — a `SceneSpec` record layer materialized into substitutes.* Considered and rejected. Its only justification was giving property tests a generable, shrinkable value. But the marshaling layer's input is `Models.cs`, not host interfaces, and the producer and end-to-end layers stay example-based, so no property test ever needs a generated host graph. The layer would have been fifteen records serving a need that does not exist.

*Alternative — a reflection-driven "stub every member non-null" base.* Rejected in favor of explicit factories. Against a fast-moving prerelease dependency, a helper that silently fills in whatever appears is exactly the mechanism that turns a host interface change into a confusing null-reference failure deep in a producer. Explicit factories fail at compile time, at the one place that needs updating.

### Identifier defaults: disjoint, non-zero ranges

The existing multi-series tests establish disjoint Shoko-id and AniDB-id ranges by hand, with a comment explaining that comparing one space against the other can then never match by coincidence. `MinimalArgs` leaves `IShokoSeries.ID` at 0, so the same class of bug is invisible in the other 30 tests. Moving this into the factory defaults makes it structural: Shoko ids from one band, AniDB ids from another, never defaulted.

### Test project topology: one project, folders by claim level

`Pure/`, `Sandbox/`, `Env/`, `Renaming/`, plus `Fakes/` and `Generators/`. The folder a test lives in states what it is allowed to touch.

*Alternative — split `LuaEnv.Tests` from `LuaRenamer.Tests`.* Considered for boundary enforcement: if the environment layer's test project compiles without a reference to the host's metadata interfaces, the boundary is proven rather than maintained by discipline. Rejected because confining those references to `Fakes/` gets most of the signal at none of the cost, and the boundary is already asserted by `LuaEnv.csproj`'s own reference list. Revisit if `Fakes/` starts leaking upward.

### Property testing: at the layers with non-circular invariants

Properties are used where an invariant can be stated without restating the implementation:

| Layer | Generated input | Why a property beats examples |
|---|---|---|
| Path cleaning | `string` × 4 flags | Six invariants over the full input space; today eight enumerated rows guard two regexes |
| Path resolution | paths from the accepted grammar, plus mutations | The parser has five distinct throw sites and a nil-tolerant walk |
| Model marshaling | `ILuaModel` graphs off `LuaSchema` | Seven invariants derived from the schema, so a schema change cannot pass silently |
| Episode collapsing | `(seriesId, number, type)[]` | Range compression is invertible, so round-trip is a real oracle |

Producers and `GetPath` stay example-based: "for any anime graph, the file name is …" has no invariant that is not a reimplementation of the producer.

The seven marshaling invariants are specified in spec.md; the one worth calling out here is **schema conformance** — every key present on a materialized model is a declared `[LuaField]` of that type. That ties `ModelTranslator` and `DefsGenerator` to the same `LuaSchema` walk, so a schema change landing in only one of them fails in the suite rather than in CI's `git diff` on the generated defs.

Model graph generation reads `LuaSchema.LuaFields(t)` and dispatches on property type — enum to a member choice, `ILuaModel` to a depth-bounded recursion, `IReadOnlyList<T>` to a sized list, `IReadOnlyDictionary<TEnum,T>` to a map, nullable to sometimes-null (the case that exercises null-becomes-absent), and the static callable fields to their declared values. Depth is bounded explicitly; the production graph terminates only because nested relation anime are built with `includeRelations: false`, which a generator does not know.

### Logger assertions: a fake logger, not a substitute

`ILogger.Log<TState>` is generic, which is why the current assertions read `It.Is<It.IsAnyType>((o,t) => o.ToString() == "test")`. A substitute produces the same shape. A collecting fake logger gives `LatestRecord.Message` instead. This is a deliberate exception to using substitutes throughout, taken for the two logging tests and any that follow.

### Dependency version guard: a restore-time range

`Microsoft.Extensions.Logging.Abstractions` must resolve to 10.x because the host supplies `ILogger` by DI and the plugin API does not itself constrain the version. Expressing this as `[10.0.0,11.0.0)` makes a major bump unresolvable: it fails at restore, before compilation, naming the package and the permitted range. Dependabot can still move within 10.x, as it did two commits ago.

*Alternative — keep the runtime assertion.* Rejected: it reports a dependency-resolution problem as a test failure, after a full build and test run, and it asserts the loaded assembly version rather than the resolved package.

### Date and time: assert fields, not rendered composites

`os.time` interprets its table as local time and `os.date` renders in local time, so the existing `%c` assertion round-trips through the same zone and passes anywhere — but it pins `strftime`'s C-locale layout, and it asserts `isdst = false` at midnight, which is the hour most likely to sit on a transition in some zone. CI runs UTC and would never show it.

Split into a producer-level assertion on `DateTimeModel` fields (the mapping that can actually regress) and a sandbox test using explicit specifiers. `os.setlocale` stays out of the sandbox: it is process-global and its effect outlives the sandbox, so a script could change `LC_TIME` for the whole host process until restart. Its absence becomes asserted rather than incidental, via the sandbox surface pin.

### Assertion library: AwesomeAssertions

Same API, MIT, and it removes the question of whether the project sits inside FluentAssertions 8's open-source exemption. The namespace the current release exposes is confirmed during setup; it affects `using` lines only.

## Risks / Trade-offs

- **FsCheck's xUnit v3 adapter may not be available or stable** → Spike 1 settles it. Fallback is a plain `[Fact]` calling `Prop.ForAll(...).QuickCheckThrowOnFailure()`, which needs no adapter; the cost is weaker failure reporting, not a design change. Because this shapes the surface of every property test, it is resolved before any are written.

- **Reflective construction of `required init` record models may fight back** → `init` accessors are reachable through `PropertyInfo.SetValue`, and `required` is expected to be compile-time-only, but the marshaling generator depends on both. Spike 1 confirms against a real `AnimeModel`. Fallbacks in order: uninitialized-instance construction then property assignment; or per-model constructor functions, trading generality for about a dozen hand-written generators.

- **Concurrent native Lua states under parallel-by-class execution** → Separate `lua_State`s should be independent, but this is P/Invoke into a native library in a CI container, and the failure mode is intermittent. Spike 2 stresses many sandboxes across many threads before the suite depends on it. Fallback is a non-parallel collection for Lua-touching tests, which costs wall time only.

- **Shrinking through a Lua VM is slow** → Generator sizes are bounded (graph depth, list length) from the start rather than tuned after the first timeout.

- **Deleting the suite before the replacement exists forfeits the regression net** → The rebuild proceeds bottom-up (`Pure/` → `Sandbox/` → `Env/` → `Renaming/`), and the old files are removed only as each level's replacement lands, so the tree is never without coverage of a level that has one.

- **Explicit factories drift as the host's interfaces move** → Accepted deliberately; see the rejected auto-stubbing alternative. Drift surfaces as a compile error in `Fakes/`, which is the intended behavior.

- **The suite gets slower** → Property tests run hundreds of cases where an example ran one, and most touch a Lua state. Mitigated by bounded sizes and by the fact that the two largest property sets (path cleaning, path resolution) need no Lua state at all.

## Migration Plan

Bottom-up, one level per step, each independently green:

1. **Spikes** — adapter, reflective construction, parallel Lua states. Nothing else starts until all three are settled.
2. **Project and CI** — retarget `Tests.csproj`, verify the Microsoft.Testing.Platform invocation locally and in both workflows, land the dependency range and drop the runtime version assertion.
3. **`Pure/`** — no Lua, no host types. Proves the property harness on the simplest layer.
4. **`Sandbox/`** — Lua, no host types. Adds the surface pin.
5. **`Fakes/` + `Env/`** — factories, then producers and marshaling.
6. **`Renaming/`** — end-to-end, including the paths that had no coverage.

Each step deletes the old tests it supersedes. Rollback at any point is reverting that step's commit; the preceding levels stay green.

## Open Questions

- Whether the sandbox surface pin asserts the flattened name set or the nested table shape. Both satisfy the requirement; the choice depends on which produces a readable diff when a name is added, and is best made against a real failure message.
