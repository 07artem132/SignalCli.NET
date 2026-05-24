## ADDED Requirements

### Requirement: Obsolete-message removal versions SHALL be pinned by an executable test

A test SHALL exist that reflectively enumerates every `[ObsoleteAttribute]` on every member of `SignalCli.dll` and, for messages matching the regex `will be removed in (\d+)\.0`, asserts that the captured major version is strictly greater than the assembly's current major.

#### Scenario: A future drift to "removed in 3.0" while at 4.0 is detected
- **GIVEN** the package version is bumped to `4.0.0`
- **AND** a `[Obsolete("...; will be removed in 3.0")]` attribute remains anywhere in the assembly
- **WHEN** `dotnet test --filter ObsoleteMessageConsistency` runs
- **THEN** the test fails
- **AND** the failure message lists each offending `MemberInfo.FullName` and the matched removal-version, so an agent can fix without running `grep`

### Requirement: `[LoggerMessage]` EventIds SHALL stay inside reserved blocks

A test SHALL exist that reflectively enumerates `[LoggerMessageAttribute]` on every `partial` method of every `*Log.cs` class and asserts the `EventId` lies within the block declared for that class in `CLAUDE.md` "Established patterns — Logging".

The blocks are: 100-199 `SignalCliHostedServiceLog`, 200-299 `SignalCliHealthMonitorLog`, 300-399 `JsonRpcClientLog`, 400-499 `JsonRpcClientHostedServiceLog`, 500-599 `SignalEventServiceLog`, 600-699 `SignalServiceLog`, 700-799 `SignalMessageLog`, 800-899 `SignalAccountsLog`/`SignalDevicesLog`/`SignalGroupsLog`, 900-999 `ProcessRunnerLog`/`ProcessStateManagerLog`.

#### Scenario: A new logger method in JsonRpcClientLog using EventId 250 is rejected
- **GIVEN** an agent adds `[LoggerMessage(EventId = 250, ...)] public static partial void NewLog(...)` to `JsonRpcClientLog`
- **WHEN** the regression test runs
- **THEN** the test fails for `JsonRpcClientLog` because 250 ∉ [300, 399]
- **AND** the failure message names the offending method and the expected range

### Requirement: Public API surface SHALL be pinned by a baseline file

A test SHALL exist that reflectively walks every public type, member, parameter, and generic constraint of `SignalCli.dll`, emits a stable canonical-form line per member, sorts them, and compares the result to a baseline file `Tests/SignalCli.Tests/RegressionGuards/SignalCli.public-api.txt`.

On baseline mismatch, the test SHALL fail with a unified diff (`+` for added members, `-` for removed) so the reviewer reads it as a PR diff.

The baseline file SHALL be a normal source-controlled artifact. Intentional public-API changes are made by replacing the baseline file contents in the same commit as the API change.

#### Scenario: An accidental new public type is caught at test time
- **GIVEN** an agent adds `public class SignalCli.Internal.SomeHelper { }` (intended to be `internal`)
- **WHEN** the regression test runs
- **THEN** the test fails with `+ T:SignalCli.Internal.SomeHelper`
- **AND** the reviewer immediately sees the unintended public addition

#### Scenario: An intentional public API change updates the baseline
- **GIVEN** a developer intentionally adds `public sealed class NewClient`
- **WHEN** the developer runs the failing test and pastes the new output into the baseline file
- **THEN** subsequent test runs pass
- **AND** the baseline diff in the PR shows `+ T:SignalCli.NewClient` (plus its members) — reviewer signs off as a design decision
