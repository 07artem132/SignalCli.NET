## ADDED Requirements

### Requirement: Bounded buffering between stdout reader and notification subscribers
The JSON-RPC transport SHALL place a bounded `System.Threading.Channels.Channel<T>` between the stdout reader loop and the notification fan-out, so a slow or stalled subscriber cannot block the reader from draining the `signal-cli` stdout pipe.

#### Scenario: A slow subscriber does not stall stdout drain
- **GIVEN** an `IJsonRpcClient` with one subscriber that takes 50 ms per notification
- **AND** `signal-cli` emits 1 000 notifications back-to-back
- **WHEN** the subscriber processes them
- **THEN** the reader continues to call `ReadLineAsync` on stdout throughout
- **AND** no notification is dropped
- **AND** notifications are delivered to the subscriber in the order they were read

#### Scenario: Capacity is exhausted
- **GIVEN** `Config.NotificationChannelCapacity = N`
- **WHEN** `N` notifications are buffered and the consumer has not drained any
- **THEN** the reader's next `WriteAsync` awaits asynchronously (does not throw)
- **AND** the reader does not advance until the consumer dequeues at least one item

### Requirement: Capacity is configurable
The capacity of the notification channel SHALL be configurable via `Config.NotificationChannelCapacity` (default 1 024). The value MUST be ≥ 1.

#### Scenario: Invalid capacity is rejected at startup
- **WHEN** `Config.NotificationChannelCapacity ≤ 0` is configured
- **THEN** the hosted-service startup fails fast with an actionable exception message

### Requirement: Orderly shutdown drains the channel
On `DisposeAsync`, the JSON-RPC client SHALL complete the channel writer, await the consumer task to finish processing any in-flight items, and only then complete and dispose the notification `Subject`.

#### Scenario: Dispose during active traffic
- **GIVEN** notifications are in the channel
- **WHEN** `DisposeAsync` is called
- **THEN** every notification already accepted into the channel is delivered to current subscribers
- **AND** no notification is delivered after the `Subject` completes
- **AND** no `UnobservedTaskException` is raised

### Requirement: JsonRpcClient is async-disposable only
`JsonRpcClient` SHALL implement `IAsyncDisposable` and SHALL NOT additionally implement `IDisposable`. The sync-over-async bridge (`DisposeAsync().AsTask().GetAwaiter().GetResult()`) SHALL NOT exist on this type (Microsoft *Common async/await bugs* — never use `.Result`/`.Wait()`/`.GetAwaiter().GetResult()`).

#### Scenario: DI container disposes the client
- **GIVEN** the DI container holds the `JsonRpcClient` as a singleton via `JsonRpcClientHostedService`
- **WHEN** the host shuts down
- **THEN** `IAsyncDisposable.DisposeAsync` is awaited (the consumer already does an `is IAsyncDisposable` check)
- **AND** no `IDisposable.Dispose` is invoked

#### Scenario: A consumer calls Dispose anyway
- **WHEN** legacy consumer code calls `client.Dispose()` (synchronous)
- **THEN** the type does not compile because `IDisposable` is not implemented
- **AND** the consumer is steered to `await client.DisposeAsync()` at compile time

### Requirement: Outbound requests serialize without intermediate JsonElement
JSON-RPC requests SHALL be composed directly to the output writer (`Utf8JsonWriter` against `pair.StandardInput`), without first serializing the user's `TRequest` to a `JsonElement` and then re-serializing the whole `JsonRpcRequest` to a string. The result MUST be identical to the previous two-pass implementation byte-for-byte (modulo non-semantic whitespace, which is already disabled).

#### Scenario: Request payload format is unchanged
- **GIVEN** the same `TRequest` and `method`/`id` arguments
- **WHEN** a request is sent under the new single-pass writer
- **THEN** the written JSON has the same key order (`"jsonrpc"`, `"method"`, `"params"`, `"id"`) and the same `"params"` shape as before
- **AND** signal-cli accepts the request identically

#### Scenario: Allocation profile improves
- **GIVEN** a benchmark that sends 10 000 small requests
- **WHEN** the new path runs vs the prior `SerializeToElement` + `Serialize` path
- **THEN** the new path allocates strictly fewer `JsonDocument`/`string` instances per request
- **AND** the test does not regress in throughput
