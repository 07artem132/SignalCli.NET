## ADDED Requirements

### Requirement: Java resolution on all supported platforms
`Config.CreateDefault()` SHALL resolve a Java executable on Windows, Linux, and macOS. On non-Windows platforms the resolver MUST check `JAVA_HOME/bin/java` first and fall back to `java` resolved via `PATH`.

#### Scenario: Linux with JAVA_HOME set
- **WHEN** running on Linux with `JAVA_HOME` pointing at a valid JDK
- **THEN** the resolved executable is `$JAVA_HOME/bin/java`

#### Scenario: Linux without JAVA_HOME
- **WHEN** running on Linux with no `JAVA_HOME` but `java` available on `PATH`
- **THEN** the resolved executable is `java`

#### Scenario: No Java available
- **WHEN** no Java can be located on the current platform
- **THEN** a clear, actionable exception is thrown naming the platform and the variables checked

### Requirement: Documentation matches platform support
The documented platform support SHALL match actual behavior. If a platform is not supported, the README MUST NOT claim it is.

#### Scenario: README review
- **WHEN** the cross-platform startup requirement is satisfied for a platform
- **THEN** the README lists that platform as supported, otherwise it does not
