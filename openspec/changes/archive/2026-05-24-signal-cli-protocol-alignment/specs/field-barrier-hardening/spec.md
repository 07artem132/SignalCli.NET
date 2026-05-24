## ADDED Requirements

### Requirement: `JsonRpcClientHostedService._client` SHALL be `volatile`

The `IJsonRpcClient? _client` field on `JsonRpcClientHostedService` SHALL be declared `volatile`. The field is written by `StartAsync` / `StopAsync` / `DisposeAsync` and read concurrently by `SignalCliHealthMonitor.PingCliAsync`, `SignalEventService.StartAsync`, and `SignalEventService.SubscribeAsync`. On weak memory models (ARM64 — supported by .NET 10), without `volatile` semantics a reader on a different core may observe a stale null after the writer published a non-null reference. The `volatile` modifier provides acquire/release semantics at every read/write and has negligible runtime cost on x64 (no fence emitted).

#### Scenario: Concurrent Client reads do not observe stale null
- **GIVEN** a `JsonRpcClientHostedService` with mocked dependencies
- **WHEN** `Task.WhenAll(StartAsync, sleep 5ms + StopAsync, 100× read Client property)` races on a single host
- **THEN** no `NullReferenceException` is thrown from any thread
- **AND** every successful `Client` read returns a non-null reference

### Requirement: `SignalCliHostedService.Dispose()` sync path SHALL synchronize with `CleanupProcess`

Sync `Dispose()` on `SignalCliHostedService` SHALL acquire `_operationLock.Wait(TimeSpan.FromMilliseconds(50))` before invoking `DisposeCore()`. If the wait times out (a long-running cleanup is in flight), `DisposeCore` runs anyway — preserving the pre-existing best-effort behavior. The acquisition synchronizes the read of `_currentProcess` inside `DisposeCore` with the writes performed by `CleanupProcess` under the lock-finally of `StopProcessInternalAsyncNoLock`, eliminating the race window where sync `Dispose` could observe a torn or already-disposed process reference.

#### Scenario: Sync Dispose during cleanup does not throw
- **GIVEN** a `SignalCliHostedService` whose `_currentProcess.WaitForExitAsync` mock delays 100ms
- **AND** thread A is mid-`StopProcessInternalAsyncNoLock` holding `_operationLock`
- **WHEN** thread B calls `service.Dispose()`
- **THEN** no exception leaks from either thread
- **AND** the captured logger recorded the `Disposing` log entry exactly once

### Requirement: Test coverage SHALL include explicit race-prober tests

Both race fixes above SHALL be accompanied by tests that fail under a Thread.Sleep-injected delay in the absence of the fix. The tests are lightweight (no `FakeTimeProvider`; just `Task.WhenAll`) and live under:

- `Tests/SignalCli.Tests/JsonRpcClientHostedServiceTests.cs` — `Client_ConcurrentStartStop_NoNullRefException`.
- `Tests/SignalCli.Tests/SignalCliHostedService/SignalCliHostedServiceDisposalTests.cs` — `SyncDispose_DuringCleanup_AcquiresLock`.

#### Scenario: Race-prober tests verify the fix end-to-end
- **WHEN** the race-prober tests run with the fixes applied
- **THEN** both pass within 500ms wall-clock each
- **AND** removing the fix locally (revert the `volatile` or the `Wait(50)`) causes the corresponding test to fail intermittently within 10 reruns
