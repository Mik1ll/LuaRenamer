## Context

`LuaSandbox.Run` loads a user script against the restricted env and pcalls it from a trusted Lua run function. Nothing bounds how long that takes.

## Goals / Non-Goals

**Goals:**
- Stop a script that runs too long, with an error that says it timed out.
- Keep a script from catching that error and carrying on.
- Add as little code as possible: no managed callbacks, no new types.

**Non-Goals:**
- Bounding memory, or the work done inside a single library call.
- Making the timeout user-configurable.

## Decisions

**A Lua count hook, installed by the trusted run function.** Every 1,000 instructions it compares `os.time()` against a deadline. It is Lua rather than a managed hook because raising a Lua error from managed code would longjmp across .NET frames. `os.time()` is wall-clock time, unlike `os.clock()`, which counts CPU time for the whole Shoko process. Its one-second resolution is fine for a 15-second limit: a script is stopped up to a second late. Alternatives: running the script on a separate thread and abandoning it leaves a Lua state still in use on that thread, which can't safely be disposed.

**Past the deadline, the hook errors on every instruction outside the run function.** It re-arms with a count of 1 and errors unless the running function is `run` itself. So `while true do pcall(spin) end` and a spinning `__tostring` on the error object both stop: the script's own frames can never get past an instruction, while `run` can return the error.

**`tostring` on the error stays under the hook**, inside a `pcall`, because a `__tostring` metamethod is script code.

**The timeout is a `TimeoutSeconds` init property, default 15.** Tests set it to 1, so each non-terminating case takes one to two seconds. Trusted chunks and `CompileFunction` run without it.

## Risks / Trade-offs

- [A single library call runs no Lua instructions, so the hook can't interrupt it.] → A pathological pattern match, or a C loop over a huge empty range, runs until it returns. Documented in the README; such calls are rare in practice.
- [The hook adds a check every 1,000 instructions.] → An `os.time()` call per thousand instructions is negligible next to the instructions themselves.
