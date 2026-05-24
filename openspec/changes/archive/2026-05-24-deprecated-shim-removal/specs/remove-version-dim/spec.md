## ADDED Requirements

### Requirement: `ISignalCliClient.Version()` SHALL NOT exist

`ISignalCliClient` SHALL expose `VersionAsync(CancellationToken)` only. The `[Obsolete]` default-interface-method `Version(CancellationToken)` that delegates to `VersionAsync` SHALL be deleted. Consumers MUST migrate from `client.Version(...)` to `client.VersionAsync(...)` — the sed-friendly pattern is `s/\.Version(/\.VersionAsync(/g`.

#### Scenario: Calling Version() no longer compiles
- **WHEN** a consumer writes `await signalCliClient.Version()`
- **THEN** the compiler reports CS1061 (method does not exist) — not a deprecation warning, because the member is gone
- **AND** the consumer rewrites the call to `await signalCliClient.VersionAsync()` to fix it

#### Scenario: Public API surface no longer lists Version
- **WHEN** `PublicApiSurfaceTests` runs after the change
- **THEN** the canonical surface baseline does NOT contain `M:SignalCli.Interfaces.SignalCli.ISignalCliClient.Version(System.Threading.CancellationToken)`
- **AND** the baseline does contain `M:SignalCli.Interfaces.SignalCli.ISignalCliClient.VersionAsync(System.Threading.CancellationToken)`
