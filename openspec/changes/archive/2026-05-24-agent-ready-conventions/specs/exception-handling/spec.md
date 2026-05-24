## ADDED Requirements

### Requirement: Catch specific exception types
Code SHALL catch specific exception types where the failure mode is known (e.g. `JsonException` for parse, `IOException` for file IO, `InvalidOperationException`/`Win32Exception` for process start), rather than a broad `catch (Exception)`.

#### Scenario: Known failure narrowed
- **WHEN** a block can only fail in a known way (e.g. JSON parsing)
- **THEN** it catches the specific exception type, not `System.Exception`

### Requirement: Documented broad catches at loop boundaries
A broad `catch (Exception)` SHALL be permitted only at long-running boundaries where one failing item must not terminate the loop, and each such catch SHALL log the error and continue and carry a comment stating the intent.

#### Scenario: Reader loop survives a bad line
- **WHEN** the stdout reader, health-monitor loop, or notification dispatcher encounters an unexpected error on one item
- **THEN** it logs and continues processing subsequent items (broad catch is intentional and commented)

#### Scenario: No silent swallowing
- **WHEN** an exception is caught
- **THEN** it is either rethrown, handled meaningfully, or logged — never silently discarded
