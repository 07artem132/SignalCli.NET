## ADDED Requirements

### Requirement: JSON-RPC standard error codes SHALL deserialize correctly

`JsonRpcError` SHALL deserialize the canonical JSON-RPC 2.0 error codes `-32601` (Method not found) and `-32700` (Parse error). The `Data` field on `JsonRpcError`, when present in the wire payload, SHALL be preserved as a `JsonElement` accessible on the deserialized instance.

#### Scenario: -32601 deserializes
- **WHEN** `JsonSerializer.Deserialize<JsonRpcResponse>("{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32601,\"message\":\"Method not found\"},\"id\":\"1\"}", SignalJson.Options)` runs
- **THEN** `response.Error!.Code == -32601`
- **AND** `response.Error.Message == "Method not found"`

#### Scenario: -32700 deserializes
- **WHEN** `JsonSerializer.Deserialize<JsonRpcResponse>("{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32700,\"message\":\"Parse error\"},\"id\":null}", SignalJson.Options)` runs
- **THEN** `response.Error!.Code == -32700`

#### Scenario: `error.data` payload survives
- **GIVEN** the wire payload `{"jsonrpc":"2.0","error":{"code":-32603,"message":"E","data":{"foo":42}},"id":"1"}`
- **WHEN** the response is deserialized
- **THEN** `response.Error!.Data` is non-null and the JSON property `foo` is accessible with value `42`

### Requirement: Attachment inline/temp-file boundary SHALL be sharp

`SignalMessage.SendUnifiedMessageAsync` SHALL use the inline `data:` URI path when the total encoded attachment size is strictly less than `MaxInlineEncodedAttachmentBytes` (15 000 000), and the temp-file path otherwise. The boundary is exclusive on the inline side.

#### Scenario: One byte over the inline threshold goes to temp file
- **GIVEN** an attachment whose raw size is `MaxInlineEncodedAttachmentBytes * 3 / 4 + 1` (one byte over)
- **WHEN** `SendAttachmentAsync` is called
- **THEN** the captured `parameters.Attachments[0]` is a filesystem path, not a `data:` URI

### Requirement: Attachment filename sanitization SHALL handle NUL bytes and Unicode RTL overrides

`AttachmentEntry.SafeFileName` SHALL strip NUL bytes and U+202E (RIGHT-TO-LEFT OVERRIDE) from filenames. `ToDataUri` and `SaveToTempFile` SHALL use the sanitized name.

#### Scenario: NUL-byte filename is sanitized
- **GIVEN** an `AttachmentEntry` with filename `"x\0evil.bin"`
- **WHEN** `ToDataUri()` is called
- **THEN** the produced URI's `filename=` parameter contains no NUL byte
- **AND** `SaveToTempFile()` writes to a path that exists on disk

#### Scenario: Right-to-left override is sanitized
- **GIVEN** an `AttachmentEntry` with filename `"safe‮evil.bin"`
- **WHEN** `ToDataUri()` is called
- **THEN** the produced URI's `filename=` parameter contains no U+202E codepoint

### Requirement: `AtomicCounter.Increment()` SHALL wrap at `int.MaxValue` without throwing

`AtomicCounter.Increment()` SHALL use `unchecked` integer arithmetic so that incrementing at `int.MaxValue` returns `int.MinValue` without `OverflowException`.

#### Scenario: Wrap-around does not throw
- **GIVEN** an `AtomicCounter` seeded with `(long)(int.MaxValue - 1)`
- **WHEN** `Increment()` is called twice
- **THEN** the first call returns `int.MaxValue`
- **AND** the second call returns `int.MinValue`
- **AND** no exception is thrown

### Requirement: Observability counters SHALL fire on real events

The `signalcli.events.dropped`, `signalcli.rpc.duration`, and `signalcli.process.restarts` Meter instruments SHALL produce non-empty measurements when their respective triggering events occur. The library SHALL NOT silently swallow these signals.

#### Scenario: Channel overflow increments EventsDropped by exactly the overflow count
- **GIVEN** a `SignalEventService` with text channel capacity `N` and no consumer
- **WHEN** the test pushes `N + 5` text-message envelopes through the dispatcher
- **THEN** the cumulative `signalcli.events.dropped` measurement filtered by `event_type=text` equals 5

#### Scenario: Happy-path RPC records a positive duration measurement
- **GIVEN** an `InvokeMethodAsync` call that completes normally
- **WHEN** the test reads the captured `signalcli.rpc.duration` measurements
- **THEN** at least one measurement has `value > 0` and tag `method=<the method name>`

