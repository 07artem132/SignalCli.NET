## ADDED Requirements

### Requirement: Per-call JSON-RPC request timeout override

`ISignalCliClient.InvokeMethodAsync` and the transport-level `IJsonRpcSender.InvokeMethodAsync` SHALL accept an optional trailing parameter `TimeSpan? timeout = null` that overrides the client-wide default timeout (`SignalCliOptions.RequestTimeoutSeconds`) for that single call. The parameter is API-additive: it is the last parameter and defaults to `null`, so existing call sites compile unchanged and behave identically.

`JsonRpcClient.InvokeMethodAsync` SHALL select the effective timeout as follows:

- `timeout` is a positive `TimeSpan` (`> TimeSpan.Zero`) → use it for the request's timeout `CancellationTokenSource`.
- `timeout` is `null` or `TimeSpan.Zero` → use the client-wide `_requestTimeout` (behavior unchanged).
- `timeout` is negative (`< TimeSpan.Zero`) → throw `ArgumentOutOfRangeException` at the boundary, before any `CancellationTokenSource` is scheduled.

The timeout `CancellationTokenSource` SHALL be constructed via the `TimeProvider`-aware overload `new CancellationTokenSource(effectiveTimeout, _timeProvider)` (never the parameterless-of-`TimeProvider` overload) so timeout paths remain virtualizable via `FakeTimeProvider`. A timeout MUST fault the awaiting task with `TimeoutException` and MUST remain distinguishable from a caller-initiated `OperationCanceledException`. The `TimeoutException` message SHALL report the effective timeout actually applied, not always the client-wide default.

#### Scenario: Per-call timeout shorter than the client default fires first

- **GIVEN** a `JsonRpcClient` whose client-wide default is 60 s, driven by a `FakeTimeProvider`
- **AND** an `InvokeMethodAsync` call passing `timeout = TimeSpan.FromSeconds(5)` against a silent process
- **WHEN** the virtual clock advances past 5 s (but the response never arrives)
- **THEN** the call faults with `TimeoutException`
- **AND** the message reflects the 5 s per-call value, not 60 s

#### Scenario: Per-call timeout longer than the client default does not fire at the default

- **GIVEN** a `JsonRpcClient` whose client-wide default is 30 s, driven by a `FakeTimeProvider`
- **AND** an `InvokeMethodAsync` call passing `timeout = TimeSpan.FromSeconds(130)` against a silent process
- **WHEN** the virtual clock advances past 30 s (the default) but before 130 s
- **THEN** the call has NOT completed (no `TimeoutException` yet)
- **AND** advancing past 130 s then faults it with `TimeoutException`

#### Scenario: Null timeout preserves client-default behavior

- **GIVEN** a `JsonRpcClient` whose client-wide default is 10 s, driven by a `FakeTimeProvider`
- **AND** an `InvokeMethodAsync` call passing `timeout = null` (or omitting the argument) against a silent process
- **WHEN** the virtual clock advances past 10 s
- **THEN** the call faults with `TimeoutException` at the client default

#### Scenario: Negative timeout is rejected at the boundary

- **GIVEN** a `JsonRpcClient`
- **WHEN** `InvokeMethodAsync` is called with `timeout = TimeSpan.FromSeconds(-1)`
- **THEN** it throws `ArgumentOutOfRangeException` (paramName `timeout`) before scheduling any timeout

### Requirement: `FinishLinkAsync` exposes a per-call timeout for the interactive QR-scan phase

`ISignalDevices.FinishLinkAsync` and `SignalDevices.FinishLinkAsync` SHALL accept an optional trailing parameter `TimeSpan? timeout = null` and forward it to `ISignalCliClient.InvokeMethodAsync`. The XML documentation SHALL explain the rationale: the `finishLink` RPC has a long interactive phase (the primary device must manually scan a QR code and confirm linking), which legitimately exceeds the client-wide default. `StartLinkAsync` SHALL NOT receive a per-call timeout parameter — it has no long interactive phase.

#### Scenario: FinishLinkAsync forwards the caller's timeout to the RPC client

- **GIVEN** a mocked `ISignalCliClient`
- **WHEN** `SignalDevices.FinishLinkAsync(uri, name, ct, timeout: TimeSpan.FromSeconds(150))` is called
- **THEN** the mock observes `InvokeMethodAsync` invoked with a non-null `timeout` equal to `TimeSpan.FromSeconds(150)`

#### Scenario: FinishLinkAsync without a timeout forwards null (client default)

- **GIVEN** a mocked `ISignalCliClient`
- **WHEN** `SignalDevices.FinishLinkAsync(uri, name)` is called without a timeout argument
- **THEN** the mock observes `InvokeMethodAsync` invoked with `timeout == null`
