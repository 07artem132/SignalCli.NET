## ADDED Requirements

### Requirement: `AddSignalCli(Action<Config>?)` SHALL NOT exist

`ServiceCollectionExtensions` SHALL NOT expose an overload of `AddSignalCli` accepting `Action<Config>?`. The two surviving registration overloads are:

- `AddSignalCli(Action<SignalCliOptions>?)` — programmatic configuration.
- `AddSignalCli(IConfiguration)` — binding from `appsettings.json` (AOT-safe via configuration-binding source-generator).

Additionally, the `AddSignalCliWithBundledRuntimeDefaults(Action<SignalCliOptions>?)` extension (added in capability `config-auto-resolve-migration`) provides the bundled-runtime auto-resolve UX that the legacy overload previously bundled with its `Config.CreateDefault()` call.

#### Scenario: Legacy overload no longer compiles
- **WHEN** a consumer writes `services.AddSignalCli((SignalCli.Models.Config cfg) => …)`
- **THEN** the compiler reports CS1061 — the overload does not exist
- **AND** the migration recipe is in `CHANGELOG.md [4.0.0]`

### Requirement: `SignalCliOptionsExtensions.ToOptions(Config)` and `ToIOptions(Config)` SHALL NOT exist

These two `Config → SignalCliOptions` adapters served the legacy registration path only. After the legacy overload is gone they are dead code; SHALL be deleted along with the `#pragma warning disable CS0618` block that scoped them.

#### Scenario: No `CS0618` suppressions remain in src
- **WHEN** `grep -rn '#pragma warning disable CS0618' src/SignalCli/` runs after the change
- **THEN** the output is empty

### Requirement: `ServiceCollectionExtensions.CopyFrom` helper SHALL NOT exist

The private `CopyFrom(SignalCliOptions src, SignalCliOptions dst)` field-copier was internal scaffolding for the legacy overload (it cloned a snapshot built from `Config.CreateDefault()` + consumer `Action<Config>` overrides). With the legacy overload removed, this helper has no callers. SHALL be deleted.

#### Scenario: The helper is gone
- **WHEN** `grep -rn 'CopyFrom' src/SignalCli/Extensions/` runs after the change
- **THEN** the output is empty
