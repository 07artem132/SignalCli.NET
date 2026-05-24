## ADDED Requirements

### Requirement: Graceful shutdown SHALL close stdin, not write literal "exit"

`SignalCliHostedService.StopProcessInternalAsyncNoLock` SHALL close the stdin stream of the signal-cli child process (`pair.StandardInput.Close()`) to trigger graceful shutdown. The wrapper SHALL NOT write the literal text `"exit"` to stdin — signal-cli has no JSON-RPC `exit` method, does not read stdin for literal commands, and treats `"exit"` as a malformed JSON-RPC request (responds with `-32700 Parse error` and continues running).

The wait-for-exit timeout fallback (`WaitForExitAsync` + `StopTimeoutSeconds`) and the hard-kill fallback (`Process.Kill(entireProcessTree: true)`) remain unchanged. After this requirement, the hard-kill fallback fires only when signal-cli is genuinely hung — not on every shutdown as before.

#### Scenario: Stop closes stdin and does not emit "exit" bytes
- **GIVEN** a mocked signal-cli process whose stdin is a `StreamWriter` over a `MemoryStream`
- **WHEN** `SignalCliHostedService.StopAsync` runs
- **THEN** the `MemoryStream` content contains no byte sequence matching the ASCII bytes for "exit"
- **AND** the stream/writer is closed

#### Scenario: Real signal-cli exits within StopTimeoutSeconds via stdin EOF
- **GIVEN** the bundled signal-cli runtime is present (Win/macOS JRE or Linux native)
- **AND** `StopTimeoutSeconds = 2`
- **WHEN** `await hostedService.StopAsync(CancellationToken.None)` is called on a `Running` host
- **THEN** within 4 wall-clock seconds the OS process has exited (`Process.HasExited == true`)
- **AND** the captured logger recorded NO `ProcessKillTimeout` entry

### Requirement: `ExitWriteFailed` log entry SHALL NOT exist

The `[LoggerMessage]`-generated method `SignalCliHostedServiceLog.ExitWriteFailed` SHALL be deleted. The new diagnostic surface for the stdin-close path is `SignalCliHostedServiceLog.StdinCloseFailed` at `Debug` level, fired only on `IOException` / `ObjectDisposedException` during `Close()`.

#### Scenario: No reference to ExitWriteFailed remains in the codebase
- **WHEN** `grep -rn 'ExitWriteFailed' src/ Tests/` runs after the change
- **THEN** the output is empty
