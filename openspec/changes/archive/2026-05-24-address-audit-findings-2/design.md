# Design — audit remediation (round 2)

## Cluster A — RPC robustness (F1, F4, F19)

**Request timeout.** Add `Config.RequestTimeoutSeconds` (default 30). In `JsonRpcClient.InvokeMethodAsync`, build a linked CTS from the caller token + a `CancelAfter` timeout token; on cancellation, fault the request TCS with a `TimeoutException` (not `OperationCanceledException`) when the caller token is not the trigger, so callers can distinguish a timeout from explicit cancel. Apply the same timeout to the startup `version` handshake in `JsonRpcClientHostedService` so a hung start cannot wedge the host.

**Reader-loop lifecycle.** Track per-`StreamPair` reader state: `CancellationTokenSource`, stdout `Task`, stderr `Task`. In `OnStreamPairChanged`, cancel the previous CTS, `await` both tasks to completion (with a short bounded timeout), then start new loops over the new pair. `Dispose` (now also `DisposeAsync`) does the same. Stop wrapping `pair.StandardOutput.BaseStream` in a new `using` `StreamReader` — read from the pair-provided reader directly. Mirror the stdout `catch when (!_disposed)` on the stderr loop.

**Cancellation propagation.** `tcs.TrySetCanceled(cancellationToken)` so the `OperationCanceledException` carries the token.

## Cluster B — Process restart supervision (F2, F3, F9, F25)

**Serialize crash-recovery.** Convert `OnProcessExited` to a thin `async void` wrapper that posts to a single-consumer queue or simply takes `_operationLock` before doing any state mutation/restart, mirroring `StopAsync`/`ForceRestartAsync`. Re-check `_disposed`/`_stopping` *inside* the lock. Wrap the body in `try/catch` so an exception cannot crash the host.

**Windowed restart budget.** Introduce `_restartWindow` (default 60 s). When the state machine transitions to `Running`, schedule a timer that — if the process is still `Running` after `_restartWindow` — resets `_restartCount` to 0 atomically under the lock. Cancel the timer on any non-`Running` transition. Both increment paths (`ForceRestartAsync`, `OnProcessExited`) read/write the count under the lock.

**Graceful stop.** Replace the fixed `Task.Delay(StopTimeoutSeconds)` with `await _currentProcess.WaitForExitAsync(linkedCtsWithTimeout)`; on timeout, log and `Kill(entireProcessTree: true)`. Wrap the stdin `exit` write in its own `try/catch` and fall through to wait/kill if it throws.

**State-manager dispose race.** Guard `UpdateState` with the same `System.Threading.Lock` as `Dispose`; `UpdateState` checks `_disposed` before calling `OnNext`.

## Cluster C — Logging privacy (F5)

Replace `string.Join(", ", response)` Information logs with count-only Information logs and per-item `Trace` logs that include the structured properties (still no raw record `ToString()`). Add a `VerifyLog` test that the facade does not log PII above `Trace`.

## Cluster D — Correctness (F8, F12, F13, F24)

**F8:** replace dead branch with `if (groupRecipients.Count > 0 && userRecipients.Count > 0) throw new ArgumentException("Cannot mix user and group recipients", nameof(recipients));` and a test.

**F12:** add `ArgumentNullException.ThrowIfNull(options)` to every public `SignalMessage.Send*Async` entry.

**F13:** add the missing observables (`Quotes`, `Edits`, `RemoteDeletes`) on `ISignalEventService` — additive, no break. Default-log at `Information` (no body) instead of "unknown".

**F24:** pass `nameof(quoteTimestamp)` as `paramName`; put the joined list only in the message.

## Cluster E — Config (F10, F11)

Mark `Config.AppHome`/`JavaExecutable`/`LibDirectory` as `required` and update `CreateDefault()` accordingly. Move `LogFileCli`/`StoragePathCli` defaults out of field initializers into `CreateDefault()` using `Path.Combine(AppHome, …)` so changing `AppHome` actually takes effect.

## Cluster F — Utilities (F16, F18)

**MimeTypeHelper:** parse the ISO-BMFF major brand at offset 8 (`isom`/`mp42` → mp4, `qt  ` → mov, `heic`/`heix` → heic, `M4A ` → m4a, `3gp4` → 3gp). Replace single `stream.Read` with `ReadAtLeast(8)` (loop until 8 bytes or EOF).

**TextStyleParser:** when a token closes without an opener, or an opener never closes, emit the literal marker char into output. Confirm offset units against `signal-cli` source (UTF-16 code units vs code points). Add unit tests for emoji content and for unclosed `*` / `~` / `` ` ``.

## Cluster G — Tests (F7, F14, F15)

**Integration project (F7).** New `Tests/SignalCli.Tests.Integration/Tests.csproj` (xUnit, `[Trait("Category","E2E")]`). Single dependency on the matching `SignalCli.Runtime.Jre.*` package (RID-gated via `<ItemGroup Condition="$(RuntimeIdentifier) == 'win-x64'">`). Fixture: `AddSignalCli` with `AppHome = AppContext.BaseDirectory`, `LibDirectory = "signal-cli/lib"`, leave `JavaExecutable` unset (auto-discovers the bundled JRE). Test: start the host, `WaitForReadyAsync`, call `ISignalCliClient.Version()`, assert non-empty. CI runs it on `windows-latest`/`macos-14` matrix jobs that restore the corresponding JRE package; `ubuntu-latest` runs the existing `SignalCli.Runtime.Native` path instead.

**De-flake (F14).** Replace `Task.Delay`/`Stopwatch`-based assertions in `SignalCliHostedService*` and `SignalCliHealthMonitor*` suites with `TaskCompletionSource`-based signaling (already proven in `StartStopTests`); for interval-driven tests, inject a fake clock/scheduler so test wall-clock time is irrelevant.

**Strengthen facade tests (F15).** For `SignalApiFacadeTests`, drop the pure "returns what mock was set up" assertions and keep tests that verify the captured RPC method name and parameters (the `Strict` mock approach used in some tests already).

## Cluster H — Docs and polish (F6, F17, F20, F21, F22, F23)

Straightforward edits per finding; see proposal.md.

## Verification

- `dotnet build SignalCli.sln -c Release` is warning-clean (`TreatWarningsAsErrors=true`).
- `dotnet test Tests/SignalCli.Tests/...` ≥ existing test count + the new tests listed under tasks (timeout, locked-exit, windowed-budget, mixed-recipient rejection, idempotent StartAsync).
- New `Tests/SignalCli.Tests.Integration/` E2E test passes locally and in CI.
- `openspec validate address-audit-findings-2 --strict` passes.
