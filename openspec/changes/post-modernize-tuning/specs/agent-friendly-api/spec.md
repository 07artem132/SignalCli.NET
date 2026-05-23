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
