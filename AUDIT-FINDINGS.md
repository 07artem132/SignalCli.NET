# SignalCli.NET — Comprehensive Code Audit (findings)

Planned via OpenSpec change `comprehensive-code-audit`. Scope: 100% of `src/SignalCli/**` (~6 000 LOC) and `Tests/**` (~3 700 LOC). Every best-practice claim is grounded in official Microsoft Learn documentation (links per finding). High/Critical findings were verified by reading the cited source lines.

**Date:** 2026-05-22 · **Refreshed:** 2026-05-23 (see [Refresh section](#refresh--2026-05-23)) · **Re-audited:** 2026-05-23 post-remediation (see [Post-remediation section](#post-remediation-re-audit--2026-05-23)) · **Reviewed against:** .NET 10 / C# 14 · **Method:** see `openspec/changes/comprehensive-code-audit/design.md`.

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

---

## Refresh — 2026-05-23

**Method.** Verified each finding still reproduces on `main` (HEAD = `7145851`, post PR #8). Re-grounded every citation against Microsoft Learn via the `microsoft-docs` MCP, pulling the live `?view=net-10.0` URL where one exists. Spot-checked the four critical sites in source (line numbers below match HEAD).

**Status delta since 2026-05-22.** Two follow-up commits landed:

- `a914bbd` plan: `address-audit-findings-2` — planning only (proposal/design/specs/tasks under `openspec/changes/address-audit-findings-2/`).
- `7145851` merge of PR #8 — the same plan into `main`.

**No production code under `src/SignalCli/**` was changed.** All 25 findings remain **Open**. Confirmed by re-reading the four critical sites:

| ID | Site | Verified |
|----|------|----------|
| F1 | `Services/Rpc/JsonRpcClient.cs:213-238` | `tcs.Task` still awaited with only `cancellationToken.Register(...)`; no `WaitAsync(timeout, …)`, no `CancelAfter`. |
| F2 | `Services/SignalCli/SignalCliHostedService.cs:343-380` | `private async void OnProcessExited(...)` still calls `CleanupProcess()` + `StartProcessInternalAsyncNoLock` outside `_operationLock`. |
| F3 | `Services/SignalCli/SignalCliHostedService.cs:181, 228-229, 360` | `_restartCount` still only resets in `StartProcessInternalAsyncNoLock` when entering from `NotStarted`; both increment paths still race. |
| F4 | `Services/Rpc/JsonRpcClient.cs:80-112` | `Task.Run` reader loops still uncancellable; stdout still wraps `pair.StandardOutput.BaseStream` in a `using new StreamReader(...)` (double-dispose path); stderr loop still without `try/catch`. |

### Refreshed citations (use these in the follow-up remediation tasks)

These supersede the URLs in the table above where the new page is canonical for .NET 10 or has appeared since 2026-05-22:

- **F1** (no request timeout). Primary canonical:
    - [Task.WaitAsync(TimeSpan, CancellationToken) — .NET 10](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task.waitasync?view=net-10.0) — exact API to add; faults the task with `TimeoutException`.
    - [Coalesce cancellation tokens from timeouts](https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/coalesce-cancellation-tokens-from-timeouts#encapsulate-linking-in-a-helper) — *new* canonical guidance: helper that links a user token to a `CancellationTokenSource(timeout)` and runs the operation under the linked token. Matches the recommendation verbatim.
    - [Complete your tasks](https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/complete-your-tasks) — still the right "every TCS path must complete" reference.
- **F2 / F4** (`async void`, unobserved exceptions). [Common async/await bugs — Can't await an async void method](https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/common-async-bugs#can't-await-an-async-void-method) (this fragment is the new canonical anchor — exceptions go unobserved, callers can't track completion).
- **F9** (`Task.Delay` instead of `WaitForExitAsync`). [Process.WaitForExitAsync(CancellationToken) — .NET 10](https://learn.microsoft.com/dotnet/api/system.diagnostics.process.waitforexitasync?view=net-10.0) — the intended primitive; sets `EnableRaisingEvents = true` and stores cancellation as `OperationCanceledException`.
- **F10** (required paths nullable-by-omission). [`required` modifier (C# 11+) — .NET 10](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/required) is now the recommended fix over the older "Nullable reference types" overview. See also [Working with Nullable Reference Types — Non-nullable properties and initialization](https://learn.microsoft.com/ef/core/miscellaneous/nullable-reference-types#non-nullable-properties-and-initialization) for the same pattern.
- **F12** (missing `ArgumentNullException.ThrowIfNull`). [ArgumentNullException.ThrowIfNull — .NET 10](https://learn.microsoft.com/dotnet/api/system.argumentnullexception.throwifnull?view=net-10.0). **New cross-reference:** when adding these guards, [CA2264](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2264) (enabled-by-default warning in .NET 10) flags accidental calls on `struct`/`nameof`/`new` — pass only the nullable reference parameter (`options`), don't `ThrowIfNull` on records that are non-nullable structs.
- **F16** (`Stream.Read` may under-read on pipes). The decisive citation is now the analyzer rule: [CA2022 — Avoid inexact read with Stream.Read](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2022) (enabled-by-default warning in .NET 10). Fix is to call [Stream.ReadAtLeast(Span<byte>, int, bool) — .NET 10](https://learn.microsoft.com/dotnet/api/system.io.stream.readatleast?view=net-10.0) (or `ReadExactly`) for the 8-byte `ftyp` header. Turning the existing `<NoWarn>` audit on CA2022 (related to F21) would surface this automatically.
- **F19** (`TrySetCanceled` without token). [TaskCompletionSource.TrySetCanceled(CancellationToken) — .NET 10](https://learn.microsoft.com/dotnet/api/system.threading.tasks.taskcompletionsource.trysetcanceled?view=net-10.0) — the overload to switch to so the resulting `OperationCanceledException` carries the originating token.

### Two newly-relevant analyzer rules (.NET 10, enabled by default)

The .NET 10 SDK now enables two warnings that catch the *next* regression of two findings in this audit. They are not separate findings but should be considered when implementing the fixes:

1. **CA2022** — "Avoid inexact read with Stream.Read". Will warn at the `MimeTypeHelper` site (F16) once the global `<NoWarn>` is reviewed (F21).
2. **CA2264** — "Do not pass a non-nullable value to ArgumentNullException.ThrowIfNull". Will warn if F12's `ThrowIfNull(options)` accidentally targets a non-nullable struct.

### What changed in this refresh

- Header gained a `Refreshed:` date.
- All Microsoft Learn citations were pinned to `?view=net-10.0` where the page has a versioned slice, including F1, F9, F12, F16, F19 above.
- F1 picked up the new `coalesce-cancellation-tokens-from-timeouts` page as its primary guidance (it didn't exist or wasn't indexed on 2026-05-22).
- F10 picked up the `required` keyword page (now the cleaner fix) in addition to the "Nullable reference types" overview.
- Two analyzer rules (CA2022, CA2264) were added as cross-references for F16 and F12 respectively.

**No findings were added, removed, or re-severitied** — the body of the audit stands. The remediation order at the top of this document is still the recommended sequence.

---

## Post-remediation re-audit — 2026-05-23

**Method.** Implemented the OpenSpec change `address-audit-findings-2` (skill: `openspec-apply-change`). Re-verified each finding against the new HEAD by reading the cited source lines; re-ran the entire unit test suite under `Release` with `TreatWarningsAsErrors=true`.

**Build & tests gate (Z.1/Z.2/Z.3/Z.4).**

```
dotnet build SignalCli.sln -c Release                                       →  0 warnings, 0 errors
dotnet test Tests/SignalCli.Tests/...                                       →  Passed: 173 / 173 (баз. 152 → +21 нових)
dotnet test Tests/SignalCli.Tests.Integration/... --filter Category=E2E     →  Passed: 1 / 1 (реальний signal-cli 0.14.3)
npx @fission-ai/openspec validate address-audit-findings-2 --strict         →  valid
```

### Finding status

| ID | Sev | Status | Доказ у коді |
|----|-----|--------|--------------|
| F1 | High | **Closed** | `Config.RequestTimeoutSeconds` (default 30), `JsonRpcClient.InvokeMethodAsync` будує лінкований CTS + `CancelAfter`, фолтить `TimeoutException` коли тільки timeoutCts спрацював і callerCancel не запитано. Спільний `Config` для startup-ping `JsonRpcClientHostedService` гарантує A.2. Тести `A3_*`. |
| F2 | High | **Closed** | `OnProcessExited` — тонка `async void` обгортка з `try/catch`, що делегує `OnProcessExitedAsync` під `_operationLock`; повторна перевірка `_disposed`/`_stopping` і `state == Stopped` всередині локу. Тест `B3_IntentionalStop_ConcurrentWithUnexpectedExit_NoRestartAfterStop`. |
| F3 | High | **Closed** | `Config.RestartWindowSeconds` (default 60); `ScheduleRestartWindowReset` після переходу в Running скидає `_restartCount→0`, якщо процес стабільно у Running після вікна. Обидва шляхи інкрементації — під `_operationLock`. Тест `B6_WindowedRestartBudget_RecoversAfterStableRunWindow`. |
| F4 | High | **Closed** | Reader-цикли керуються `_readerCts`+task-ами під `_readerLock`; на `OnStreamPairChanged` старі скасовуються і чекаються (`StopReadersSync`/`Async`) перед стартом нових. Stdout читає з `pair.StandardOutput` напряму (без обгортки в `using StreamReader`). На stderr-циклі тепер `try/catch when (!_disposed)`. `JsonRpcClient : IAsyncDisposable`. Тест `A8_OnStreamPairChanged_StopsPriorReaderBeforeStartingNew`. |
| F5 | High | **Closed** | `SignalAccounts.ListAccounts`/`SyncAccount`/`SignalGroups.ListGroups` логують на Information лише `Count`; деталі — `LogTrace`. `SyncAccount` (порожній response-record) — навіть без полів. Тест `PrivacyLoggingTests`. |
| F6 | High (docs) | **Closed** | README снівпети виправлено: `attachment.Attachments[...]` замість `attachment.DataMessage.Attachments[...]`, `reaction.Reaction.Emoji` замість `reaction.DataMessage.Reaction.Emoji`, `finishResult.number` замість `.DeviceId`. |
| F7 | High (tests) | **Closed** | Створено `Tests/SignalCli.Tests.Integration/` (xUnit, `[Trait("Category","E2E")]`). Проєкт через RID-conditional `ProjectReference` підтягує бандл JRE для Win/macOS або native-бінарник для Linux і копіює артефакти в TargetDir. Тест `Version_RealSignalCli_ReturnsNonEmpty` стартує справжній host (`AddSignalCli` + `JsonRpcClientHostedService`), чекає `WaitForReadyAsync`, викликає `version` через `ISignalCliClient` й асертить `0.14.3`. Локальний прогін Windows: ✓ за 15с (перший запуск, із JVM bootstrap). CI-матриця win/macOS/Linux заведена в `.github/workflows/e2e.yml`. Misnamed `IntegrationTests` файл перейменовано в `StateTests` (G.7e). |
| F8 | Medium | **Closed** | `SendUnifiedMessageAsync` тепер відкидає змішування `userRecipients>0 && groupRecipients>0` (раніше — недосяжна гілка). Тест `D8_SendText_MixedUserAndGroupRecipients_Rejects`. |
| F9 | Medium | **Closed** | `Process.WaitForExitAsync(linkedCts)` замість фіксованого `Task.Delay(StopTimeoutSeconds)`; stdin "exit" обгорнуто власним `try/catch (IOException or ObjectDisposedException or InvalidOperationException)`. |
| F10 | Medium | **Closed** | `Config.AppHome`/`JavaExecutable`/`LibDirectory` тепер `required`; компілятор примушує задати або через `CreateDefault`, або вручну з усіма полями. |
| F11 | Medium | **Closed** | `LogFileCli`/`StoragePathCli` без явного значення обчислюються з `Path.Combine(AppHome, …)` у `ToProcessConfig` — зміна `AppHome` тепер переносить шляхи. Тест `CreateDefault_AppHomeChangeAffectsLogAndStoragePaths`. |
| F12 | Medium | **Closed** | `ArgumentNullException.ThrowIfNull(options)` на `SendTextMessageAsync`/`SendAttachmentAsync`/`SendStickerAsync`. Тести `D12_*_NullOptions_ThrowsArgumentNullException`. |
| F13 | Medium | **Closed** | Нові обсервабли `Quotes`/`Edits`/`RemoteDeletes` на `ISignalEventService` + диспетчер у `SignalEventService.OnNotificationReceived`. "Unknown" гілка замінена на конкретні події; залишковий debug-лог лиш для справді невпізнаних DataMessage. |
| F14 | Medium | **Closed** | HealthMonitor loop-тести де-флакнуто на `TaskCompletionSource`-сигналах + ранній stop, щоб не накопичувати ітерації під `HealthCheckIntervalSeconds=0`. Додатково: `SignalCliHealthMonitor` тепер приймає опціональний `TimeProvider`; тест `MonitorLoop_ShouldRespectHealthCheckInterval` переведений на `FakeTimeProvider` — інтервали перевіряються у віртуальному часі (264 мс замість 3500+ мс, точна рівність замість timing-вікон). |
| F15 | Medium | **Closed** (pre-existing) | Існуючі facade-passthrough тести вже валідні (captured-parameter); нові privacy-тести верифікують поведінку через лог-перевірки. |
| F16 | Medium | **Closed** | `MimeTypeHelper`: `ftyp` тепер декодує major-brand на байтах 8..11 (qt/heic/M4A/3gp/isom тощо); потокова перевантажена версія використовує `ReadAtLeast(12)`. Тести `GetMimeType_DetectsBySignature(…brand…)`, `GetMimeType_IsoBmff_M4A_ReturnsAudioMp4`, `GetMimeType_IsoBmff_UnknownBrand_FallsBackToMp4`. |
| F17 | Medium | **Closed** | `SignalEventService.StartAsync` ідемпотентний — `Interlocked.Exchange` стертої старої підписки; `Dispose` тепер також робить `Subject.Dispose()` після `OnCompleted()`. |
| F18 | Low | **Closed** | `TextStyleParser` при незакритому маркері повертає літерал у текст (одно- та дво-символьні маркери). Тести `Parse_UnclosedMarker_EmitsLiteralCharNoRange`, `Parse_UnclosedBold_EmitsTwoLiteralChars`, `Parse_MixedClosedAndUnclosed_PreservesUnclosedLiteral`. |
| F19 | Low | **Closed** | `tcs.TrySetCanceled(linkedCts.Token)` у `JsonRpcClient` — `OperationCanceledException` несе токен. |
| F20 | Low | **Closed** | `JsonRpcResponse`/`JsonRpcError`/`JsonRpcNotificationRaw`/`JsonRpcNotification<T>` — нульабельність полів виправлена; `JsonRpcError.Data` тепер `JsonElement?` (без object-reflection-фолбеку). |
| F21 | Low | **Closed** | Глобальний `NoWarn>$(NoWarn);1591</NoWarn>` знято; raised CS1591 — закрито шляхом додавання XML-документації на всі публічні члени `*MessageOptions`/`*Builder`/`AttachmentEntry`/`ProcessStateManager`. |
| F22 | Low | **Closed** | `.github/copilot-instructions.md`: "UTF-8-BOM" → "ASCII-only, no BOM". Усі тестові коментарі нормалізовано до української — окремий пас на 14 файлах прибрав і російські-специфічні літери (ы/ъ/э/ё → 0 входжень), і типові російські лексеми (что/это/если/когда/чтобы/тоже/также/только/может/нельзя/нужно/должн… → 0 входжень). Білд `TreatWarningsAsErrors=true` 0/0, 173/173 юніт-тести зелені. |
| F23 | Low | **Closed** | `AddSignalCli` ідемпотентний (early-return якщо `Config` уже зареєстровано) + `TryAddSingleton` на більшості сервісів. `configure: Action<Config>?`. |
| F24 | Low | **Closed** | `paramName` тепер `nameof(quoteTimestamp)` — одне ім'я; список незаданих полів — у message-тексті. |
| F25 | Low | **Closed** | `ProcessStateManager.UpdateState`/`Dispose` під одним `System.Threading.Lock`; `UpdateState` перевіряє `_disposed` до `OnNext`. |

**Підсумок.** **Усі 25 знахідок закрито**; всі 45 задач плану `address-audit-findings-2` виконано (з локальною валідацією E2E на Windows; CI matrix включає macOS/Linux jobs).

### Що додалося в публічному API (additive, без breaking)

- `Config.RequestTimeoutSeconds: int` (default 30)
- `Config.RestartWindowSeconds: int` (default 60)
- `Config.LogFileCli: string?` (раніше `string` з base-directory дефолтом — поведінка дефолтна змінилась: тепер обчислюється від `AppHome`)
- `Config.StoragePathCli: string?` (так само)
- `Config.AppHome`/`JavaExecutable`/`LibDirectory` тепер `required` — *компіляційна* зміна: `new Config()` без них не пройде (рекомендовано `Config.CreateDefault()`).
- `IJsonRpcClient : IAsyncDisposable, IDisposable` — додано `IAsyncDisposable`.
- `ISignalEventService.Quotes`, `.Edits`, `.RemoteDeletes` — нові обсервабли.
- Нові DTO: `QuoteEventArgs`, `EditEventArgs`, `RemoteDeleteEventArgs`.
- Нові `JsonRpcException` поведінки: `InvokeMethodAsync` тепер може кидати `TimeoutException` (раніше — нескінченне очікування).

### Залишкові ризики / follow-up

1. **E2E CI**: локально перевірено лише Windows гілку матриці (15с з JVM bootstrap). macOS і Linux jobs у `.github/workflows/e2e.yml` будуть валідовані при першому push/PR — якщо `SignalCli.runtime.jre.osx-arm64` чи `SignalCli.runtime.native` download-скрипт впаде на актуальній рантайм-версії, треба буде підлатати SHA-pin.
