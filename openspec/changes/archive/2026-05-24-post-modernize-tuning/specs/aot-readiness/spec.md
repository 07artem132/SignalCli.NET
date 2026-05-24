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

### Requirement: Test suite migrates off the reflection fallback
Tests that today serialize anonymous types via the runtime reflection fallback (`JsonRpcClientTests.cs:106,128,150,174`; `JsonSerializationTests.cs:20`) SHALL be migrated. Two options are acceptable; the implementation may choose per-call site:
- **Option A — concrete DTO:** replace `new { … }` with a concrete record registered in `SignalJsonContext` (or in a tests-only `JsonSerializerContext`).
- **Option B — tests-only options:** expose `internal static JsonSerializerOptions SignalJson.OptionsForTests` (via `[InternalsVisibleTo("SignalCli.Tests")]`) that adds `DefaultJsonTypeInfoResolver` as a fallback. The production `SignalJson.Options` SHALL remain source-gen-only.

#### Scenario: Production serialization is source-gen-only
- **WHEN** the production library serializes any type
- **THEN** the path is purely source-generated metadata (no `DefaultJsonTypeInfoResolver`)

#### Scenario: Test code paths are isolated from production fallback
- **GIVEN** Option B is chosen for some tests
- **WHEN** the production assembly is published (e.g. AOT)
- **THEN** `SignalJson.OptionsForTests` is not reachable from the public surface
- **AND** the AOT analyzer reports zero warnings for the production assembly

### Requirement: CLAUDE.md rule 6 SHALL be updated post-implementation
This capability's implementation SHALL update `CLAUDE.md` rule 6 to remove the phrase "(which combines the source-gen resolver with a reflection fallback)" and to state that every serializable type MUST be in `SignalJsonContext` (this is mirrored as a `code-hygiene` requirement so the doc fix is tracked even if the implementation lands in stages).

#### Scenario: Documentation matches reality
- **WHEN** the change is complete
- **THEN** `CLAUDE.md` does not describe a reflection fallback that no longer exists
- **AND** future contributors are told to add new types to `SignalJsonContext`
