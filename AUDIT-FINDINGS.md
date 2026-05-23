# SignalCli.NET — Comprehensive Code Audit (findings)

Planned via OpenSpec change `comprehensive-code-audit`. Scope: 100% of `src/SignalCli/**` (~6 000 LOC) and `Tests/**` (~3 700 LOC). Every best-practice claim is grounded in official Microsoft Learn documentation (links per finding). High/Critical findings were verified by reading the cited source lines.

**Date:** 2026-05-22 · **Reviewed against:** .NET 10 / C# 14 · **Method:** see `openspec/changes/comprehensive-code-audit/design.md`.

> This audit makes **no production-code changes**. Each accepted finding is mirrored in `tasks.md` and is intended to become a task in a follow-up remediation change.

---

## Executive summary

The library is well-architected: a single-source-of-truth state machine, presence-based event fan-out, source-generated `System.Text.Json`, argument-list process launch (no shell injection), and — verified — correct `JsonDocument`/`JsonElement` lifetime, correct `TaskCompletionSource` continuation flags, clean async hygiene, and sanitized attachment paths. The defects that remain cluster in **three areas**:

1. **RPC robustness** — there is no request timeout, so a live-but-silent `signal-cli` can hang a caller forever; reader loops are uncancellable fire-and-forget tasks.
2. **Process crash-recovery concurrency** — the `Exited` handler is `async void` and restarts the process *outside* the operation lock, and the restart budget is a lifetime counter that permanently disables auto-restart after a few sporadic crashes.
3. **Privacy & docs** — `ListAccounts`/`ListGroups`/`SyncAccount` log phone numbers and group metadata at `Information` (violating the library's own privacy rule), several README snippets don't compile, and there is **no end-to-end test** against a real `signal-cli`.

No data-corruption or remote-exploit bug was found in the normal happy path.

---

## Findings

Severity: **Critical** (data loss/hang/crash/security) · **High** (leak/race/swallowed error/doc-wrong) · **Medium** (best-practice, limited blast radius) · **Low** (style/doc/micro-opt).

| ID | Sev | Location | Issue | Why (Microsoft Learn) | Recommendation |
|----|-----|----------|-------|-----------------------|----------------|
| F1 | **High** | `Services/Rpc/JsonRpcClient.cs:213-238`; startup ping `Services/Rpc/JsonRpcClientHostedService.cs` | **No request timeout.** `InvokeMethodAsync` awaits `tcs.Task` with only `cancellationToken.Register`. If `signal-cli` is alive but never answers (or a response id never matches) and the caller passed `default` (the startup `version` ping does), the call hangs forever. | "[Complete your tasks](https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/complete-your-tasks)" — *"Add timeout-based tests so hangs fail fast"*; "[Common async bugs](https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/common-async-bugs)". | Wrap the wait in a linked CTS with a configurable `CancelAfter`; fault the TCS with `TimeoutException`. Add a timeout test. |
| F2 | **High** | `Services/SignalCli/SignalCliHostedService.cs:343-380` | **`async void` `OnProcessExited` restarts outside the lock.** It calls `CleanupProcess()` and `StartProcessInternalAsyncNoLock(...)` with no `_operationLock`, racing `StopAsync`/`ForceRestartAsync` (which hold it). Guarded only by `_disposed`/`_stopping` bools → a crash that coincides with an intentional stop can double-clean / restart-after-stop / orphan a process. `async void` also turns an escaped exception (pre-`try`, lines 350-352) into a process crash. | "[Common async bugs](https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/common-async-bugs)"; "[async void](https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/synchronizationcontext-console-apps#handle-async-void-methods)". | Acquire `_operationLock` (or serialize restarts through a single-consumer queue); re-check `_disposed`/`_stopping` inside the lock; keep `async void` as a thin `try/catch` wrapper over a `Task`-returning method. |
| F3 | **High** | `Services/SignalCli/SignalCliHostedService.cs:181, 226-229, 360` | **Restart budget is a lifetime counter.** `_restartCount` resets only on a *manual* start, but increments on every auto/force restart. After `MaxRestartAttempts` (default 3) *sporadic* crashes over the whole lifetime, auto-restart is disabled permanently — even though each crash was individually recoverable. Two increment paths (181, 360) also race with no shared lock. | Resilience guidance — transient-fault handling should use a *windowed* budget, not a monotonic lifetime count. | Reset `_restartCount` to 0 once the process reaches `Running` and stays up for a debounce window; guard all access under the lock. |
| F4 | **High** | `Services/Rpc/JsonRpcClient.cs:80-112` | **Reader loops are uncancellable fire-and-forget.** No `CancellationToken`; `_disposed` is only checked *after* `ReadLineAsync` returns. The stderr loop (103-111) has **no `try/catch`** → unobserved task exception on shutdown. The stdout loop wraps `pair.StandardOutput.BaseStream` in a `new StreamReader(...)` inside a `using` (86) → disposing it disposes the process stream that `StreamPair.Dispose()` also disposes (double-dispose). | "[Implementing Dispose](https://learn.microsoft.com/dotnet/standard/garbage-collection/implementing-dispose)"; "[TPL exception handling / unobserved exceptions](https://learn.microsoft.com/dotnet/standard/parallel-programming/exception-handling-task-parallel-library)". | Track a per-pair `CancellationTokenSource`; cancel + await the prior loops in `OnStreamPairChanged`/`Dispose`. Read from `pair.StandardOutput` directly (don't wrap-and-dispose). Mirror the stdout `catch when (!_disposed)` on stderr. Consider `IAsyncDisposable`. |
| F5 | **High** | `Services/Signal/SignalAccounts.cs:~35,~68`; `Services/Signal/SignalGroups.cs:~35` | **PII logged at `Information`.** `ListAccounts`/`SyncAccount`/`ListGroups` log `string.Join(", ", response)`; the `Account`/`Group`/`Member` records' `ToString()` emit phone **Numbers**, UUIDs, and group names. This violates the project's own `logging-privacy` requirement (CLAUDE.md rule #1). | The repo's own `openspec/.../logging-privacy/spec.md`; general PII-in-logs guidance. | Log only `response.Count` at `Information`; emit details only at `Trace`. |
| F6 | **High** (docs) | `README.md:~184,509,543,547,549,564,565` | **README examples don't compile.** `AttachmentEventArgs` has no `DataMessage` and `JsonAttachment` has no `.Data`; `ReactionEventArgs` exposes `Reaction` directly; `FinishLinkResponse` exposes `number`, not `DeviceId`. | Documentation accuracy. | Fix snippets to the real API; note that attachment bytes need a separate fetch. |
| F7 | **High** (tests) | `Tests/.../SignalCliHostedService/SignalCliHostedServiceIntegrationTests.cs` | **No real end-to-end test exists**; this "integration" test is fully mocked and misnamed. Real process spawn, stdin/stdout JSON-RPC framing, ArgumentList parsing, and JRE bootstrap are never exercised. | Testing guidance (integration vs unit). | Add a bundled-JRE E2E test (see *Integration-test plan* below). Rename the mocked file to `*StateTests`. |
| F8 | Medium | `Services/Signal/SignalMessage.cs:80-89` | **Dead recipient validation.** `if (groupRecipients.Count > 1 && userRecipients.Count > 1)` (86) is unreachable — line 80 already throws when `groupRecipients.Count > 1`. So a mixed *1 group + N users* set passes validation and is sent as a malformed `send`. | Correctness. | Replace with: reject when `groupRecipients.Count > 0 && userRecipients.Count > 0`. |
| F9 | Medium | `Services/SignalCli/SignalCliHostedService.cs:289` | **Graceful stop always waits the full `StopTimeoutSeconds`** via `Task.Delay`, even when the process exits in milliseconds → every shutdown is slow. | `Process.WaitForExitAsync` is the intended primitive. | `await proc.WaitForExitAsync(linkedCtsWithTimeout)`; wrap the stdin `exit` write in its own `try/catch` and fall through to `Kill`. |
| F10 | Medium | `Models/Config.cs:68,88,103` | **Required paths nullable-by-omission.** `AppHome`/`JavaExecutable`/`LibDirectory` are non-nullable `string` with no initializer; `new Config()` (not via `CreateDefault`) leaves them null → opaque NRE / `Path.Combine(null,…)`. | "[Nullable reference types](https://learn.microsoft.com/dotnet/csharp/nullable-references)". | Mark `required` or validate in `ToProcessConfig` with an actionable message. |
| F11 | Medium | `Models/Config.cs:78,83` | `LogFileCli`/`StoragePathCli` defaults use `AppDomain.CurrentDomain.BaseDirectory` (ignoring `AppHome`) and `"/"` string concat. Changing `AppHome` silently has no effect. | `Path.Combine` guidance. | Compute in `CreateDefault()` from `AppHome` via `Path.Combine`. |
| F12 | Medium | `Services/Signal/SignalMessage.cs` public methods | No `ArgumentNullException.ThrowIfNull(options)` → `NullReferenceException` instead of the documented `ArgumentNullException`. | "[ArgumentNullException.ThrowIfNull](https://learn.microsoft.com/dotnet/api/system.argumentnullexception.throwifnull)". | Add `ArgumentNullException.ThrowIfNull(options)` at method entry. |
| F13 | Medium | `Services/Signal/SignalEventService.cs:~223` | A `DataMessage` carrying **only** a quote / edit / remote-delete (no body/reaction/sticker/attachment) is logged as "unknown" and **dropped** — consumers never see it. | Correctness/coverage. | Add observables for those payloads or document the limited surface; don't classify as "unknown". |
| F14 | Medium | `Tests/.../SignalCliHealthMonitor/*`, `Tests/.../SignalCliHostedService/*` | **Timing-fragile tests** (`Task.Delay(100..3500)` + `Stopwatch` window asserts) → CI flakiness. | Reliable-test guidance. | Replace sleeps with `TaskCompletionSource` signaling (the `StartStop` tests already model this); inject a fake clock/scheduler for interval tests. |
| F15 | Medium | `Tests/.../SignalApiFacadeTests.cs` | Several facade tests assert a mock returns its own setup value (near-tautological). | Test-value guidance. | Keep one null-guard per facade; replace passthrough asserts with captured-parameter checks (as the `ListGroups`/`FinishLink` tests already do). |
| F16 | Medium | `Utilities/MimeTypeHelper.cs:~81, ~114` | `"ftyp"` ISO-BMFF check labels MOV/HEIC/M4A/3GP as `video/mp4`; the `Stream` overload's single `Read` may under-read (<8 bytes) on pipe/network streams. | Stream read-contract (`Stream.Read` may return fewer bytes). | Read the major brand at offset 8 to disambiguate; loop/`ReadAtLeast` until 8 bytes or EOF. |
| F17 | Medium | `Services/Signal/SignalEventService.cs:~338-362` | `StartAsync` is not idempotent — a second start overwrites `_notificationSubscription`, leaking the prior subscription; Subjects are `OnCompleted()` but not `Dispose()`d. | Rx disposal hygiene. | Guard double-start (dispose existing first); `Dispose()` subjects after `OnCompleted()` (or `CompositeDisposable`). |
| F18 | Low | `Utilities/TextStyleParser.cs:55-108` | Unmatched/unclosed markers are silently consumed (the marker char is dropped, not echoed). Offsets are UTF-16 code units — verify Signal expects that, not code points (emoji/surrogates would misalign). | Globalization / text-element guidance. | On unclosed token, emit the literal char; confirm offset units against signal-cli. |
| F19 | Low | `Services/Rpc/JsonRpcClient.cs:220` | `tcs.TrySetCanceled()` doesn't pass the token, so the `OperationCanceledException` carries no `CancellationToken`. | Cancellation guidance. | `TrySetCanceled(cancellationToken)` or `tcs.Task.WaitAsync(cancellationToken)`. |
| F20 | Low | `Models/Rpc/JsonRpcResponse.cs`, `JsonRpcNotification.cs` | Non-nullable members (`Error`, `JsonRpc`, `Id`) are legitimately null at runtime; `JsonRpcNotification.Data` is `object` (forces reflection-fallback serialization) rather than `JsonElement`. | Nullable + STJ source-gen guidance. | Mark optional members nullable / `required`; type `Data` as `JsonElement?`. |
| F21 | Low | `src/SignalCli/SignalCli.csproj:14` | `<NoWarn>$(NoWarn);1591</NoWarn>` globally suppresses missing-XML-doc warnings despite `GenerateDocumentationFile=true`, hiding the few undocumented public members (`SignalAccounts.*`, `SignalDevices.*`, `SignalGroups.ListGroups`, `SignalEventService.Start/StopAsync`). | "[CS1591 / GenerateDocumentationFile](https://learn.microsoft.com/dotnet/csharp/language-reference/compiler-messages/cs1591)". | Remove the suppression and document the gaps, or audit periodically. |
| F22 | Low | `Tests/**` comments; `.github/copilot-instructions.md:~13` | Test comments are **Russian** while CLAUDE.md mandates **Ukrainian** for the codebase; `copilot-instructions` still claims the download scripts use "UTF-8 BOM" (now ASCII-only per CLAUDE.md rule #7). | Convention consistency. | Normalize test comments; fix copilot-instructions to "ASCII-only, no BOM". |
| F23 | Low | `Extensions/ServiceCollectionExtensions.cs:27-62` | `AddSignalCli` isn't guarded against double-registration (two calls run two hosted-service sets); `configure` is non-nullable but called with `?.`. No captive-dependency issue (all singletons). | "[DI guidelines](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection-guidelines)". | `TryAdd*` / guard re-registration; make the param nullable or drop the `?.`. |
| F24 | Low | `Services/Signal/SignalMessage.cs:97` | The quote-validation `ArgumentException` passes a comma-joined list as `paramName` (must be a single name). | `ArgumentException.ParamName` contract. | Pass `nameof(quoteTimestamp)`; put the list in the message only. |
| F25 | Low | `Services/SignalCli/ProcessStateManager.cs:68-97` | `UpdateState` calls `_stateSubject.OnNext` outside the lock and `Dispose()` isn't synchronized → a state update racing dispose can hit a disposed `BehaviorSubject`. | Rx/IDisposable race. | Guard `Dispose`/`UpdateState` with the same `Lock`; check `_disposed` before `OnNext`. |

---

## Verified non-issues (checked and correct — no action)

These were specifically examined and found to follow Microsoft guidance:

- **`JsonDocument`/`JsonElement` lifetime** — `ProcessMessage` disposes its `JsonDocument` (`using`), and STJ **clones** `JsonElement` members (`Result`, `Params`) on `Deserialize`, so they remain valid afterward. No use-after-dispose.
- **`TaskCompletionSource` continuations** — created with `RunContinuationsAsynchronously` (`JsonRpcClient.cs:213`), per "[async coordination primitives](https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/async-coordination-primitives)".
- **Composite event dispatch** — text + attachment + reaction all fire (no early `return`).
- **Observable encapsulation** — exposed via `.AsObservable()` (consumers can't push).
- **Attachment temp-file cleanup** — deleted in `finally` on success **and** failure (and tested).
- **Path traversal** — `Path.GetFileName` sanitizes the temp path and the data-URI `filename=`.
- **Process arguments** — built via `ProcessStartInfo.ArgumentList` (no shell injection).
- **Redirected-stream deadlock** — stdout and stderr are read on **separate** tasks, so the classic [`Process` redirect deadlock](https://learn.microsoft.com/dotnet/api/system.diagnostics.process.standardoutput) is avoided.
- **Async hygiene** — no `.Result`/`.Wait()`/`.GetAwaiter().GetResult()`; `ConfigureAwait(false)` used consistently in library code.

---

## Test quality

- **~152 executable cases** (~132 `[Fact]` + `[Theory]` rows). Estimated **~110-120 real-value**; **~25-35 weak** (facade passthroughs + timing-fragile lifecycle/health tests).
- **Genuinely valuable**: serialization round-trips, `TextStyleParser`, `MimeTypeHelper`, `AttachmentEntry` traversal, `SignalMessage` temp-file-cleanup-on-failure, `Config` arg-building, and most state-machine tests.
- **Gaps** (no/weak coverage): RPC request timeout & unknown-id response (F1), reader-loop lifetime on reconnect (F4), composite dispatch for sticker / 3-payload (F13-adjacent), ping-timeout token propagation (a `// TODO` admits it).

### Integration-test gap & plan
There is **no test that launches a real `signal-cli`**. Proposed: a separate `Tests/SignalCli.Tests.Integration` project, `[Trait("Category","E2E")]`, referencing `SignalCli.Runtime.Jre.win-x64` / `.osx-arm64`. A fixture sets `AppHome = AppContext.BaseDirectory`, `LibDirectory = "signal-cli/lib"`, leaves `JavaExecutable` unset so `Config.ResolveBundledJava` finds the bundled `jre/bin/java[.exe]`, starts the host, `await WaitForReadyAsync()`, and calls `ISignalCliClient.Version()` against the real spawned JVM — asserting a non-empty version. This exercises real process spawn + `ArgumentList` + JSON-RPC framing + JRE bootstrap with **no account, no network, and no system Java** (CI-friendly), gated by a RID check. *(This path was validated manually during the bundled-JRE work; it should become an automated test.)*

---

## Documentation quality

- **XML docs**: thorough on most public members (in Ukrainian). Gaps on the `SignalAccounts`/`SignalDevices`/`SignalGroups` methods and `SignalEventService.Start/StopAsync`; `CS1591` is globally suppressed (F21).
- **README** (F6): install/platform sections are accurate after recent edits, but the **event/attachment/reaction/quickstart code snippets do not compile**.
- **CLAUDE.md**: accurate after this session's fixes. **copilot-instructions.md** still claims UTF-8-BOM scripts (F22).
- **Language consistency** (F22): `src/` comments are Ukrainian; many test comments are Russian.

---

## Source coverage

All non-generated `.cs` under `src/SignalCli/**` were assigned to a subsystem (S1-S7) and reviewed in full. Files with no finding (reviewed-clean) include the event-arg DTOs, `ProcessState`/`ProcessConfig`, recipient records, `StreamPair` (minor F-class only), and the interface set. No production file was skipped.

---

## Recommended remediation order (feeds a follow-up change)

1. **F1** request timeout, **F4** reader-loop lifecycle — the two robustness/hang risks.
2. **F2 + F3** crash-recovery concurrency (lock the `Exited` path; window the restart budget).
3. **F5** PII logging (privacy regression).
4. **F7** bundled-JRE E2E test; **F6** README snippets.
5. **F8-F17** correctness/medium items.
6. **F18-F25** low/polish.
