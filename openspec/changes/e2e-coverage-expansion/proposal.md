# E2E coverage expansion — Integration scorecard 7 → 8

## Why

`audit v2.1 → scorecard_integration_detail` documented the path to lift the
Integration coverage score:

- **path-to-7** (~~proposed~~ **already shipped**): an E2E test that closes stdin
  on a running bundled-JRE signal-cli instance and asserts graceful exit within
  `StopTimeoutSeconds`. Verified at audit v2.1 time as already covered by
  `SignalCliE2EGracefulShutdownTests.Process_GracefulShutdown_ExitsWithoutKillTimeout`
  (file `Tests/SignalCli.Tests.Integration/SignalCliE2EGracefulShutdownTests.cs`),
  shipped in commit 706cf27 ("test(4.0.1): close last follow-up — 5 skip-gated
  E2E integration tests"). Re-reading the file confirms: the test asserts
  `stopDuration < 4s` (sub-1s typical for graceful EOF), checks the OS process
  is gone via `Process.GetProcessById`, and uses the bundled-runtime auto-resolve
  path. **Integration score is therefore 7 today, not 6 as the v2.1 scorecard
  shipped — the v2.1 estimate predated the file-listing recheck.** This change
  treats the score-7 milestone as a closed historical item and focuses on the
  delta to 8.

- **path-to-8** (this change): an E2E test pinning CLAUDE.md "signal-cli protocol
  behavior we depend on" claim **#3** — *"Parallel request processing → match by
  id, not by order"*. The cited fact is grounded in signal-cli's
  `JsonRpcReader.java:58` which uses `Executors.newVirtualThreadPerTaskExecutor()`
  so responses arrive in execution-time order, not request order. Our wrapper's
  `JsonRpcClient._pendingRequests : ConcurrentDictionary<string,
  TaskCompletionSource>` keyed by request `id` is supposed to handle this
  correctly — and unit tests cover it via mocked dispatchers — but no integration
  test exercises the real signal-cli virtual-thread dispatcher under concurrent
  load. A protocol-fidelity regression here (e.g. a refactor that switches to a
  `Queue<TaskCompletionSource>` "because order is preserved") would compile,
  pass all unit tests, and only manifest under concurrent calls against a real
  signal-cli process — exactly the gap E2E coverage is supposed to close.

## What Changes

Single new E2E integration test under
`Tests/SignalCli.Tests.Integration/SignalCliE2EParallelRpcCorrelationTests.cs`.
Same skip-gate pattern as the three existing E2E files (bundled-JRE + signal-cli
jar check; gracefully skip if runtime not present).

- **`e2e-coverage-expansion`** capability:
  - Adds `Process_ParallelVersionCalls_AllResolveToCorrectResponseById` E2E test:
    fire **10** concurrent `VersionAsync()` calls against one bundled signal-cli
    process; all 10 must complete successfully, each receiving the SAME version
    string (one signal-cli responds with one version regardless of concurrency),
    AND each `Task` must complete (no cross-talk → no `TimeoutException`).
  - The strong assertion: `Task.WhenAll(versionTasks)` completes within
    `RequestTimeoutSeconds * 2` (generous bound — 60s default). If `_pendingRequests`
    correlation is broken (e.g. id-based dict replaced by a queue), at least
    one of the 10 tasks would fault with `TimeoutException` because its response
    landed in the wrong TCS.
  - Skip-gate identical to existing E2E suite (`IsRuntimeAvailable` static
    helper, bundled-JRE or native-Linux signal-cli path-check).
  - **No production code changes** — this is a pure observation/regression test
    against existing behavior.

## Capabilities

### New Capabilities

- **`e2e-coverage-expansion`**: the Integration test suite SHALL cover parallel
  JSON-RPC request correlation against a real signal-cli instance. A test SHALL
  fire ≥10 concurrent `VersionAsync()` calls and assert all complete with the
  same version response within `RequestTimeoutSeconds * 2` (proving id-based
  `TaskCompletionSource` correlation works against the real virtual-thread
  dispatcher in `JsonRpcReader.java`, not just against the mocked `Subject<T>`
  in unit tests).

### Modified Capabilities

- **`integration-tests-expansion`** (originally from `audit-followup-2026`): the
  Integration coverage scope grows from {start/stop/restart, external-kill,
  health-monitor cadence, mid-flight `DisposeAsync`, `IConfiguration` overload,
  graceful shutdown} to **also include** parallel RPC correlation. This is
  additive — no existing E2E test changes shape or coverage.

## Out of scope

- **A signal-cli-side load test.** We do NOT measure signal-cli's own
  throughput / latency under concurrency — that's signal-cli's concern, not
  ours. The test is purely a correctness regression for OUR `JsonRpcClient`
  id-correlation layer.
- **Concurrent calls that hit unique-side-effect RPC methods** (e.g. concurrent
  `sendMessage`). signal-cli's behavior under concurrent message-sends is its
  own contract and would require a Signal account — strictly out of scope for
  the no-account-required test gate. `version` is the canonical idempotent
  read-only RPC for this kind of correlation test.
- **Stress testing with 1000+ concurrent calls.** 10 is enough to exercise the
  virtual-thread dispatcher path (signal-cli spawns one virtual thread per
  request — 10 is well above the "1 vs N" threshold where ordering can drift).
  Going higher would inflate test runtime without adding regression-detection
  value.
- **Path to score 9 or 10.** Would require either (a) live Signal account E2E
  tests with a sandbox account/CI secrets, or (b) a synthetic signal-cli
  mock-server that simulates protocol violations. Both are substantially larger
  changes; out of scope here.
