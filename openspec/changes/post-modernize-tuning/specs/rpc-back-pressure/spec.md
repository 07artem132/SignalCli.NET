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
