## ADDED Requirements

### Requirement: Serialized crash-recovery
The unexpected-exit handler SHALL acquire the same operation lock as `StartAsync`/`StopAsync`/`ForceRestartAsync` before mutating process state or initiating an auto-restart. It MUST re-check the `Disposed` and `Stopping` predicates inside the lock and MUST NOT crash the host on an escaped exception (the `async void` wrapper MUST `try`/`catch` and log).

#### Scenario: Intentional stop concurrent with unexpected exit
- **GIVEN** the process is `Running`
- **WHEN** `StopAsync` begins to acquire the operation lock at the same instant the process exits unexpectedly
- **THEN** exactly one cleanup runs, no auto-restart is initiated after the intentional stop, and no exception escapes the exited handler

#### Scenario: Exited handler throws
- **GIVEN** an exception is thrown inside the exited handler (e.g., a logger failure)
- **WHEN** the handler returns
- **THEN** the host process does not crash; the exception is logged and the supervisor remains in a defined state

### Requirement: Windowed restart budget
The auto-restart budget (`Config.MaxRestartAttempts`) SHALL be **windowed**, not a lifetime counter: when the process reaches `Running` and remains there for at least `Config.RestartWindowSeconds` (default 60 s), the restart count SHALL reset to 0. Both the auto-restart and force-restart paths SHALL increment the counter under the operation lock.

#### Scenario: Sporadic crashes over a long lifetime
- **GIVEN** `MaxRestartAttempts = 3` and `RestartWindowSeconds = 60`
- **AND** the process has run for several hours with a single earlier transient crash + recovery
- **WHEN** the process crashes again
- **THEN** auto-restart proceeds (the budget has been reset by the stable-window timer)

#### Scenario: Burst of crashes within the window
- **GIVEN** `MaxRestartAttempts = 3` and `RestartWindowSeconds = 60`
- **WHEN** the process crashes 4 times within 60 s
- **THEN** the 4th unexpected exit does not trigger another auto-restart and the failure is logged
