## ADDED Requirements

### Requirement: Verified signal-cli download
The download scripts (`download-signal-cli.ps1`, `download-signal-cli.sh`) SHALL verify the integrity of the downloaded `signal-cli` archive against a known SHA-256 hash before extraction, and MUST abort with a non-zero exit code if verification fails.

#### Scenario: Hash matches
- **WHEN** the downloaded archive's SHA-256 equals the expected pinned hash
- **THEN** the archive is extracted and the script exits 0

#### Scenario: Hash mismatch
- **WHEN** the downloaded archive's SHA-256 does not match the expected hash
- **THEN** the archive is not extracted
- **AND** the script exits with a non-zero code and an explanatory message

### Requirement: Robust failure handling in shell script
The shell download script SHALL fail fast: a failed download or extraction MUST cause a non-zero exit code rather than continuing and exiting 0.

#### Scenario: Download failure
- **WHEN** the archive download fails
- **THEN** the script exits with a non-zero code and does not attempt extraction

### Requirement: macOS-compatible download tooling
The download tooling SHALL work on macOS without requiring tools that are absent from a default install.

#### Scenario: macOS without wget
- **WHEN** the script runs on macOS where `wget` is not installed
- **THEN** the download still succeeds (e.g. via `curl`)

### Requirement: Builds do not require network when runtime is present
The MSBuild download target SHALL be skipped when a valid signal-cli runtime is already present, so repeated or offline builds do not require network access.

#### Scenario: Runtime already downloaded
- **WHEN** a valid signal-cli runtime already exists in the intermediate output
- **THEN** the build does not attempt a network download
