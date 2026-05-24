## ADDED Requirements

### Requirement: Bounded JSON-RPC request lifetime
Every outgoing JSON-RPC request SHALL complete or fault within a bounded, configurable timeout (`Config.RequestTimeoutSeconds`, default 30 s). A timeout MUST fault the awaiting task with `TimeoutException` and MUST be distinguishable from a caller-initiated `OperationCanceledException`.

#### Scenario: signal-cli is alive but silent
- **GIVEN** the `signal-cli` process is running but never responds to a request
- **WHEN** the caller awaits the corresponding RPC method
- **THEN** the call faults with `TimeoutException` within `RequestTimeoutSeconds + epsilon`
- **AND** the pending-request entry for that id is removed

#### Scenario: Caller cancels before the timeout fires
- **GIVEN** the caller passes a `CancellationToken` that is cancelled before the timeout elapses
- **WHEN** the cancellation is requested
- **THEN** the call faults with `OperationCanceledException` carrying the caller's token, not `TimeoutException`

### Requirement: Cancellable, ownership-correct reader lifecycle
The stdin/stdout/stderr reader loops SHALL be cancellable, SHALL be stopped (cancelled and awaited to completion) before the underlying `StreamPair` is replaced or disposed, and SHALL NOT take ownership of (or dispose) streams owned by the spawned `Process`.

#### Scenario: Stream pair is replaced after a restart
- **GIVEN** the `JsonRpcClient` is reading from a `StreamPair` `A`
- **WHEN** `StreamPairChanged` emits a new pair `B`
- **THEN** the reader loops for `A` are cancelled and awaited to completion before the loops for `B` are started
- **AND** the streams of `A` are not disposed by the reader loops

#### Scenario: Client is disposed
- **WHEN** `JsonRpcClient` is disposed (via `DisposeAsync` or `Dispose`)
- **THEN** all reader loops observe cancellation and complete
- **AND** no `UnobservedTaskException` is raised
