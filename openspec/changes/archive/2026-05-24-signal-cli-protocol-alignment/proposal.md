# signal-cli protocol alignment

## Why

A 2026-05-24 investigation of the **original signal-cli Java application** at `bda4e7f` (after 0.14.3) — cloned outside our repo for read-only analysis — found that several of our long-standing wrapper assumptions about signal-cli's behavior are wrong, and one of them is a **correctness bug** that has been shipping since 1.0.

Findings:

1. **🔴 `WriteLineAsync("exit")` on stdin does nothing.** `SignalCliHostedService.StopProcessInternalAsyncNoLock` ([cs:370-378](../../../src/SignalCli/Services/SignalCli/SignalCliHostedService.cs)) writes a literal `"exit"` newline before `WaitForExitAsync`, expecting graceful shutdown. signal-cli does **not** have a JSON-RPC `exit` method, and does **not** read stdin for the literal text `"exit"`. The stdin reader is `JsonRpcReader` which parses every line as JSON ([JsonRpcReader.java:59-75](https://github.com/AsamK/signal-cli/blob/master/lib/src/main/java/org/asamk/signal/jsonrpc/JsonRpcReader.java)). Our line produces a `-32700 Parse error` response on stdout (which we then log as `UnknownMessage` at Trace because no pending `id` matches) — the process **keeps running**, our wait-for-exit hits the timeout, and we always fall through to `Process.Kill(entireProcessTree: true)` (= TerminateProcess on Windows, SIGKILL on Unix). Every "graceful" stop today is in fact a hard kill. signal-cli's canonical graceful triggers are SIGTERM/SIGINT (its `Shutdown` class installs `sun.misc.Signal` handlers) **and stdin EOF** (the line supplier returns null → reader loop ends → JVM shuts down normally). Closing the stdin stream from .NET produces an EOF and is the cross-platform fix.
2. **🟡 signal-cli emits 6 custom JSON-RPC error codes** (`-1`, `-3`, `-4`, `-5`, `-6`) for user-error / I/O-error / untrusted-identity / rate-limit / captcha-rejected. We wrap every error as `JsonRpcException` with `Code = response.Error.Code` (raw int) and force the consumer to either inspect the message text (locale-dependent) or pattern-match the integer. CLAUDE.md rule #14 *"Typed/idempotent state errors"* applies — wire-level errors deserve typed surfaces too.
3. **🟡 Two race-smells in our own code, found during the audit:**
   - `JsonRpcClientHostedService._client` is a plain `IJsonRpcClient?` field. Reads outside any lock from `SignalCliHealthMonitor.PingCliAsync` and `SignalEventService.StartAsync`; writes from `StartAsync`/`StopAsync`/`DisposeAsync`. On x64 the reference read is atomic, but there is no happens-before relationship between writer and reader — on ARM (.NET 10 ships ARM64), a stale-cached null may be observed after `_client` was assigned. `volatile` (or `Interlocked.Exchange`) fixes this with negligible runtime cost.
   - `SignalCliHostedService.Dispose()` (sync path) reads `_currentProcess` without taking `_operationLock`. If `CleanupProcess` is concurrently running under the lock-finally of `StopProcessInternalAsyncNoLock`, the read may observe a torn state or a disposed process. Acquiring the lock with a 50ms try-wait makes the sync path safe; the async-Dispose already drains via the lock.
4. **🟢 Inline-attachment boundary is tighter than we documented.** Jackson 2.20.2 (signal-cli's library) enforces `StreamReadConstraints.maxStringLength = 20_000_000` characters per **string token** — not per JSON line. Our threshold of `MaxInlineEncodedAttachmentBytes = 15_000_000` allows a 20M base64-encoded attachment string (15M raw × 4/3 ≈ 20M), which lands exactly at Jackson's limit with zero margin for the surrounding `send` parameters. One byte of JSON envelope overhead and the request fails with a Jackson `StreamConstraintsException`. Lower the threshold to give a 4M margin.
5. **🟢 Quirks worth pinning in CLAUDE.md.** Several behaviors we depend on are not documented anywhere in our codebase: stdout is line-flushed by signal-cli (so our `ReadLineAsync` loop works); signal-cli processes requests in parallel via virtual threads (so response order ≠ request order, and our `id`-based correlation is mandatory, not optional); `subscribeReceive` is **not** idempotent at the protocol level (signal-cli returns a fresh ID every call — our idempotency lives entirely on the wrapper side via reservation TCS).

## What Changes

Five capabilities, each independently shippable. Two release tracks: `graceful-shutdown-fix` lands as a **3.0.2 patch** (correctness bug, non-breaking); the other four ship together as **3.1.0** (additive — new types, new docs, lowered threshold).

1. **`graceful-shutdown-fix`** *(3.0.2 patch)*:
   - `SignalCliHostedService.StopProcessInternalAsyncNoLock` SHALL close stdin (`pair.StandardInput.Close()`) instead of writing `"exit"` and flushing. The wait-for-exit logic stays; the kill-on-timeout fallback stays. Remove the unused `ExitWriteFailed` `[LoggerMessage]` entry — it can no longer fire.
   - New regression test `SignalCliHostedServiceShutdownTests.StopAsync_ClosesStdin_DoesNotWriteExitText` — capture the stdin `MemoryStream` content; assert it contains zero "exit" bytes; assert stream is closed.
   - New Integration E2E `Process_GracefulShutdown_ExitsWithoutKillTimeout` — start real signal-cli, stop, assert process exited within `StopTimeoutSeconds` AND no `ProcessKillTimeout` log was emitted.
2. **`typed-rpc-errors`** *(3.1.0 additive)*:
   - New enum `SignalCli.Exceptions.JsonRpcErrorCode` with members: `ParseError = -32700`, `InvalidRequest = -32600`, `MethodNotFound = -32601`, `InvalidParams = -32602`, `InternalError = -32603`, `UserError = -1`, `IoError = -3`, `UntrustedIdentity = -4`, `RateLimit = -5`, `CaptchaRejected = -6`.
   - New property `JsonRpcException.KnownCode { get; } : JsonRpcErrorCode?` returning `null` for unknown codes.
   - Two derived exceptions for the codes consumers most likely want to catch by type: `RateLimitException : JsonRpcException` (code `-5`) and `UntrustedIdentityException : JsonRpcException` (code `-4`). Throw the appropriate derived type from `JsonRpcClient.InvokeMethodAsync` when the response error code matches; fall back to `JsonRpcException` for the rest.
   - XMLDoc on `JsonRpcException` documents the full error-code table with a citation to the signal-cli source file.
3. **`field-barrier-hardening`** *(3.1.0 additive)*:
   - `JsonRpcClientHostedService._client` changes from `IJsonRpcClient?` to `volatile IJsonRpcClient?`. The `Client` getter, `StartAsync` write, `StopAsync` clear, and `DisposeAsync` clear all use the volatile field semantics.
   - `SignalCliHostedService.Dispose()` sync path SHALL take `_operationLock.Wait(TimeSpan.FromMilliseconds(50))` before reading `_currentProcess`. If the wait times out (an in-flight async operation didn't drain), proceed to the kill path anyway — but the lock acquisition synchronizes the read with `CleanupProcess`'s lock-finally writes.
   - Two new race-prober tests (`JsonRpcClientHostedServiceTests.Client_ConcurrentStartStop_NoNullRef`, `SignalCliHostedServiceDisposalTests.SyncDispose_DuringCleanup_DoesNotObserveTornState`) — both lightweight (no FakeTimeProvider needed; just `Task.WhenAll` racing two threads).
4. **`signal-cli-quirks-doc`** *(3.1.0 docs)*:
   - New CLAUDE.md section *"signal-cli protocol behavior we depend on"* under "Conventions". Documents: stdin EOF / SIGTERM = graceful shutdown; stdout pure-JSON & line-flushed by signal-cli; virtual-thread parallel request processing → correlate by `id`; `subscribeReceive` non-idempotent at protocol level; Jackson `maxStringLength = 20_000_000` per string token; full error-code table with semantics; Java 25 requirement.
   - Each fact carries a citation to the signal-cli source file (`SignalJsonRpcCommandHandler.java:35-280`, `JsonRpcReader.java:58`, `JsonWriterImpl.java:30`, etc.) — so a future maintainer can re-verify against a newer signal-cli release.
5. **`attachment-threshold-margin`** *(3.1.0 additive — defensive)*:
   - `SignalMessage.MaxInlineEncodedAttachmentBytes` SHALL drop from `15_000_000` to `12_000_000`. New rationale: 12M raw × 4/3 = 16M encoded; Jackson's 20M cap leaves 4M of margin for the surrounding `send` JSON (`recipient`, `message` text, `mentions`, `quote*`, `attachments[]` paths after switchover). The old value left zero margin.
   - Update the `MaxInlineEncodedAttachmentBytes` XMLDoc to cite Jackson 2.20.2's default + the 4M margin justification.
   - Edge-case test `EncodedSize_AtNewBoundary_UsesTempFile` updates the boundary value; the test from `audit-followup-2026 §6b.1` is the source-of-truth for this assertion.

## Capabilities

### New Capabilities

- `graceful-shutdown-fix`: stdin EOF (close-stream) SHALL be the graceful shutdown trigger; literal `"exit"` text SHALL NOT be written. Regression test pins this.
- `typed-rpc-errors`: signal-cli error codes `-1..-6` SHALL be representable as a typed enum + two derived exceptions for high-leverage cases.
- `field-barrier-hardening`: `volatile`-or-`Interlocked` write semantics SHALL apply to fields accessed from multiple threads without explicit lock-coordination.
- `signal-cli-quirks-doc`: CLAUDE.md SHALL document the protocol behaviors the wrapper relies on, each with a citation to the signal-cli source.
- `attachment-threshold-margin`: the inline-vs-temp-file boundary SHALL leave a 4M margin under Jackson's 20M `maxStringLength` for surrounding JSON fields.

### Modified Capabilities

- `rpc-back-pressure` (archived in 2026-05-24-post-modernize-tuning): the channel pattern stays; only the error mapping in `JsonRpcClient.InvokeMethodAsync` adds derived-exception branching. No throughput or threading change.
- `hosting-modernization` (same archive): stdin close + lock-acquired sync Dispose tighten the existing shutdown contract; the `IHostedLifecycleService` phase methods stay no-op.
- `attachment-handling` (older capability — archived earlier): boundary moves from 15M to 12M raw; the data-URI vs temp-file branch logic is unchanged.

## Out of scope

- **Sending SIGTERM explicitly via `Process.Kill(System.Diagnostics.Signal.SIGTERM)`** (new .NET 10 overload). Considered as a redundant safety net after stdin-close. Skipped because (a) stdin-close already triggers the same graceful path inside signal-cli; (b) the new overload is Unix-only — Windows has no POSIX signals; (c) one canonical path is easier to reason about than two.
- **Migrating to signal-cli's `--socket` mode** (Unix domain socket transport instead of stdin/stdout). signal-cli supports this; we could replace the entire process-management layer. Out of scope — would be a separate architectural change with its own OpenSpec.
- **Decoding `error.data` payload field** into structured `JsonRpcException.Data`. `audit-followup-2026 §6.a` (`Error_WithDataField_PreservesPayload`) already pins that `JsonRpcError.Data` is preserved on the wire DTO; making the contents typed is out of scope here.
- **Adding test for stdout block-buffering edge case** (Java's default 8KB block buffer for non-TTY stdout). signal-cli's `JsonWriterImpl.flush()` per line makes this a non-issue per the investigation — no need for a defensive test.

## Dependencies

- **Independent of `audit-followup-2026` and `deprecated-shim-removal`.** Can ship in any order relative to them. The `PublicApiSurfaceTests` baseline (from `audit-followup-2026`) updates with the new public types (`JsonRpcErrorCode`, `RateLimitException`, `UntrustedIdentityException`, `JsonRpcException.KnownCode` property) — that's the only cross-change touch.

## Release strategy

- Branch: `claude/signal-cli-protocol-alignment`.
- Capability `graceful-shutdown-fix` lands first **as its own PR** → cut **v3.0.2 patch release** immediately (correctness bug fix shouldn't wait on the rest).
- The remaining four capabilities ship together on a second PR → **v3.1.0 minor release**. One commit per capability inside that PR (4 commits + 1 trailing CHANGELOG/version bump).
- Both PRs gated on `dotnet test SignalCli.sln` green and `openspec validate signal-cli-protocol-alignment --strict` green.
