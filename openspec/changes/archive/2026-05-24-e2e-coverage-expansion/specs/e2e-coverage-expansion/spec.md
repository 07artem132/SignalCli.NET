## ADDED Requirements

### Requirement: Integration test suite SHALL cover parallel JSON-RPC request correlation

The Integration test suite SHALL include an end-to-end test that fires at least 10 concurrent JSON-RPC requests against a real bundled-JRE (or native-Linux) signal-cli process and asserts each request resolves to its OWN response — proving the `JsonRpcClient._pendingRequests : ConcurrentDictionary<string, TaskCompletionSource>` id-based correlation works against signal-cli's virtual-thread dispatcher (`JsonRpcReader.java:58`, `Executors.newVirtualThreadPerTaskExecutor()`), not just against the unit-test-level `Subject<T>` mock.

The test SHALL skip gracefully (per existing E2E skip-gate pattern) when no bundled-runtime is available — it MUST NOT fail CI on machines without the optional `SignalCli.Runtime.Jre.{win-x64,osx-arm64}` or `SignalCli.Runtime.Native` packages.

#### Scenario: 10 concurrent VersionAsync calls all resolve to the same version string
- **GIVEN** a `Host` started with `AddSignalCliWithBundledRuntimeDefaults` and the bundled signal-cli process reached `WaitForReadyAsync`
- **WHEN** 10 `signalCli.VersionAsync(cts.Token)` calls are issued in parallel via `Task.WhenAll`
- **THEN** all 10 tasks complete successfully within `RequestTimeoutSeconds * 2` (60s with default settings)
- **AND** all 10 `VersionResponse.Version` strings are identical (single distinct value)
- **AND** no response is null or empty (no cross-talk landed an empty payload into the wrong `TaskCompletionSource`)

#### Scenario: missing bundled runtime triggers graceful skip
- **GIVEN** a test machine without `SignalCli.Runtime.Jre.*` and without native signal-cli
- **WHEN** the parallel-RPC E2E test runs
- **THEN** the test logs `[SKIP] <reason>` and returns without failing the test run (same pattern as `SignalCliE2EVersionTests.IsRuntimeAvailable`)

#### Scenario: correlation regression is detected by the test
- **GIVEN** a hypothetical refactor that replaces `_pendingRequests : ConcurrentDictionary<string, TaskCompletionSource>` with a `Queue<TaskCompletionSource>` (relying on response-order matching request-order)
- **WHEN** the parallel-RPC E2E test runs against the refactored code
- **THEN** at least one of the 10 `VersionAsync` tasks faults with `TimeoutException` or returns mismatched data (because signal-cli's virtual-thread dispatcher delivers responses in execution-time order, not request order — the queue head and the response would no longer line up)
- **AND** the test fails loudly, surfacing the broken protocol-fidelity claim from CLAUDE.md "signal-cli protocol behavior we depend on" §3
