## 1. Timeout

- [x] 1.1 In the trusted run function, take an optional `timeout` in seconds and install a count hook that stops the chunk once `os.time()` passes the deadline. Past it, re-arm the hook at a count of 1 and error on every instruction outside the run function. Keep `tostring` on the error under the hook, in a `pcall`
- [x] 1.2 Add `LuaSandbox.TimeoutSeconds` (init, default 15) and pass it from `Run`. Trusted chunks and `CompileFunction` pass no timeout

## 2. Tests

- [x] 2.1 Add `Tests/Sandbox/TimeoutTests.cs`: non-terminating scripts under a one-second timeout, each on its own task with an xUnit `Timeout`, covering a bare loop, `pcall` retried forever, a spinning `__tostring` on the error object, and a loop in a library callback. Pin the 15-second default
- [x] 2.2 Verify the tests bite: with the re-arm at a count of 1 removed, the `pcall`-retry case times out
  - Confirmed locally: that case fails (canceled at the 10 s test timeout) and the other four pass.

## 3. Docs

- [x] 3.1 Document the timeout and the single-library-call gap in README's "The Environment"
