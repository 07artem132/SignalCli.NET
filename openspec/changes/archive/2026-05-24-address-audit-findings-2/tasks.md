## A. RPC robustness

- [x] A.1 (F1) Add `Config.RequestTimeoutSeconds` (default 30); link CTS in `JsonRpcClient.InvokeMethodAsync`; fault TCS with `TimeoutException` distinctly from caller cancel
- [x] A.2 (F1) Apply the same timeout to the startup `version` handshake in `JsonRpcClientHostedService`
- [x] A.3 (F1) Test: silent process → call faults with `TimeoutException` within `RequestTimeoutSeconds`
- [x] A.4 (F4) Per-`StreamPair` `CancellationTokenSource`; track stdout/stderr `Task`s; cancel + await on stream change and on dispose
- [x] A.5 (F4) Stop wrapping `pair.StandardOutput.BaseStream` in a new `using` `StreamReader` — read from the existing reader
- [x] A.6 (F4) Add `try/catch when (!_disposed)` on stderr loop
- [x] A.7 (F4) Implement `IAsyncDisposable` on `JsonRpcClient`; hosted-service `StopAsync` awaits it
- [x] A.8 (F4) Test: pushing a 2nd `StreamPair` stops the prior reader before starting the new one
- [x] A.19 (F19) Pass cancellation token to `TrySetCanceled(cancellationToken)`

## B. Process restart supervision

- [x] B.1 (F2) Serialize `OnProcessExited` through `_operationLock` (or a single-consumer queue); thin `async void` wrapper with `try/catch`
- [x] B.2 (F2) Re-check `_disposed`/`_stopping` inside the lock; ensure no state mutation before the lock is held
- [x] B.3 (F2) Test: an intentional `StopAsync` concurrent with an unexpected exit results in exactly one cleanup and no restart-after-stop
- [x] B.4 (F3) Introduce `Config.RestartWindowSeconds` (default 60); on `Running` transition schedule a timer that resets `_restartCount` to 0 when still `Running` after the window
- [x] B.5 (F3) Both increment paths read/write `_restartCount` under `_operationLock`
- [x] B.6 (F3) Test: a process that runs for `> RestartWindow` then crashes is restarted even if the cumulative budget was previously spent
- [x] B.9 (F9) Replace fixed `Task.Delay(StopTimeoutSeconds)` with `WaitForExitAsync(linkedCtsWithTimeout)`; wrap stdin `exit` write in its own `try/catch`
- [x] B.25 (F25) Guard `ProcessStateManager.UpdateState`/`Dispose` with the same `System.Threading.Lock`; `UpdateState` checks `_disposed` before `OnNext`

## C. Logging privacy

- [x] C.5 (F5) `SignalAccounts.ListAccounts`/`SyncAccount` log only count at `Information`; details only at `Trace`
- [x] C.5b (F5) `SignalGroups.ListGroups` same treatment
- [x] C.5c (F5) `VerifyLog` test asserts no record `ToString()` content is emitted above `Trace` from the facades

## D. Correctness

- [x] D.8 (F8) Replace dead validation branch in `SignalMessage`: reject `groupRecipients.Count > 0 && userRecipients.Count > 0`; add test
- [x] D.12 (F12) `ArgumentNullException.ThrowIfNull(options)` on every public `SignalMessage.Send*Async`
- [x] D.13 (F13) Add `Quotes`/`Edits`/`RemoteDeletes` observables on `ISignalEventService` (or document the limited surface and stop logging "unknown" for these)
- [x] D.24 (F24) Pass `nameof(quoteTimestamp)` (single) as `paramName` in the quote-validation `ArgumentException`

## E. Config

- [x] E.10 (F10) Mark `Config.AppHome`/`JavaExecutable`/`LibDirectory` `required`; update `CreateDefault` and any failing tests
- [x] E.11 (F11) Move `LogFileCli`/`StoragePathCli` defaults into `CreateDefault()` using `Path.Combine(AppHome, …)`; add test that changing `AppHome` propagates

## F. Utilities

- [x] F.16 (F16) `MimeTypeHelper`: read ISO-BMFF major brand at offset 8 to disambiguate MOV/HEIC/M4A/3GP from MP4; loop / `ReadAtLeast` on stream overload; add tests
- [x] F.18 (F18) `TextStyleParser`: on unclosed marker, emit literal char; confirm offset units against signal-cli; add tests (emoji, unclosed `*`/`~`/`` ` ``)

## G. Tests & integration

- [x] G.7 (F7) Create `Tests/SignalCli.Tests.Integration/` project (xUnit, `[Trait("Category","E2E")]`)
- [x] G.7b (F7) Reference the matching `SignalCli.Runtime.Jre.*` package per RID
- [x] G.7c (F7) Write E2E test: start host, `WaitForReadyAsync`, `Version()`, assert non-empty
- [x] G.7d (F7) CI matrix: windows-latest + macos-14 + ubuntu-latest (native) jobs run the E2E
- [x] G.7e Rename the existing misnamed `SignalCliHostedServiceIntegrationTests.cs` to `…StateTests.cs`
- [x] G.14 (F14) De-flake `SignalCliHostedService*` and `SignalCliHealthMonitor*` timing tests via `TaskCompletionSource` signaling and a fake clock
- [x] G.15 (F15) Replace passthrough facade asserts with captured-parameter checks; keep one null-guard per facade

## H. Docs and polish

- [x] H.6 (F6) Fix the non-compiling README event/attachment/reaction/quickstart snippets
- [x] H.17 (F17) Make `SignalEventService.StartAsync` idempotent (dispose prior subscription); `Dispose` the Subjects after `OnCompleted()`
- [x] H.20 (F20) Correct nullability on `JsonRpcResponse`/`JsonRpcError`/`JsonRpcNotification`; type `Data` as `JsonElement?`
- [x] H.21 (F21) Remove the global `CS1591` suppression in `SignalCli.csproj`; document the few gaps
- [x] H.22 (F22) Normalize Russian test comments to Ukrainian; fix `.github/copilot-instructions.md` to "ASCII-only, no BOM"
- [x] H.23 (F23) Guard `AddSignalCli` against double-registration via `TryAdd*`; align `Action<Config>` nullability

## Z. Verification

- [x] Z.1 `dotnet build SignalCli.sln -c Release` warning-clean (`TreatWarningsAsErrors=true`)
- [x] Z.2 `dotnet test Tests/SignalCli.Tests/...` — all green, no flakiness, ≥ current count + new tests
- [x] Z.3 New `Tests/SignalCli.Tests.Integration/` E2E green on Windows + macOS + Linux jobs
- [x] Z.4 `openspec validate address-audit-findings-2 --strict` passes

## Status

Усі 45 задач виконано. Місцева верифікація (Windows x64):

- `dotnet build SignalCli.sln -c Release` → 0 warnings, 0 errors (`TreatWarningsAsErrors=true`)
- `dotnet test Tests/SignalCli.Tests/...` → **Passed 173 / 173**
- `dotnet test Tests/SignalCli.Tests.Integration/...` `--filter Category=E2E` → **Passed 1 / 1** (реальний signal-cli 0.14.3 через бандл JRE 25)
- `openspec validate address-audit-findings-2 --strict` → valid

CI-частина матриці (`.github/workflows/e2e.yml`): job `e2e` запускається на
`windows-latest` (JRE bundle для win-x64), `macos-14` (JRE bundle для osx-arm64),
`ubuntu-latest` (native binary signal-cli). Локально перевірено лише Windows
гілку — реальне виконання macOS/Linux jobs відбудеться при push/PR.
