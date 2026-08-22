## Context

See proposal.md — Why. Two pieces of current state drive the approach.

**The metatable bridge.** `LuaSandbox`'s `BaseEnv` chunk builds `env.string` as a hand-picked subset, then runs:

```lua
setmetatable(string, {__index = env.string})
```

That puts a metatable on the *real* string table so that `("a b"):cleanspaces()` finds the helper `utils.lua` defined into `env.string`. But Lua's string **value** metatable has its own `__index`, and that still points at the real string library. So a lookup on a string value hits the real table first and only falls through to `env.string` on a miss. Verified in a probe against a live sandbox:

```
same_as_env_string=False   has_dump_mt=True   has_dump_env=False   via_value_dump=True
keys=lower,unpack,char,len,pack,dump,byte,gsub,rep,find,packsize,format,gmatch,match,upper,sub,reverse
```

`dump` resolves through `('x').dump` while being absent from `env.string`. The direction of the bridge is backwards: it makes the restricted table a fallback for the unrestricted one, when it needs to be the other way round.

**The separator assertion.** `Utils.NormPath` is `Path.TrimEndingDirectorySeparator(path.Replace(Alt, Primary))`. On Unix both separator constants are `/`, so the `Replace` is a no-op and `UsesOnlyThePlatformSeparator`'s `NotContain(Alt)` becomes "contains no `/`". FsCheck's default string generator draws uniformly over codepoints 0–127; a probe found `/` in essentially every sample, so this fails deterministically off Windows rather than intermittently.

## Goals / Non-Goals

**Goals**
- Every assertion either can fail for the reason its name gives, or is restated to claim only what it checks.
- The sandbox's published surface becomes the actual reachable surface, so the pin is a boundary rather than a description of one route to it.

**Non-Goals**
- Changing when CI runs. Decided against; the separator fix stands on its own.
- Broadening the published name set. The fix removes reach, and any name a trusted chunk turns out to need is added to `env.string` deliberately, not restored by weakening the barrier.
- Reworking the generated-model generator's shrinking. Seeds do not shrink meaningfully, which is a known and accepted cost of building graphs from a `Random`.

## Decisions

### Reverse the string bridge rather than filter the real table

Set the string **value** metatable's `__index` to `env.string`:

```lua
getmetatable('').__index = env.string
```

and drop the `setmetatable(string, ...)` line. Every lookup on a string value then resolves against the published subset only, and helpers that `utils.lua` adds to `env.string` later are still found because `env.string` is never rebound — the same property the current bridge relies on.

*Alternatives considered.* Deleting `dump` from the real string table: whack-a-mole, and silently reopens whenever a runtime adds a name — which is exactly the failure mode the spec's "a name added by a future runtime version" scenario names. Leaving the leak and pinning it accurately: rejected in favor of closing it, since `dump` being currently inert (no `load` is reachable) is a property of today's allowlist rather than a guarantee.

Setting `__metatable` on the string metatable to block `getmetatable('')` is *not* part of this change: `getmetatable` is published, scripts may legitimately use it, and once `__index` points at the published table there is nothing further to reach.

### Pin the surface by probing the routes, not by enumerating one table

`EnvSurfaceTests` currently reads `Env`'s keys. It gains a second assertion that resolves names through a value of each primitive type that carries a metatable — in practice strings, the only such type in Lua — and requires that set to be a subset of the published one. Stated as a subset rather than an equality: `env.string` legitimately holds helpers (`cleanspaces`, `truncate`) beyond what the standard library defines, and equality would force the two lists to be maintained in lockstep for no benefit.

### State the separator property in terms of what normalization guarantees

Replace `NotContain(Alt)` with an assertion that the result equals the input with alternate separators rewritten — true on both platforms and still failing if `NormPath` stopped rewriting. Where the constants coincide this is trivially satisfiable, which is correct: there is nothing to rewrite.

### Route `ILuaFunc` to the callable check

`AssertValue`'s `case Delegate:` misses `LuaFunc<T>`, which is a class implementing `ILuaFunc`, so the five static `getname` fields land in `default:` → `AssertLeaf`, which accepts `string`. Add `case ILuaFunc:` alongside `case Delegate:`, both routing to `AssertCallable`. This is the check that would catch a translator regression emitting source text instead of a compiled handle.

### Make identifier spaces disagree in the arrangement

`RelocationGraph.MultiSeries` assigns `Ids.ShokoSeries + i` and `Ids.AnidbAnime + i`, so the two orderings agree and no assertion can tell them apart. Assign the Shoko ids in the reverse order, so the series with the lowest AniDB id has the highest Shoko id. `PrimaryResolutionTests` then genuinely discriminates, and any test that was passing for the wrong reason fails on this change — which is the point, and is expected to surface during the apply.

### Guard termination at graph construction

`ModelGraphs.Value` recurses into a required nested model at the depth bound, terminating only because every model-to-model cycle in the schema passes through a collection. Rather than leave that as a comment, walk `LuaSchema.LuaFields` once at static initialization for a required model-to-model reference not mediated by a collection, and throw naming the field if one exists. Cheap, runs once, and converts a stack overflow into a diagnosis.

### Assert model coverage directly

Replace `HaveCountGreaterThan(15)` — a proxy that checks the schema is large, not that it is covered — with a `[Theory]` over `ModelGraphs.ModelTypes`, giving every model type at least one checked graph. The existing property keeps its role of exploring depth and nullability combinations, but coverage no longer rests on random sampling reaching all sixteen-plus types.

## Risks / Trade-offs

- **A trusted chunk uses an unpublished string name** → `lualinq.lua` and `utils.lua` resolve string methods through the same metatable, so a name they rely on that is missing from `env.string` breaks them. The task list runs the suite immediately after the sandbox change specifically to surface this; the fix is to publish the name deliberately.
- **A user script relies on a real-string-library name** → It stops working. `string.dump` is the only name confirmed reachable-but-unpublished today; the shipped `default.lua` does not use it. This is called out as breaking in the proposal.
- **Reversing the id ordering breaks currently-green tests** → Intended. Any failure is a test that was passing on the correlation rather than on the behavior, and each one needs its assertion checked against what production actually does, not merely re-pinned to the new arrangement.
- **The metatable probe is Lua-specific and finds only strings today** → It will not discover a future reach through, say, a userdata metatable. Accepted: the model translator produces tables and primitives, and the assertion's value is in failing when the *string* route widens, which is the route that exists.

## Migration Plan

No data or configuration migration. The sandbox change ships with the plugin and takes effect on the next relocation; there is no persisted state to convert, since a fresh `lua_State` is built per relocation (`LuaRenamer.cs:150`). Rollback is reverting the `BaseEnv` chunk.

Order matters within the apply: make the sandbox change before extending the surface pin, so the pin is written against the intended surface rather than the current one.
