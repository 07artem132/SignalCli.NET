## ADDED Requirements

### Requirement: Target .NET 10
All projects SHALL target `net10.0`, and CI SHALL build and test on the .NET 10 SDK.

#### Scenario: Solution builds on .NET 10
- **WHEN** the solution is built with the .NET 10 SDK
- **THEN** all projects compile with zero errors

#### Scenario: CI uses .NET 10
- **WHEN** the CI workflow runs
- **THEN** it installs the `10.0.x` SDK and runs build + tests

### Requirement: Graceful child-process shutdown
On supported platforms the library SHALL attempt a graceful shutdown of signal-cli (process-group signal / `exit`) before forcibly killing it, and SHALL fall back to `Kill(entireProcessTree)` only if the process does not exit within the grace window.

#### Scenario: Process exits gracefully
- **WHEN** the hosted service stops and signal-cli responds to the graceful shutdown within the grace window
- **THEN** the process is not force-killed

#### Scenario: Process hangs
- **WHEN** signal-cli does not exit within the grace window
- **THEN** the library force-kills the process tree as a last resort
