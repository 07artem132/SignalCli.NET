## ADDED Requirements

### Requirement: Unit tests run on virtual time
Every wall-clock `await Task.Delay(...)` in `Tests/SignalCli.Tests/**` (i.e. excluding the integration project) SHALL be replaced by virtual-time advancement via `Microsoft.Extensions.Time.Testing.FakeTimeProvider`. The unit-test suite SHALL complete deterministically irrespective of CI load.

#### Scenario: Restart-window test
- **GIVEN** `Config.RestartWindowSeconds = 1`
- **WHEN** `B6_WindowedRestartBudget` runs
- **THEN** the test uses `fakeTime.Advance(TimeSpan.FromSeconds(1) + epsilon)` to simulate stability
- **AND** total wall-clock test duration is < 200 ms

#### Scenario: Health-monitor interval test
- **GIVEN** `Config.HealthCheckIntervalSeconds = 1`
- **WHEN** the loop is exercised with `fakeTime.Advance(1s)` × 3
- **THEN** exactly 3 pings are observed
- **AND** the test does not perform any real-time sleep

### Requirement: Tests access internals through documented seams, not reflection
The pattern `obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)` SHALL NOT be used in tests. Internals exposed for testing SHALL be `internal` members on the SUT (gated by the existing `[InternalsVisibleTo("SignalCli.Tests")]`), grouped under a clearly-named `TestSeam` accessor.

#### Scenario: Test reads the current process
- **WHEN** a test needs to inspect the live `IProcess` held by `SignalCliHostedService`
- **THEN** the test reads `service.TestSeam.CurrentProcess`
- **AND** no reflection is involved

### Requirement: Race-condition findings have tests
A regression test SHALL exist for each race-condition capability introduced by this change:
- `state-machine-thread-safety` — synchronous-subscriber reentrancy test
- `subscription-race-safety` — 10-caller parallel `SubscribeAsync` test
- `rpc-back-pressure` — slow-subscriber 1 000-message test

#### Scenario: All race tests run in CI under the standard test command
- **WHEN** `dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj` runs
- **THEN** the three race tests execute and pass
- **AND** each test completes within 5 seconds wall-clock

### Requirement: Test framework targets Microsoft.Testing.Platform
The unit-test project SHALL migrate from `xunit 2.x` / `xunit.runner.visualstudio` (VSTest) to `xunit.v3` running on Microsoft.Testing.Platform (Microsoft *Testing in .NET — xUnit.net v3*).

#### Scenario: Tests run via `dotnet test` on MTP
- **WHEN** `dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj` runs
- **THEN** MTP executes the suite
- **AND** the test count, names, and outcomes match the pre-migration baseline (modulo new race tests)

### Requirement: Mocks are Strict by default in the test suite
`MockBehavior.Strict` SHALL be used for `Mock<T>` instances across `Tests/SignalCli.Tests/**`. Any unmocked call to a Strict mock makes the test fail loudly — preventing the silent regressions that `MockBehavior.Loose` allows (an unexpected call returns `default(T)`).

#### Scenario: Unexpected invocation on a strict mock
- **GIVEN** a test that mocks `ISignalCliClient` with `MockBehavior.Strict`
- **WHEN** the SUT calls a method that the test did not `Setup`
- **THEN** the test fails with `MockException` naming the unexpected call
- **AND** the contributor is guided to add the missing `Setup` (or to delete the SUT's unexpected call)

#### Scenario: Exception to the rule is justified inline
- **WHEN** a specific test legitimately needs Loose behavior (e.g., the SUT iterates a long list of optional methods)
- **THEN** the `MockBehavior.Loose` argument is present at the construction site
- **AND** a comment names the reason

### Requirement: Hosted-service fixtures use IAsyncLifetime
Test bases that instantiate hosted services for fixtures SHALL implement `IAsyncLifetime` so `StartAsync`/`StopAsync` are awaited, not block-waited.

#### Scenario: Hosted-service fixture teardown
- **WHEN** a test class deriving from the migrated base finishes
- **THEN** `DisposeAsync` awaits `host.StopAsync()`
- **AND** no `host.StopAsync().Wait()` (sync-over-async) remains
