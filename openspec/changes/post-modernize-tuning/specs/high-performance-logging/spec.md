## ADDED Requirements

### Requirement: All services-layer log calls use source-generated `LoggerMessage`
Every log call in `src/SignalCli/Services/**` SHALL go through a `[LoggerMessage]`-attributed `partial` method (Microsoft *High-performance logging in .NET*). Direct `_logger.LogInformation(...)` / `LogDebug` / `LogTrace` / `LogWarning` / `LogError` extension-method calls SHALL NOT remain on services-layer hot paths.

#### Scenario: Reader path emits a notification log
- **GIVEN** `JsonRpcClient` receives a notification line
- **WHEN** the line is processed
- **THEN** the log emission goes through a generated partial method (no template parsing at runtime; no parameter boxing)
- **AND** the call site reads `Log.NotificationReceived(_logger, methodName)`, not `_logger.LogDebug("…{Method}…", methodName)`

### Requirement: Event IDs are stable and per-service
Each service SHALL own an event-ID range and SHALL NOT overlap with another service. The ranges SHALL be documented in the `Log.<Service>` static class.

#### Scenario: Aggregator filters by stable ID
- **WHEN** a log consumer filters by `EventId.Id == 3001`
- **THEN** they get exactly the JSON-RPC "request sent" events, not any other service's

### Requirement: Logging privacy invariant is preserved verbatim
The existing `logging-privacy` capability (from `address-audit-findings`) — *no PII (message body, phone number, attachment payload) above `Trace`* — SHALL hold verbatim after the migration. Each migrated call site SHALL keep the same log level it had before.

#### Scenario: `PrivacyLoggingTests` still passes
- **WHEN** the test suite runs after migration
- **THEN** `PrivacyLoggingTests` passes with no asserts modified
- **AND** the `Information`-level log of `ListAccounts` contains the count, not the account list

### Requirement: Analyzers enforce the rule going forward
`.editorconfig` SHALL enable `CA1848` (use `LoggerMessage`) and `CA1873` (avoid expensive logging args) at `warning` severity. New code that introduces a non-source-generated logging call on the services layer SHALL surface as a warning.

#### Scenario: A new contributor adds `_logger.LogInformation(...)` in services
- **WHEN** the build runs
- **THEN** `CA1848` is reported as a warning at that line
- **AND** the contributor is steered to the per-service `Log.<Service>` class
