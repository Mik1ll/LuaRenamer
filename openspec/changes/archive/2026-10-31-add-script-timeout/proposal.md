## Why

A user script runs on the Shoko server's relocation thread with no limit on how long it runs. A mistyped loop condition, or `while true do end`, hangs that thread with no error and no recovery short of a restart.

## What Changes

- **BREAKING (script-visible)**: a user script is stopped once it has run for 15 seconds, and the relocation reports a timeout error. Catching the error with the published `pcall` does not let the script continue.
- Add tests that run non-terminating scripts under a one-second timeout, each written so a regression fails that test rather than hanging the run.
- Document the timeout in the README's "The Environment" section.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `script-sandbox`: adds a requirement that a script's running time is bounded and that exceeding it is reported as the relocation's error.

## Impact

- **Production**: the trusted run function in `LuaEnv/LuaSandbox.cs` installs a Lua count hook when given a timeout, and `LuaSandbox` gains a `TimeoutSeconds` property (default 15). `LuaRenamer.GetPath` is unchanged: a timeout surfaces as a script error through the existing path.
- **Tests**: `Tests/Sandbox/TimeoutTests.cs`.
- **Docs**: `README.md` "The Environment".
- **Risk**: a script that legitimately takes longer than 15 seconds now fails. The shipped default script runs in milliseconds.
