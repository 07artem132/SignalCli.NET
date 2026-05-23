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

### Requirement: Public surface inputs are validated at the boundary
`SignalDevices.FinishLinkAsync(deviceLinkUri, deviceName, ct)` and `SignalGroups.ListGroupsAsync(account, ct)` SHALL validate their string inputs via `ArgumentException.ThrowIfNullOrEmpty(...)` (.NET 8+) before the RPC fires. Passing `null` or `""` SHALL produce an `ArgumentException` with the correct `paramName`, not a 400-class error from signal-cli.

#### Scenario: Empty account is rejected before any RPC
- **WHEN** `signalGroups.ListGroupsAsync("")` is called
- **THEN** the method throws `ArgumentException` with `paramName = "account"`
- **AND** no `listGroups` RPC is sent

### Requirement: Facade services do not implement IDisposable for empty bodies
`SignalAccounts`, `SignalDevices`, `SignalGroups`, and `SignalMessage` SHALL NOT implement `IDisposable` while their `Dispose` body is empty/no-op. These classes do not own disposable resources — declaring `IDisposable` only causes the DI container to invoke a useless `Dispose` and misleads readers about what the class owns.

#### Scenario: DI shuts down
- **WHEN** the DI container disposes the root scope
- **THEN** none of the facade services is invoked through `IDisposable.Dispose`
- **AND** no behavioral regression occurs (current Dispose body is empty)

### Requirement: SignalDevices logs entry-level operations for consistency
`SignalDevices.StartLinkAsync` and `SignalDevices.FinishLinkAsync` SHALL log a `Debug`-level entry message (e.g. "Запуск процесу зв'язування пристрою") symmetric to `SignalAccounts.ListAccounts` / `SignalGroups.ListGroups`. Behavioral logging coverage SHALL be consistent across all facade services.

#### Scenario: Trace correlates calls across facades
- **WHEN** a contributor traces an operation through the log
- **THEN** every facade call has a `Debug` entry record naming the method
- **AND** `SignalDevices` produces those records on the same conditions as the other facades

### Requirement: Internal classes that are not extension points are sealed
`ProcessWrapper`, `ProcessFactory`, `JsonRpcClientFactory`, `SignalAccounts`, `SignalDevices`, `SignalGroups`, and the now-internal `SignalEventService` SHALL be declared `sealed`. Inheritance is not a supported extension scenario for any of them.

#### Scenario: Build catches accidental inheritance
- **WHEN** a contributor attempts to subclass any of these types
- **THEN** the compiler reports CS0509

### Requirement: StreamPair is sealed and Dispose is idempotent
`StreamPair` (public type) SHALL be `sealed` and its `Dispose()` SHALL be guarded against repeated invocation. Repeated `Dispose` SHALL be a no-op; this is defense in depth on top of `StreamWriter`/`StreamReader`'s own idempotency, against accidental ownership confusion between `StreamPair` and the `Process` that owns the underlying streams.

#### Scenario: Dispose called twice
- **GIVEN** a `StreamPair` whose `Dispose()` has already run
- **WHEN** `Dispose()` is called again
- **THEN** the second call is a no-op (no exception, no `Stream.Dispose` invocation)

### Requirement: README dependency table is accurate
The dependency table in `README.md` SHALL list every `PackageReference` from `src/SignalCli/SignalCli.csproj`. Adding/removing a public dependency SHALL be paired with a README update. `JetBrains.Annotations` (currently missing) SHALL be added on this pass.

#### Scenario: New dependency added to csproj
- **WHEN** a contributor adds a `PackageReference` to `src/SignalCli/SignalCli.csproj`
- **THEN** the same PR updates the README dependency table
- **AND** code review confirms the table is in sync

### Requirement: CLAUDE.md rule 6 is updated to reflect source-gen-only serialization
After `aot-readiness` removes the reflection fallback from `SignalJson.Options`, the corresponding sentence in `CLAUDE.md` rule 6 SHALL be updated to remove the phrase "(which combines the source-gen resolver with a reflection fallback)". The rule SHALL instead state that every serializable type MUST be in `SignalJsonContext` and that there is no runtime fallback.

#### Scenario: New contributor reads CLAUDE.md after the change
- **WHEN** they look up serialization conventions
- **THEN** the doc accurately states "source-generated context only — no reflection fallback at runtime"
- **AND** they understand they must register new types in the context

### Requirement: CLAUDE.md rule 7 is updated for csproj-anchored SHA pinning
After `supply-chain-hardening` moves SHA pinning into csproj `<…Sha256>` properties (passed to the download scripts as arguments), the corresponding sentence in `CLAUDE.md` rule 7 SHALL be updated. The rule SHALL state that the canonical version/hash live in the csproj and that the scripts validate against the passed parameter (so a single-place edit suffices).

#### Scenario: Contributor bumps signal-cli version
- **WHEN** they edit `<SignalCliVersion>` and `<SignalCliSha256>` in the csproj
- **THEN** CLAUDE.md tells them this is the only edit required (scripts read the values as arguments)
- **AND** the previous "update both .ps1 and .sh" guidance is removed

### Requirement: tasks.md counter reflects current test count
The task that reads "152 tests pass" SHALL be updated to the actual current count (173 at the time of this audit) so future contributors do not chase a phantom regression.

#### Scenario: Contributor runs the test suite
- **WHEN** they consult `tasks.md` for the expected count
- **THEN** the number matches what `dotnet test` actually reports today (modulo any new tests added by the change itself)
