## ADDED Requirements

### Requirement: `SignalCli.Models.Config` type SHALL NOT exist

The `[Obsolete]` `Config` class — including all its instance members (`AppHome`, `LibDirectory`, `JavaExecutable`, `SignalCliExecutable`, `MaxRestartAttempts`, `HealthCheckIntervalSeconds`, `HealthCheckTimeoutSeconds`, `RestartDelaySeconds`, `StopTimeoutSeconds`, `RequestTimeoutSeconds`, `RestartWindowSeconds`, `UseManualReceiveMode`, `CliLogLevelCli`, `LogFileCli`, `StoragePathCli`, `EnvironmentVariables`, `WithEnvironment`, `ToProcessConfig`, `BuildClasspath`, `CreateDefault`, `ResolveBundledJava`, `ResolveOnPath`, `TryResolveJavaPath`, `ResolveJavaPath`) — SHALL be deleted from `src/SignalCli/`.

`SignalCliOptions` is the sole configuration surface. The auto-resolve UX previously hosted on `Config.CreateDefault` is replaced by `AddSignalCliWithBundledRuntimeDefaults` (capability `config-auto-resolve-migration`). The path-resolution utilities are relocated to `SignalCli.Utilities.JavaPathResolver`.

#### Scenario: Type does not exist in any namespace
- **WHEN** `grep -rn 'class Config\b' src/SignalCli/` runs after the change
- **THEN** the output is empty
- **AND** `Reflection.Assembly.Load("SignalCli").GetType("SignalCli.Models.Config")` returns `null`

### Requirement: `SignalCliOptions.ToConfig()` SHALL NOT exist

The internal `ToConfig()` shim on `SignalCliOptions` (which produced a `Config` snapshot for the legacy `ProcessConfig` builder) SHALL be deleted along with its `#pragma warning disable CS0618` block.

#### Scenario: `ToConfig` is unreachable
- **WHEN** `grep -rn '\.ToConfig\b' src/SignalCli/` runs after the change
- **THEN** the output is empty

### Requirement: `SignalCliOptionsExtensions.ToProcessConfig` SHALL be self-contained

`SignalCliOptionsExtensions.ToProcessConfig(this SignalCliOptions)` SHALL implement the signal-cli launch-command construction directly (no delegation through `Config`). The implementation MUST preserve every observable behavior of the prior `Config.ToProcessConfig()`:

- Native-vs-JVM mode selection (`SignalCliExecutable` non-empty → native; else JVM-mode with classpath).
- Argument order: log-level (`-v`/`-vv`/`-vvv`) → `--log-file=<path>` → `--config=<path>` → `jsonRpc` → `--receive-mode=manual|on-start`.
- Path-separator choice (`;` on Windows, `:` elsewhere) for the JVM classpath.
- `WorkingDirectory = AppHome`, `CreateNewProcessGroup = true`, all three streams redirected.
- `EnvironmentVariables` passed through verbatim.

The classpath cache (`_cachedClasspath`) from `post-modernize-tuning §8c.10` SHALL be dropped — `ToProcessConfig` is called once per process start, and the cached value provided no measurable benefit in restart scenarios since `SignalCliHostedService` retains the `ProcessConfig` reference across restarts.

#### Scenario: Migrated tests pin protocol-level behavior
- **WHEN** `SignalCliOptionsExtensionsTests` (renamed from `ConfigTests`) runs
- **THEN** every assertion that previously verified `Config.ToProcessConfig` output passes against `SignalCliOptionsExtensions.ToProcessConfig` (SUT changed; assertions preserved verbatim)
- **AND** test count for the migrated suite is unchanged from `ConfigTests` pre-migration

### Requirement: The three-site duplication trap SHALL no longer exist

After this change, adding a new property to `SignalCliOptions` SHALL require updating exactly ONE site (`SignalCliOptions.cs` — the property declaration). The triplet of `Config.ToOptions` / `SignalCliOptionsExtensions.ToOptions(Config)` / `ServiceCollectionExtensions.CopyFrom` field-copiers SHALL be deleted with the `Config` type, eliminating the documented drift-risk that previously required all three sites to stay synchronized when adding a property.

#### Scenario: A new option property does not require triplet updates
- **GIVEN** a developer adds `public int FooSeconds { get; set; } = 5;` to `SignalCliOptions`
- **WHEN** they run `grep -rn 'AppHome\s*=' src/SignalCli/` to find sister field-copier sites
- **THEN** the only matches are tests and the new property declaration itself — no production field-copier helper remains
