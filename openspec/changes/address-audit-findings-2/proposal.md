## Why

The `comprehensive-code-audit` change identified 25 findings (verified against source and grounded in Microsoft Learn) — see `AUDIT-FINDINGS.md`. The High-severity items are real robustness/concurrency/privacy defects that can hang callers, race the process supervisor, and leak PII into logs; the Medium items contain a dead validation branch that lets malformed sends through. Fixing them in one change keeps the diff reviewable and pairs each fix with the requirement that justifies it.

## What Changes

Implements the audit findings, grouped by cluster (F-IDs match `AUDIT-FINDINGS.md`):

- **RPC robustness (F1, F4, F19)** — configurable per-request timeout that faults the TCS with `TimeoutException`; per-`StreamPair` `CancellationTokenSource` so reader loops can be cancelled and awaited on stream change / dispose; stop wrapping the process stream in a `using` `StreamReader`; add `try/catch` on the stderr loop; consider `IAsyncDisposable` for `JsonRpcClient`. Pass the cancellation token to `TrySetCanceled`.
- **Process restart supervision (F2, F3, F9, F25)** — `OnProcessExited` becomes a thin `async void` wrapper that posts work onto a serialized handler taking `_operationLock`, so crash-recovery races with intentional `StopAsync`/`ForceRestartAsync` are eliminated. `_restartCount` resets after the process is `Running` and stable for a debounce window (default 60 s) instead of being a lifetime counter. Replace the fixed `Task.Delay(StopTimeoutSeconds)` with `Process.WaitForExitAsync(timeout)`. Guard `ProcessStateManager.UpdateState`/`Dispose` so a late `OnNext` cannot hit a disposed `BehaviorSubject`.
- **Logging privacy (F5)** — `SignalAccounts.ListAccounts`/`SyncAccount` and `SignalGroups.ListGroups` log only counts at `Information`; record details only at `Trace`. Restores the project's own `logging-privacy` requirement that was violated in the facades.
- **Correctness (F8, F12, F13, F24)** — fix the unreachable recipient-validation branch so mixed user+group sends are rejected; add `ArgumentNullException.ThrowIfNull` on public `SignalMessage` method entries; either surface observables for quote/edit/remote-delete `DataMessage`s or document the limited surface (default: log at `Information` instead of "unknown" + document); pass a single `nameof` as `paramName` in the quote-validation throw.
- **Config (F10, F11)** — mark `AppHome`/`JavaExecutable`/`LibDirectory` `required` (or validate with an actionable message); compute `LogFileCli`/`StoragePathCli` defaults in `CreateDefault()` from `AppHome` via `Path.Combine` so changing `AppHome` actually takes effect.
- **Utilities (F16, F18)** — `MimeTypeHelper`: read the ISO-BMFF major brand at offset 8 to disambiguate MOV/HEIC/M4A/3GP from MP4; loop / `ReadAtLeast` on the stream overload. `TextStyleParser`: on an unclosed marker, emit the literal char (don't drop it); confirm offset units against `signal-cli`.
- **Tests (F7, F14, F15)** — **new** `Tests/SignalCli.Tests.Integration/` project (gated `[Trait("Category","E2E")]`) that depends on `SignalCli.Runtime.Jre.win-x64` / `.osx-arm64` and runs a real `version` round-trip with no system Java; de-flake timing-based health/lifecycle tests via `TaskCompletionSource` signaling and/or a fake clock; replace near-tautological facade passthrough tests with captured-parameter checks.
- **Docs (F6, F17, F20, F21, F22, F23)** — fix the non-compiling README event/attachment/reaction/quickstart snippets; make `SignalEventService.StartAsync` idempotent and `Dispose` the subjects; correct nullability on `JsonRpcResponse`/`JsonRpcError`/`JsonRpcNotification` (type `Data` as `JsonElement?`); remove the global `CS1591` suppression and document the gaps; normalize test comments to Ukrainian; fix `copilot-instructions.md` to "ASCII-only, no BOM"; `TryAdd*` in `AddSignalCli` to guard double-registration.

## Capabilities

### New Capabilities
- `rpc-robustness`: bounded request lifetime and a cancellable, ownership-correct reader-loop lifecycle for the JSON-RPC transport.
- `process-restart-supervision`: serialized crash-recovery and a windowed (not lifetime) restart budget.

### Modified Capabilities
<!-- The existing `logging-privacy` requirement (in change `address-audit-findings`) already covers the facade leaks (F5); we restore conformance via a task rather than altering the requirement. -->

## Impact

- Code: `Services/Rpc/JsonRpcClient.cs`, `Services/Rpc/JsonRpcClientHostedService.cs`, `Services/SignalCli/SignalCliHostedService.cs`, `Services/SignalCli/ProcessStateManager.cs`, `Services/Signal/SignalAccounts.cs`, `Services/Signal/SignalGroups.cs`, `Services/Signal/SignalMessage.cs`, `Services/Signal/SignalEventService.cs`, `Models/Config.cs`, `Models/Rpc/JsonRpcResponse.cs`, `Models/Rpc/JsonRpcNotification.cs`, `Utilities/MimeTypeHelper.cs`, `Utilities/TextStyleParser.cs`, `Extensions/ServiceCollectionExtensions.cs`.
- Tests: new `Tests/SignalCli.Tests.Integration/` project; refactor of timing-fragile suites; replacement of weak facade passthrough tests.
- Docs: `README.md`, `CLAUDE.md` (light), `.github/copilot-instructions.md`.
- Behavior:
  - Callers may now see `TimeoutException` from RPC calls when `signal-cli` is silent past the timeout (new public knob `Config.RequestTimeoutSeconds`, default 30 s).
  - Auto-restart no longer permanently disables itself after the lifetime budget is spent.
  - Information-level logs become quieter (no PII in facade logs).
  - No breaking signature changes; new public surface is additive.
