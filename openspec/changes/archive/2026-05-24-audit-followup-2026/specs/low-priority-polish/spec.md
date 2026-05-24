## ADDED Requirements

### Requirement: `Example/Program.cs` SHALL NOT cast the configure delegate

The bundled `Example/SignalCli.Example/Program.cs` SHALL call `services.AddSignalCli(o => …)` without an explicit `(Action<SignalCliOptions>)` cast. Overload resolution between `AddSignalCli(Action<SignalCliOptions>?)`, `AddSignalCli(IConfiguration)`, and `AddSignalCli(Action<Config>?)` is unambiguous for a delegate literal — the cast is misleading and signals to readers (especially AI agents copying the example) that an overload-resolution problem exists when none does.

#### Scenario: Example compiles without the cast
- **GIVEN** the line `services.AddSignalCli((Action<SignalCliOptions>)(o => { … }))` in `Example/SignalCli.Example/Program.cs`
- **WHEN** the maintainer rewrites it to `services.AddSignalCli(o => { … })`
- **THEN** the file compiles with `TreatWarningsAsErrors=true`
- **AND** the runtime behaviour is identical (same overload selected, same options object built)

### Requirement: CLAUDE.md SHALL document the .NET 10 BackgroundService.ExecuteAsync behaviour change

CLAUDE.md "Established patterns — Background loops + time" SHALL include a one-paragraph note that, starting in .NET 10, `BackgroundService.ExecuteAsync` runs entirely on a background thread (no longer blocks startup on its synchronous prefix). `SignalCliHealthMonitor.ExecuteAsync` is unaffected because it never blocks startup — but the note ensures future contributors do not place synchronous-blocking initialization at the top of `ExecuteAsync` expecting `StartAsync` semantics.

#### Scenario: CLAUDE.md mentions the .NET 10 ExecuteAsync change
- **WHEN** an agent reads CLAUDE.md "Established patterns — Background loops + time"
- **THEN** the paragraph names the .NET 10 behaviour change with a link to Microsoft's breaking-change page
- **AND** clarifies that `SignalCliHealthMonitor` is already aligned (no `PeriodicTimer` work happens before the first `WaitForNextTickAsync`)
