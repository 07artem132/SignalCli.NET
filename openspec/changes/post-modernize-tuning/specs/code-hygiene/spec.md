## ADDED Requirements

### Requirement: Sealed internal types where extension is not a use case
`SignalEventService` SHALL be `sealed`. No subclass of this type is supported, and `sealed` makes that the compiler's job to enforce.

#### Scenario: Inheritance is rejected at compile time
- **WHEN** a consumer attempts to subclass `SignalEventService`
- **THEN** the C# compiler reports CS0509

### Requirement: No unused private fields
Private fields that are written but never read SHALL be removed. The current `SignalEventService._rpcClient` field (assigned in `StartAsync`, read nowhere — `_rpcClientProvider.Client` is used directly) SHALL be deleted along with its assignment.

#### Scenario: Analyzer catches the regression
- **WHEN** a new private field is introduced that is never read
- **THEN** `IDE0052` ("Remove unread private member") fires
- **AND** the build fails (`TreatWarningsAsErrors=true`)

### Requirement: AtomicCounter wrap-around behavior is documented or eliminated
`AtomicCounter` SHALL either (a) document the wrap-around invariant inline so future readers understand the subtle `% int.MaxValue` semantics, or (b) eliminate the int-cast altogether by exposing the counter as `long`/`string` so the wrap is not reachable in practice.

#### Scenario: Reading the code months later
- **WHEN** a contributor reads `AtomicCounter.Increment`
- **THEN** either the wrap-around behavior is explained in a `// WHY:` comment, or the int-narrowing branch is gone

### Requirement: Single-pass enumeration in argument validation
`SignalMessage.ValidateRecipients` SHALL materialize the `IEnumerable<IRecipient>` exactly once. The current double-enumeration (`recipients == null || !recipients.Any()`, then a separate `Where(...).Select(...).ToList()` later) SHALL be replaced by a single materialization at the entry, with all subsequent checks consuming the materialized list.

#### Scenario: Caller passes a stateful enumerable
- **GIVEN** an `IEnumerable<IRecipient>` whose enumeration has side effects (e.g. logs each yield)
- **WHEN** `SendTextMessageAsync` is called
- **THEN** the enumerator is iterated exactly once
- **AND** no side effect from the caller's iterator fires twice

### Requirement: Catch-and-immediate-rethrow is removed or enriched
A `try { … } catch (Exception ex) { _logger.LogError(ex, "…"); throw; }` block SHALL be either:
- removed (the outer caller already logs), or
- enriched with context that the inner exception lacks (the method name, an account identifier, the RPC id).

The bare log-and-rethrow pattern (no context added) SHALL NOT remain across `SignalService`, `SignalMessage`, `SignalAccounts`, `SignalDevices`, `SignalGroups`, `JsonRpcClientHostedService`.

#### Scenario: Caller catches and gets a meaningful trace
- **GIVEN** an exception originates inside `SignalMessage.SendTextMessageAsync`
- **WHEN** it propagates out
- **THEN** there is exactly one log entry per failure (not two: one inside the method and one in a wrapping handler)

### Requirement: Hot-path I/O is cached, not repeated
`Config.BuildClasspath` (which scans `LibDirectory` for `*.jar` on every process start) SHALL cache the classpath string after the first invocation per `Config` instance. The directory scan SHALL NOT repeat on each `Restart`/`ForceRestart` of the underlying signal-cli process.

#### Scenario: Repeated restarts
- **GIVEN** signal-cli is restarted N times during a session
- **WHEN** the configuration is unchanged
- **THEN** `Directory.GetFiles(...)` is invoked exactly once on the lib directory
- **AND** subsequent restarts use the cached classpath string

### Requirement: AtomicCounter request id allocation is minimal
The per-request id allocation SHALL be the single int-to-string conversion (already in place). Any additional allocations introduced by culture-related overloads SHALL be removed if they do not affect correctness. `int.ToString()` (no `CultureInfo` argument) is acceptable because the digits 0-9 are culture-invariant for non-negative integers.

#### Scenario: Locale change does not affect request ids
- **GIVEN** a tr-TR culture is active
- **WHEN** an integer id is converted to a string
- **THEN** the result is the same ASCII digits as in en-US (so omitting `InvariantCulture` is safe here)
