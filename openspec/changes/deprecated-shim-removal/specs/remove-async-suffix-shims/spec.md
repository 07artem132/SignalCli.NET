## ADDED Requirements

### Requirement: The five Async-suffix-less shim methods SHALL NOT exist

The following `[Obsolete]` default-interface-method shims SHALL be deleted entirely from their respective interfaces. Only the `Async`-suffix sibling SHALL remain. Migration is a mechanical rename — sed patterns in the `CHANGELOG.md [4.0.0]` migration table.

- `ISignalAccounts.ListAccounts(CancellationToken)` → use `ListAccountsAsync(...)`
- `ISignalAccounts.SyncAccount(CancellationToken)` → use `SyncAccountAsync(...)`
- `ISignalDevices.StartLink(CancellationToken)` → use `StartLinkAsync(...)`
- `ISignalDevices.FinishLink(string, string, CancellationToken)` → use `FinishLinkAsync(...)`
- `ISignalGroups.ListGroups(string, CancellationToken)` → use `ListGroupsAsync(...)`

The `<summary>Застаріле: використовуйте …Async.</summary>` doc-comment block preceding each shim SHALL also be removed (orphan documentation for a deleted member is misleading).

#### Scenario: Calling a removed shim does not compile
- **WHEN** a consumer writes `await signalAccounts.ListAccounts()`
- **THEN** the compiler reports CS1061 — not a deprecation warning
- **AND** the fix is `await signalAccounts.ListAccountsAsync()`

#### Scenario: Surface baseline shrinks by exactly five `M:` entries
- **WHEN** `PublicApiSurfaceTests` runs after the change
- **THEN** the baseline diff contains exactly the following five removed lines (their argument-tuple shape may vary slightly with canonical form):
  - `M:SignalCli.Interfaces.Signal.ISignalAccounts.ListAccounts(...)`
  - `M:SignalCli.Interfaces.Signal.ISignalAccounts.SyncAccount(...)`
  - `M:SignalCli.Interfaces.Signal.ISignalDevices.StartLink(...)`
  - `M:SignalCli.Interfaces.Signal.ISignalDevices.FinishLink(...)`
  - `M:SignalCli.Interfaces.Signal.ISignalGroups.ListGroups(...)`
- **AND** no other public-surface lines change in this capability
