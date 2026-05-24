## ADDED Requirements

### Requirement: Bundled-runtime defaults SHALL be discoverable as an explicit extension

`ServiceCollectionExtensions` SHALL expose a public extension method `AddSignalCliWithBundledRuntimeDefaults(Action<SignalCliOptions>? configure = null)` that registers SignalCli services with the following defaults pre-populated, before the consumer's `configure` delegate runs:

- `AppHome = AppContext.BaseDirectory`
- `LibDirectory = "SignalCli/lib"`
- `JavaExecutable = JavaPathResolver.TryResolveJavaPath(AppHome)` — searches bundled JRE, `JAVA_HOME`, Windows Oracle path, and system `PATH` in that order; falls back to `string.Empty` if none found.

The consumer's `configure` delegate, if supplied, runs AFTER defaults and SHALL be able to override any of these settings (for example, set `SignalCliExecutable` to a Linux native binary path while nulling `JavaExecutable`).

#### Scenario: Bundled-runtime defaults populate paths for the bundled-JRE consumer
- **GIVEN** a Windows or macOS test host with `SignalCli.Runtime.Jre.*` package unpacked to `<TargetDir>/jre/bin/java[.exe]`
- **WHEN** `services.AddSignalCliWithBundledRuntimeDefaults()` is called with no overrides
- **THEN** `IOptions<SignalCliOptions>.Value.JavaExecutable` is a non-empty existing path to the bundled JRE
- **AND** `LibDirectory` equals `"SignalCli/lib"`
- **AND** `AppHome` equals `AppContext.BaseDirectory`

#### Scenario: Consumer override beats default
- **GIVEN** a Linux test host with no bundled JRE (native runtime mode)
- **WHEN** `services.AddSignalCliWithBundledRuntimeDefaults(opts => { opts.SignalCliExecutable = "/path/to/signal-cli"; opts.JavaExecutable = null; })` is called
- **THEN** options resolve cleanly (no `OptionsValidationException`)
- **AND** the cross-field XOR validator accepts `SignalCliExecutable` alone

### Requirement: Java path resolution SHALL live in `JavaPathResolver`, not `Config`

A new internal static class `SignalCli.Utilities.JavaPathResolver` SHALL host the bundled-JRE / `JAVA_HOME` / `PATH` / Windows-Oracle-folder lookup logic. The class SHALL be `internal` and gated to `SignalCli.Tests` via the existing `[InternalsVisibleTo]` allowance in `SignalCli.csproj`. Methods:

- `internal static string? TryResolveBundledJre(string baseDirectory)` — returns the path to `<baseDirectory>/jre/bin/java[.exe]` if it exists, else `null`.
- `internal static string? TryResolveOnPath(string executable)` — searches `PATH` directories, returns the first hit, else `null`.
- `internal static string TryResolveJavaPath(string baseDirectory)` — orchestrates the above in order: bundled → `JAVA_HOME` → Windows Oracle → `PATH`. Returns `string.Empty` if nothing found (preserves the prior `Config.TryResolveJavaPath` swallow-and-empty contract used by the cross-field XOR validator).

#### Scenario: Resolver is reachable from unit tests
- **WHEN** `Tests/SignalCli.Tests/JavaPathResolverTests.cs` references `JavaPathResolver.TryResolveOnPath`
- **THEN** the test project compiles
- **AND** the resolver methods are not exposed on the public surface of `SignalCli.dll`

### Requirement: Integration E2E tests SHALL NOT use `#pragma warning disable CS0618`

Every test file under `Tests/SignalCli.Tests.Integration/` SHALL register SignalCli via `AddSignalCliWithBundledRuntimeDefaults` (or, for tests that explicitly cover the `IConfiguration` overload, `AddSignalCli(IConfiguration)`). No `#pragma warning disable CS0618` block SHALL exist anywhere in `Tests/SignalCli.Tests.Integration/`.

#### Scenario: Integration project compiles without obsolete-API suppression
- **WHEN** `dotnet build Tests/SignalCli.Tests.Integration/SignalCli.Tests.Integration.csproj -p:TreatWarningsAsErrors=true` runs
- **THEN** the build succeeds
- **AND** `grep -rn '#pragma warning disable CS0618' Tests/SignalCli.Tests.Integration/` returns no results