#### Scenario: ForceRestartAsync ticks ProcessRestarts with trigger=force
- **GIVEN** a `SignalCliHostedService` in `Running` state
- **WHEN** `ForceRestartAsync` is called
- **THEN** the captured `signalcli.process.restarts` measurements contain at least one entry with `trigger=force`

### Requirement: SubscribeAsync follower cancellation propagation SHALL be documented and tested

When a leader `SubscribeAsync(account, ct)` call is cancelled mid-RPC, the followers awaiting the same reservation TCS SHALL observe a documented outcome — either they receive the same `OperationCanceledException` (current behavior pins) OR a new leader is elected. The test SHALL pin whichever behavior is shipped.

#### Scenario: Leader cancellation surfaces in followers
- **GIVEN** a leader call to `SubscribeAsync(account, leaderCts.Token)` is in flight
- **AND** two follower calls to `SubscribeAsync(account)` are awaiting the leader's TCS
- **WHEN** `leaderCts.Cancel()` is called
- **THEN** the leader task throws `OperationCanceledException`
- **AND** both follower tasks observe a documented outcome consistent with the implementation (pinned by the test's assertion)

### Requirement: `ForceRestartAsync` SHALL be a no-op in non-restartable states

`SignalCliHostedService.ForceRestartAsync` SHALL log `ForceRestartSkipped` and return without action when the current `ProcessStateManager.CurrentState` is `Stopping`, `Stopped`, or `NotStarted`. No `ForceRestartAttempt` log SHALL be emitted in those states.

#### Scenario: ForceRestart from Stopping is skipped
- **GIVEN** the state is `Stopping`
- **WHEN** `ForceRestartAsync` is called
- **THEN** a `ForceRestartSkipped` log is recorded with `state=Stopping`
- **AND** no `ForceRestartAttempt` log is recorded

### Requirement: Notification channel SHALL deliver in FIFO order even at minimum capacity

When `SignalCliOptions.NotificationChannelCapacity = 1`, the JSON-RPC notification channel SHALL still deliver every produced message to subscribers, in FIFO order.

#### Scenario: Capacity 1, burst of 20, all delivered in order
- **GIVEN** `JsonRpcClient` is configured with `NotificationChannelCapacity = 1` and a slow subscriber
- **WHEN** 20 notifications are pushed back-to-back through `ProcessMessageAsync`
- **THEN** the subscriber receives exactly 20 notifications
- **AND** their subscription-IDs are `[0..19]` in order

### Requirement: `AddSignalCli` SHALL be idempotent

Calling `services.AddSignalCli(...)` a second time on the same `IServiceCollection` SHALL NOT add duplicate descriptors and SHALL NOT override the first call's options.

#### Scenario: Second AddSignalCli is a no-op
- **GIVEN** `services.AddSignalCli(o => o.AppHome = "/a")` has been called
- **WHEN** `services.AddSignalCli(o => o.AppHome = "/b")` is called on the same collection
- **THEN** the resolved `IOptions<SignalCliOptions>.Value.AppHome` equals `/a`
- **AND** the count of registered hosted services is unchanged from the first call

### Requirement: `EnvironmentVariables` SHALL be a read-only snapshot

`SignalCliOptions.EnvironmentVariables` SHALL be typed as `IReadOnlyDictionary<string,string>`. A consumer who casts the returned value to a mutable interface SHALL receive a wrapper that prevents post-start mutation from affecting the snapshot already captured by internal services.

#### Scenario: External mutation does not leak
- **GIVEN** `opts.EnvironmentVariables = new Dictionary<string,string> { ["k"]="v" }` was set in the configure delegate
- **AND** the host has built and resolved `IOptions<SignalCliOptions>.Value`
- **WHEN** the consumer attempts to mutate the underlying dictionary post-resolve
- **THEN** the value snapshotted by internal services remains `{ "k" → "v" }`

### Requirement: `JsonRpcResponse` with both `result` and `error` SHALL prefer error

When the wire response defensively contains both a non-null `result` and a non-null `error` (a protocol violation per JSON-RPC 2.0), `JsonRpcClient` SHALL throw `JsonRpcException` with the `error` payload — `result` SHALL NOT be returned as if successful.

#### Scenario: Both fields present, error wins
- **GIVEN** an `InvokeMethodAsync` call in flight
- **WHEN** `ProcessMessageAsync` receives `{"id":"1","result":{...},"error":{"code":-1,"message":"E"}}`
- **THEN** the in-flight task faults with `JsonRpcException` carrying `Code == -1` and `Message == "E"`
