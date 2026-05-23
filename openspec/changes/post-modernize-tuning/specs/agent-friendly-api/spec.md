## ADDED Requirements

### Requirement: Public properties use PascalCase
Every public property — including those generated from positional record parameters — SHALL follow PascalCase (Microsoft *Capitalization Conventions*). Wire-level JSON field names SHALL be controlled with explicit `[JsonPropertyName("…")]` attributes so the on-the-wire format is unaffected.

#### Scenario: Response records expose PascalCase properties
- **WHEN** a consumer references `FinishLinkResponse.Number` or `SubscribeReceiveResponse.Id`
- **THEN** the property exists with PascalCase
- **AND** the corresponding JSON wire field stays `"number"` / `"id"` via `[JsonPropertyName]`

### Requirement: Async methods are suffixed `Async`
Every public method that returns `Task`, `Task<T>`, `ValueTask`, or `ValueTask<T>` SHALL be named with the `Async` suffix (Microsoft TAP guideline). Methods that today violate this — `ListAccounts`, `SyncAccount`, `StartLink`, `FinishLink`, `ListGroups`, `Version` — SHALL be renamed; the unsuffixed names SHALL NOT remain on the public surface.

#### Scenario: Renamed method on the public surface
- **WHEN** a consumer of the v3 library calls `signalAccounts.ListAccountsAsync()`
- **THEN** the call compiles and runs as before
- **AND** `signalAccounts.ListAccounts()` no longer exists on the public surface

### Requirement: CancellationToken is a method parameter, not an options member
`CancellationToken` SHALL NOT be a property on options/builder records (`TextMessageOptions`, `AttachmentMessageOptions`, `StickerMessageOptions`). It SHALL be the last parameter of the send method, defaulting to `default`.

#### Scenario: Sending a message with a cancellation token
- **GIVEN** a configured `TextMessageOptions options` and a `CancellationToken ct`
- **WHEN** the consumer calls `signalMessage.SendTextMessageAsync(options, ct)`
- **THEN** cancellation propagates as in any TAP-compliant API
- **AND** two `TextMessageOptions` instances differing only in (previously stored) `CancellationToken` are `Equals`

### Requirement: ConfigureAwait(false) on every library `await`
Every `await` in `src/SignalCli/**` library code SHALL use `.ConfigureAwait(false)`. The analyzer `CA2007` SHALL be enabled at `warning` severity in `.editorconfig` so regressions are caught at build time.

#### Scenario: Library code is library-correct
- **WHEN** the library is consumed by a UI/WinForms application
- **THEN** library awaits do not capture the UI synchronization context
- **AND** the consumer can `await` library APIs without `.ConfigureAwait(false)` themselves

### Requirement: Hosted-service surface is sealed
`SignalCliHostedService` SHALL be `sealed`. Derived classes from this hosted service are not a supported extension point.

#### Scenario: Inheritance is rejected at compile time
- **WHEN** a consumer attempts to subclass `SignalCliHostedService`
- **THEN** the C# compiler reports CS0509 ("cannot derive from sealed type")

### Requirement: Environment variables are read-only on the public surface
`Config.EnvironmentVariables` SHALL be `IReadOnlyDictionary<string, string>` on the read path. Mutation requires going through a documented helper (`Config.WithEnvironment(...)`) that returns a new snapshot.

#### Scenario: Mutating env after registration
- **GIVEN** `services.AddSignalCli(c => c.WithEnvironment(new Dictionary<string,string>{ ["X"]="1" }))`
- **WHEN** a consumer obtains the registered `Config` from DI and tries to cast `EnvironmentVariables` to `IDictionary`
- **THEN** the cast fails at runtime
- **AND** the consumer is steered toward `WithEnvironment` for any updates

### Requirement: Recipient discriminator is a sealed type hierarchy
`IRecipient` SHALL be a sealed hierarchy (`UserRecipient` and `GroupRecipient` as the only allowed implementations) usable in `is`/`switch` expression patterns. The boolean `IsGroup` discriminator SHALL be removed from the public surface; type-pattern matching replaces it. The C# compiler's exhaustiveness analyzer MUST be able to verify a `switch` over `IRecipient` covers all cases.

#### Scenario: Exhaustive pattern match
- **WHEN** a consumer writes `var label = r switch { UserRecipient u => "user", GroupRecipient g => "group" };`
- **THEN** the compiler does not warn about a non-exhaustive switch
- **AND** the boolean `r.IsGroup` is no longer available

#### Scenario: Adding a new recipient kind in a future version
- **WHEN** a third recipient kind is later introduced into the sealed hierarchy
- **THEN** every consumer-side `switch (IRecipient)` reports a non-exhaustive warning at the patch line
- **AND** consumers handle the new kind explicitly

### Requirement: Event payloads share a common envelope reference
Every `*EventArgs` record raised by `ISignalEventService` SHALL derive from a single `SignalEventArgs` base that holds a reference to the source `JsonMessageEnvelope` (and `Account` + `SubscriptionId`), so per-event records do not duplicate all 10 envelope fields. The new shape is a single reference instead of ten flattened strings (Microsoft *Performance — avoid duplicated allocations*).

