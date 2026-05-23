## ADDED Requirements

### Requirement: Library declares AOT compatibility
`src/SignalCli/SignalCli.csproj` SHALL set `<IsAotCompatible>true</IsAotCompatible>`. This auto-enables `EnableTrimAnalyzer`, `EnableSingleFileAnalyzer`, and `EnableAotAnalyzer` (Microsoft *Native AOT — AOT-compatibility analyzers*).

#### Scenario: Library builds without trim/AOT warnings
- **WHEN** `dotnet build src/SignalCli/SignalCli.csproj` runs
- **THEN** zero `IL2026`, `IL2104`, `IL3050` (or higher) diagnostics are reported
- **AND** the build succeeds with `TreatWarningsAsErrors=true`

#### Scenario: A consumer publishes with Native AOT
- **GIVEN** a probe console app that references `SignalCli.NET`
- **WHEN** the consumer runs `dotnet publish -c Release /p:PublishAot=true`
- **THEN** publication completes without warnings sourced from the `SignalCli` assembly

### Requirement: Serialization uses source-generated metadata only
`SignalJson.Options.TypeInfoResolver` SHALL be the source-generated `SignalJsonContext.Default` only. The runtime reflection fallback (`DefaultJsonTypeInfoResolver`) SHALL NOT be combined into the public resolver. Every type that is serialized or deserialized at runtime SHALL be registered in `SignalJsonContext`.

#### Scenario: A previously-unregistered type is detected at build time
- **WHEN** new code calls `JsonSerializer.Serialize<TWidget>(...)` with `TWidget` missing from `SignalJsonContext`
- **THEN** the AOT analyzer reports `IL2026`
- **AND** the build fails

### Requirement: Serialization uses fast-path mode
`SignalJsonContext` SHALL be annotated with `[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]`. The generator MUST emit both metadata and fast-path serialization for registered types (Microsoft *Reflection vs source generation*).

#### Scenario: Round-trip is identical to the metadata-only baseline
- **WHEN** every existing `JsonSerializationTests` assertion runs against the new mode
- **THEN** every assertion passes with no message-content changes

### Requirement: No `Nito.AsyncEx` dependency
`SignalCli.csproj` SHALL NOT reference `Nito.AsyncEx`. `JsonRpcClient` and `SignalCliHostedService` SHALL use `System.Threading.SemaphoreSlim(1, 1)` (Microsoft *Async semaphores, locks, and reader/writer coordination*) where they previously used `AsyncLock`.

#### Scenario: NuGet graph after the change
- **WHEN** `dotnet list package --include-transitive` runs against the library
- **THEN** `Nito.AsyncEx` does not appear in the output
