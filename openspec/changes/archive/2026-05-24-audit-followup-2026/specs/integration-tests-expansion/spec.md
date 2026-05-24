## ADDED Requirements

### Requirement: Integration suite SHALL cover the start/stop/restart cycle with a real process

`Tests/SignalCli.Tests.Integration/` SHALL include a test that drives `SignalCliHostedService` through `Start → Stop → Start` against a real bundled `signal-cli` runtime, capturing `ProcessStateManager.StateChanged` and asserting the observed state sequence.

The test SHALL share the existing `TryBuildHost` runtime-availability skip-gate so it does not fail on platforms where the bundled JRE / native binary is not present.

#### Scenario: A real signal-cli cycles through Running and Stopped
- **GIVEN** the bundled runtime is present (Win/macOS JRE or Linux native)
- **WHEN** the test starts the host, observes Running, stops it, observes Stopped, and starts a second time
- **THEN** the captured state sequence contains `Running, Stopping, Stopped, Starting, Running`
- **AND** the second `Running` corresponds to a different OS process id than the first

#### Scenario: Missing runtime SHALL skip cleanly
- **GIVEN** no bundled runtime is present on the test host
- **WHEN** the test runs
- **THEN** the test prints a skip reason to standard error and returns without failing

### Requirement: Integration suite SHALL verify external-kill auto-restart

A test SHALL kill the real `signal-cli` process out-of-band (via `Process.GetProcessById(pid).Kill(entireProcessTree: true)`) and assert that `SignalCliHostedService.OnProcessExited` triggers auto-restart, that the `signalcli.process.restarts` counter increments with `trigger=crash`, and that the new process answers `version` successfully.

#### Scenario: External kill is observed and recovered
- **GIVEN** `MaxRestartAttempts=2`, `RestartDelaySeconds=1`, and the host is `Running`
- **WHEN** the test calls `Process.GetProcessById(currentPid).Kill(entireProcessTree: true)`
- **THEN** within 30 wall-clock seconds the host returns to `Running` with a new PID
- **AND** the `signalcli.process.restarts` Meter recorded at least one measurement with `trigger=crash`

### Requirement: Integration suite SHALL verify health-monitor cadence over real time

A test SHALL configure `HealthCheckIntervalSeconds=2`, start the host, sleep 5 wall-clock seconds, and assert that `SignalCliHealthMonitor.LastPingResult.Ok == true` with a `LastPingResult.At` timestamp within the last 5 seconds.

This test is the explicit documented exception to the no-wall-clock-Task.Delay rule that applies to unit tests under `Tests/SignalCli.Tests/SignalCliHealthMonitor/`. The XML doc of the test SHALL state this exception explicitly.

#### Scenario: Health monitor pings the real signal-cli at the configured cadence
- **GIVEN** the host is started with `HealthCheckIntervalSeconds=2`
- **WHEN** the test sleeps 5 wall-clock seconds and reads `monitor.LastPingResult`
- **THEN** `LastPingResult.Ok == true`
- **AND** `(DateTimeOffset.UtcNow - LastPingResult.At).TotalSeconds < 5`

### Requirement: Integration suite SHALL verify mid-flight DisposeAsync leaves no orphan process

A test SHALL start the host, capture the child process PID, fire `VersionAsync` without awaiting it, and call `await host.DisposeAsync()`. The test SHALL assert that within 5 wall-clock seconds the captured PID has exited.

#### Scenario: DisposeAsync mid-flight terminates the child process
- **GIVEN** the host is `Running` with a captured child PID
- **AND** a `VersionAsync` call is in flight without `await`
- **WHEN** `await host.DisposeAsync()` returns
- **THEN** within 5 seconds `Process.GetProcessById(childPid)` either throws `ArgumentException` (process gone) or returns a process with `HasExited == true`

### Requirement: Integration suite SHALL verify `AddSignalCli(IConfiguration)` end-to-end with a real process

A test SHALL use the `AddSignalCli(IConfiguration)` overload with an in-memory `IConfiguration` section to start a real `signal-cli` and call `version`. This test pins the `configuration-binder-aot` capability end-to-end.

#### Scenario: IConfiguration overload starts a real signal-cli
- **GIVEN** an in-memory `IConfiguration` section with platform-appropriate `AppHome`, `LibDirectory`/`SignalCliExecutable`, and `JavaExecutable`
- **WHEN** the test calls `services.AddSignalCli(configuration.GetSection("SignalCli"))`, builds the host, starts it, and calls `version`
- **THEN** `version.Version` is non-empty and contains "0.14"