#### Scenario: Accessing source metadata
- **WHEN** a subscriber reads `evt.Source`, `evt.SourceNumber`, `evt.SourceUuid`, `evt.SourceName`, `evt.SourceDevice`, `evt.Timestamp`, `evt.ServerReceivedTimestamp`, `evt.ServerDeliveredTimestamp`
- **THEN** every value resolves through the shared `evt.Envelope` reference
- **AND** the per-event record itself holds at most three fields (`Envelope`, `Account`, `SubscriptionId`) plus the payload-specific data

#### Scenario: Allocation profile
- **GIVEN** the same notification stream as before
- **WHEN** measured against the prior flattened-field records
- **THEN** the per-event allocation count drops to ≤ 4 references (envelope + account + subscriptionId + payload-specific)

### Requirement: Example program demonstrates correct async usage
`Example/SignalCli.Example/Program.cs` SHALL:
- declare `static async Task Main(string[] args)`,
- use `await using IHost host = …`,
- `await` every returned `Task` (no fire-and-forget),
- call `await host.StopAsync()` (not `host.StopAsync().Wait()`).

#### Scenario: Example program compiles and runs with no analyzer warnings
- **WHEN** the example is compiled with the same analyzer profile as the library
- **THEN** there are zero `CA2007`, `CA2012`, `VSTHRD110`-class warnings
- **AND** no awaitable returns are discarded

### Requirement: Protocol record nullability matches the wire contract
Public records in `Models/Signal/Envelope.cs` SHALL declare a `string` property as **non-nullable** only when signal-cli is documented (or verified) to always emit a value. Where the wire field is sometimes absent, the property SHALL be `string?`. The current mismatch in `Hangup.Type`/`Hangup.Opaque`, `Offer.Type`/`Offer.Opaque`, `Answer.Opaque`, `IceUpdate.Opaque`, and `JsonRemoteDelete.RemoteDeleteId` (non-nullable, but consumed without checking) SHALL be resolved per field.

#### Scenario: signal-cli omits an "always-present" field
- **GIVEN** a non-nullable property declared as part of the contract
- **WHEN** signal-cli emits a payload missing that field
- **THEN** `[JsonRequired]` causes deserialization to fail with a clear error nameing the field (so the bug is visible, not silent)
- **AND** the broken event does NOT propagate `null` into a non-nullable consumer-visible property

#### Scenario: A field documented as optional is null
- **GIVEN** a property declared as `string?` (matching wire optionality)
- **WHEN** signal-cli omits the field
- **THEN** deserialization succeeds with `null`
- **AND** consumers receive the event with the property set to `null`

### Requirement: Argument validation distinguishes null from empty
Public constructors and methods that accept a required `string` SHALL distinguish "null" from "empty" in the thrown exception type. `ArgumentNullException` SHALL be thrown only for `null`; `ArgumentException` (or `ArgumentException.ThrowIfNullOrEmpty` on .NET 8+) SHALL be thrown for empty/whitespace.

#### Scenario: Empty phone number to UserRecipient
- **WHEN** `new UserRecipient("")` is constructed
- **THEN** the call throws `ArgumentException` (not `ArgumentNullException`)
- **AND** the XML-doc matches the actual exception type

### Requirement: BaseSignalEventArgs.Account is non-nullable
`BaseSignalEventArgs.Account` SHALL be `string` (non-nullable). At dispatch time the account is always resolved from the subscription id before the event is constructed; consumers should not be forced to null-check it.

#### Scenario: Subscriber reads Account
- **WHEN** a subscriber processes any `*EventArgs`
- **THEN** `evt.Account` is non-null
- **AND** no analyzer warns about a possible null dereference

### Requirement: Response collections are wrappers, not List<T>-derived
`ListAccountsResponse` and `ListGroupsResponse` SHALL be wrapper records exposing an `IReadOnlyList<T>` property, not classes that inherit from `List<T>` (Microsoft `CA1010` — *do not inherit from List<T>* in the public surface). The on-the-wire JSON SHALL still be a JSON array; achieved via `[JsonConverter]` on the wrapper or via a positional record param typed as `IReadOnlyList<T>`.

#### Scenario: Wire format unchanged
- **GIVEN** the same signal-cli response JSON array
- **WHEN** deserialized in v3
- **THEN** the result is a wrapper whose `.Accounts`/`.Groups` property is a non-null list
- **AND** the wire array length matches the property's `Count`

### Requirement: JsonRpcException follows CA1032
`JsonRpcException` SHALL provide the three standard exception constructors (`()`, `(string)`, `(string, Exception)`) in addition to the existing `(JsonRpcError)` constructor (Microsoft `CA1032` — *Implement standard exception constructors*).

#### Scenario: Default-constructed exception
- **WHEN** `throw new JsonRpcException()` runs
- **THEN** the exception's `Message` is the default localized message
- **AND** `Error` is null or a default-coded JsonRpcError

### Requirement: Unused JSON-RPC error code path is removed
The currently-unused `JsonRpcException(string, Exception?)` constructor (which fabricates an error with non-standard code `-32000`) SHALL be removed. If a future call site needs to throw with a custom code, it SHALL pass a fully-formed `JsonRpcError` to the existing primary constructor. JSON-RPC 2.0's "Internal error" canonical code is `-32603`; the spec disallows ad-hoc `-32000` for the internal-error semantic.

#### Scenario: Static analysis catches removed ctor's callers
- **WHEN** the build runs after the constructor's removal
- **THEN** every call site that referenced `new JsonRpcException(string, Exception)` produces CS1739/CS1503 errors
- **AND** the contributor migrates to the primary constructor
