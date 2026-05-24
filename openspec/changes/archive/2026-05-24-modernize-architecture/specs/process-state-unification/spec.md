## ADDED Requirements

### Requirement: Single source of truth for process state
Process state, the current stream pair, and readiness SHALL be derived from one authoritative state model (`ProcessStateManager`). The library SHALL NOT maintain separate, manually-synchronized representations of "process is up".

#### Scenario: State drives stream availability
- **WHEN** the process transitions to Running with a stream pair
- **THEN** `CurrentStreamPair` and the stream-pair change notification both reflect that pair without a separate update call

#### Scenario: State drives readiness
- **WHEN** the process becomes ready
- **THEN** pending `WaitForReadyAsync` callers complete, derived from the state model

#### Scenario: Stop clears derived values
- **WHEN** the process stops or fails
- **THEN** `CurrentStreamPair` becomes null and readiness waiters observe the not-ready state — all derived from the single model

### Requirement: No dead abstractions
Exposed reactive/state abstractions SHALL have at least one real consumer; otherwise they SHALL be removed.

#### Scenario: State observable is consumed
- **WHEN** the process-state observable is exposed
- **THEN** at least one component subscribes to it (e.g. readiness/stream-pair derivation)

### Requirement: Behavior parity after refactor
The restart, health-check, and readiness behaviors SHALL remain unchanged after unification, as verified by the existing hosted-service and health-monitor test suites.

#### Scenario: Existing suites pass
- **WHEN** the process-state refactor is complete
- **THEN** all pre-existing `SignalCliHostedService` and `SignalCliHealthMonitor` tests pass without behavioral changes
