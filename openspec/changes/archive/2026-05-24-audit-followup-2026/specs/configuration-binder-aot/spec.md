## ADDED Requirements

### Requirement: `AddSignalCli(IConfiguration)` SHALL be AOT-safe

`src/SignalCli/SignalCli.csproj` SHALL declare `<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>` so that `OptionsBuilder<SignalCliOptions>.Bind(IConfiguration)` emits source-generated, reflection-free binding code (Microsoft *Configuration binding source generator*).

`ServiceCollectionExtensions.AddSignalCli(IConfiguration)` and the helper `ConfigureOptionsFromConfiguration` SHALL NOT carry `[RequiresUnreferencedCode]` or `[RequiresDynamicCode]` attributes. The XML doc paragraph that today reads "AOT-warning: для AOT-deploy'у користуйтеся `AddSignalCli(Action<SignalCliOptions>?)`-overload'ом" SHALL be removed.

#### Scenario: Building with TreatWarningsAsErrors leaves IConfiguration overload clean
- **WHEN** `dotnet build src/SignalCli/SignalCli.csproj -p:TreatWarningsAsErrors=true` runs
- **THEN** no `IL2026` or `IL3050` warning originates from either `AddSignalCli(IConfiguration)` or `ConfigureOptionsFromConfiguration`
- **AND** the build succeeds

#### Scenario: A consumer publishes with Native AOT using the IConfiguration overload
- **GIVEN** a probe console app that references `SignalCli.NET`, binds `SignalCli` from `appsettings.json`, and calls `services.AddSignalCli(configuration.GetSection("SignalCli"))`
- **WHEN** the consumer runs `dotnet publish -c Release /p:PublishAot=true`
- **THEN** publication completes without warnings sourced from the `SignalCli` assembly

#### Scenario: Existing in-memory binding test stays green
- **GIVEN** `OptionsValidationTests.AddSignalCli_FromConfiguration_BindsAppsettingsValues` exercises the in-memory binding path
- **WHEN** the test runs after `<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>` is enabled
- **THEN** the test passes with the same assertions
- **AND** scalar properties (`AppHome`, `JavaExecutable`, `MaxRestartAttempts`, `RequestTimeoutSeconds`) bind identically to before
