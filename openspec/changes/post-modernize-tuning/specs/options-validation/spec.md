## ADDED Requirements

### Requirement: Config is registered via the Options pattern
`Config` SHALL be registered through `AddOptions<Config>()` with `ValidateDataAnnotations()` and `ValidateOnStart()` (Microsoft *Options pattern in .NET — Options validation*). The existing `services.AddSingleton<Config>(...)` registration SHALL be replaced.

#### Scenario: Invalid configuration fails at startup
- **GIVEN** `Config.MaxRestartAttempts = -1` is configured (out of range)
- **WHEN** the host starts
- **THEN** the host fails fast at startup with an `OptionsValidationException`
- **AND** the message names `MaxRestartAttempts` and the rule it violated

#### Scenario: Config binds from configuration sources
- **GIVEN** `appsettings.json` with a `"SignalCli": { ... }` section
- **WHEN** the consumer calls `services.AddSignalCli(Configuration.GetSection("SignalCli"))`
- **THEN** the values bind into `Config`
- **AND** all `[Range]`/`[Required]` annotations are enforced

### Requirement: Config has data-annotation constraints
The following constraints SHALL be enforced via `DataAnnotations` (or a custom `IValidateOptions<Config>`):
- `MaxRestartAttempts >= 0`
- `HealthCheckIntervalSeconds > 0`
- `HealthCheckTimeoutSeconds > 0`
- `RestartDelaySeconds >= 0`
- `StopTimeoutSeconds > 0`
- `RequestTimeoutSeconds > 0`
- `RestartWindowSeconds > 0`
- `AppHome` non-empty
- `LibDirectory` non-empty when `SignalCliExecutable` is null/empty (JVM mode)

#### Scenario: Out-of-range numeric value
- **GIVEN** `Config.RequestTimeoutSeconds = 0`
- **WHEN** the host starts
- **THEN** validation fails with a message naming `RequestTimeoutSeconds` and the rule (`> 0`)

#### Scenario: Mode-dependent rule
- **GIVEN** `Config.SignalCliExecutable` is null and `Config.LibDirectory` is empty
- **WHEN** the host starts
- **THEN** validation fails with a message that explains the JVM-mode constraint

### Requirement: Config is immutable on the public read path
After registration via DI, mutating `Config` SHALL be impossible through the public surface — every public property is `{ get; init; }`. Helper methods (e.g. `WithEnvironment`) return a new snapshot rather than mutating the captured instance. The current mixed `{ get; set; }` / `{ get; init; }` shape SHALL be resolved to all-`init` (Microsoft *Records — init-only setters*).

#### Scenario: A consumer tries to mutate after DI capture
- **GIVEN** a `Config` resolved from `IServiceProvider`
- **WHEN** the consumer attempts to assign `config.MaxRestartAttempts = 5`
- **THEN** the compiler rejects the assignment (init-only)

#### Scenario: The Action&lt;Config&gt; setup delegate still works
- **WHEN** the consumer calls `services.AddSignalCli(c => { c.MaxRestartAttempts = 5; })`
- **THEN** the delegate runs during DI registration (before `init` "freezes" the instance from the consumer perspective)
- **AND** the captured value persists across resolution

### Requirement: AddSignalCli accepts an IConfiguration overload
`services.AddSignalCli(IConfiguration section)` SHALL be available alongside the existing `services.AddSignalCli(Action<Config>? configure)`. The new overload binds the section to `Config` through the Options pipeline.

#### Scenario: Consumer wires config from JSON
- **WHEN** a consumer calls `services.AddSignalCli(builder.Configuration.GetSection("SignalCli"))`
- **THEN** every documented `appsettings.json` key under `"SignalCli"` binds into `Config`
- **AND** `ValidateOnStart` runs on the bound result
