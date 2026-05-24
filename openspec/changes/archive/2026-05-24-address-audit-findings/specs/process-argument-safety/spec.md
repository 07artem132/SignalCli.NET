## ADDED Requirements

### Requirement: Safe construction of signal-cli arguments
The library SHALL construct the signal-cli process arguments such that quote, space, or separator characters in configured paths (storage path, log file, classpath entries) cannot break the argument boundaries or inject additional arguments.

#### Scenario: Path containing a quote character
- **WHEN** a configured path such as `StoragePathCli` contains a double-quote character
- **THEN** the argument is either correctly escaped/quoted or the configuration is rejected with a clear error
- **AND** no additional or malformed arguments reach the JVM

#### Scenario: Path containing spaces
- **WHEN** a configured path contains spaces
- **THEN** signal-cli receives the path as a single argument

### Requirement: Argument construction is testable
The mapping from `Config` to the process argument vector SHALL be unit-testable without launching a process.

#### Scenario: Inspecting produced arguments
- **WHEN** `Config.ToProcessConfig()` is invoked with known paths
- **THEN** the produced arguments can be asserted in a unit test
