## ADDED Requirements

### Requirement: SessionStart hook prepares Claude Code on the Web sessions
The repository SHALL ship a `SessionStart` hook at `.claude/hooks/session-start.sh` registered via `.claude/settings.json`, that on every fresh Claude-Code-on-the-Web container:
1. installs `dotnet-sdk-10.0` (apt) if it is missing,
2. runs `dotnet restore` on the unit-test project, populating the NuGet cache,
3. runs a sanity `dotnet build` on `src/SignalCli/SignalCli.csproj`,
4. deliberately SKIPS building runtime packages (`SignalCli.runtime*`) so signal-cli / Temurin downloads do not run on every session.

#### Scenario: Fresh remote container
- **GIVEN** a freshly-spawned remote container with no `dotnet` and an empty NuGet cache
- **WHEN** the SessionStart hook completes
- **THEN** `dotnet --version` reports `10.0.x`
- **AND** `dotnet build src/SignalCli/SignalCli.csproj --no-restore` succeeds
- **AND** `dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj --no-restore` passes the unit suite

#### Scenario: Hook is idempotent
- **WHEN** the hook is invoked a second time on the same container
- **THEN** it completes successfully without errors
- **AND** it does NOT re-download the SDK or NuGet packages

### Requirement: Hook is no-op locally
The hook SHALL execute the dependency-install path only when `CLAUDE_CODE_REMOTE=true`. Locally-running Claude Code instances SHALL see a no-op so the contributor's `~/.dotnet` is untouched.

#### Scenario: Hook run locally
- **GIVEN** the environment variable `CLAUDE_CODE_REMOTE` is unset or not equal to `"true"`
- **WHEN** the hook is invoked
- **THEN** it exits 0 immediately without invoking `apt` or `dotnet`

### Requirement: Hook tolerates broken third-party APT sources
The hook SHALL succeed when `apt-get update` reports failures from third-party PPAs (e.g. `deadsnakes`, `ondrej`), since `dotnet-sdk-10.0` resolves from the official Ubuntu `noble-updates` / `noble-security` archives.

#### Scenario: PPA returns 403 during update
- **GIVEN** a container whose APT sources include a PPA that returns 403
- **WHEN** `apt-get update` runs as part of the hook
- **THEN** the failure is tolerated (logged, not fatal)
- **AND** `apt-get install dotnet-sdk-10.0` resolves and installs successfully

### Requirement: Cloud workflow is documented
`docs/cloud-development.md` SHALL document the cloud workflow: what the hook does, what it deliberately skips, the common in-session commands, the network policy needed for the full solution build, and how to switch the hook to async if startup latency becomes an issue.

#### Scenario: Contributor reads the doc and uses the project
- **WHEN** a new contributor opens a fresh Claude Code on the Web session
- **AND** consults `docs/cloud-development.md`
- **THEN** they can run unit tests, build the library, and selectively build the full solution without trial-and-error

### Requirement: CLAUDE.md links the cloud doc
`CLAUDE.md` SHALL include a "Cloud development" pointer linking to `docs/cloud-development.md`, so AI coding agents reading `CLAUDE.md` can find the cloud workflow.

#### Scenario: Agent looks up the cloud workflow
- **WHEN** an agent reads `CLAUDE.md`
- **THEN** it finds a "Cloud development" section
- **AND** the section points to `docs/cloud-development.md`
