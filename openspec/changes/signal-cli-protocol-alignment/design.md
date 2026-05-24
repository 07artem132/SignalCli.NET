# Design — signal-cli-protocol-alignment

## Method

Five capabilities, two release tracks. `graceful-shutdown-fix` is sequenced first because it is the only one that fixes a real bug; the others are improvements over already-shipping behavior.

```
graceful-shutdown-fix  (v3.0.2 patch — own PR)
       │
       ▼  (rebase)
typed-rpc-errors  ─┐
field-barrier-hardening  ─┤── single PR → v3.1.0
signal-cli-quirks-doc  ─┤
attachment-threshold-margin  ─┘
```

The four 3.1.0 capabilities are independent; tasks.md orders them for review ergonomics, not dependency.

## 1. `graceful-shutdown-fix`

### Problem (confirmed by reading signal-cli sources)

[`SignalCliHostedService.cs:370-378`](../../../src/SignalCli/Services/SignalCli/SignalCliHostedService.cs):
```csharp
try
{
    await _currentStreamPair.StandardInput.WriteLineAsync("exit").ConfigureAwait(false);
    await _currentStreamPair.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
}
catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
{
    SignalCliHostedServiceLog.ExitWriteFailed(_logger, ex);
}
```

Signal-cli has no JSON-RPC `exit` method. The stdin reader is `JsonRpcReader` ([`JsonRpcReader.java:59-75`](https://github.com/AsamK/signal-cli/blob/master/lib/src/main/java/org/asamk/signal/jsonrpc/JsonRpcReader.java)), which parses every line as JSON. Our literal `"exit"\n` produces `JsonRpcResponse.Error{ code: -32700, message: "Parse error" }` on stdout — the process **keeps running**. The subsequent `WaitForExitAsync` with `StopTimeoutSeconds` (default 2s) times out, and we hard-kill with `Kill(entireProcessTree: true)`. On Unix this is SIGKILL (.NET's default for `Process.Kill`), bypassing signal-cli's `sun.misc.Signal` handler for graceful shutdown.

The canonical graceful triggers per signal-cli source:
- **SIGTERM/SIGINT** ([`Shutdown.java:24-25`](https://github.com/AsamK/signal-cli/blob/master/src/main/java/org/asamk/signal/Shutdown.java)) — installs `sun.misc.Signal` handlers.
- **stdin EOF** — when the line supplier returns `null`, `JsonRpcReader`'s while-loop exits, the dispatcher's `handleConnection` finally-block runs (clears all subscriptions, [`SignalJsonRpcDispatcherHandler.java:212-214`](https://github.com/AsamK/signal-cli/blob/master/src/main/java/org/asamk/signal/jsonrpc/SignalJsonRpcDispatcherHandler.java)), and the JVM exits cleanly.

### Approach

**Replace** the `WriteLineAsync("exit")` + `FlushAsync` block **with** `_currentStreamPair.StandardInput.Close()`. This:
- On Unix: closes the write-end of the pipe → signal-cli's `FileInputStream` gets EOF → graceful shutdown via the reader-loop terminating naturally.
- On Windows: same — the anonymous-pipe write-end closing produces EOF on signal-cli's `System.in`.

The `WaitForExitAsync` + timeout + `Kill(entireProcessTree: true)` fallback stays. After this change, the fallback fires only when signal-cli is genuinely hung (not on every shutdown).

**Remove** the `ExitWriteFailed` `[LoggerMessage]` definition from `SignalCliHostedServiceLog` — the call site is gone, the unused method would be dead-code-stripped only with `IsTrimmable` (we have `IsAotCompatible=true` which sets `IsTrimmable=true`). Eventid `1XX` slot is freed for future use.

### Why not also send SIGTERM via `Process.Kill(System.Diagnostics.Signal.SIGTERM)`?

.NET 10 added the overload, but it is Unix-only. Adding it as a second safety-net introduces platform-conditional code with no clear benefit — stdin-close already triggers the same JVM-graceful path on every OS. One canonical mechanism beats two.

### Tests

- **Unit:** `Tests/SignalCli.Tests/SignalCliHostedService/SignalCliHostedServiceShutdownTests.cs`
  - `StopAsync_ClosesStdin_DoesNotWriteExitText` — Use a `MockProcess` whose `StandardInput` is a wrapping `StreamWriter` over `MemoryStream`. After `StopAsync`, assert (a) the underlying `MemoryStream` content has length 0 (or contains no "exit" sequence), (b) the writer is closed.
- **Integration:** `Tests/SignalCli.Tests.Integration/SignalCliE2EGracefulShutdownTests.cs`
  - `Process_GracefulShutdown_ExitsWithoutKillTimeout` — Use the same `TryBuildHost` skip-gate. Start, capture child PID, `await hostedService.StopAsync()`, assert (a) process exited within `StopTimeoutSeconds + 1s` wall-clock, (b) no `ProcessKillTimeout` log entry was emitted via the captured `FakeLogger`.

### Release

This is the **only** capability that fixes a correctness bug. Lands as its own PR → cut v3.0.2 patch release. The remaining capabilities rebase on top after the patch lands.

## 2. `typed-rpc-errors`

### Approach

Add to `src/SignalCli/Exceptions/`:

```csharp
public enum JsonRpcErrorCode
{
    // Standard JSON-RPC 2.0
    ParseError = -32700,
    InvalidRequest = -32600,
    MethodNotFound = -32601,
    InvalidParams = -32602,
    InternalError = -32603,

    // signal-cli custom (SignalJsonRpcCommandHandler.java:35-280 @ bda4e7f)
    UserError = -1,
    IoError = -3,
    UntrustedIdentity = -4,
    RateLimit = -5,
    CaptchaRejected = -6,
}

public class JsonRpcException : Exception
{
    public int Code { get; }
    public JsonRpcErrorCode? KnownCode =>
        Enum.IsDefined(typeof(JsonRpcErrorCode), Code) ? (JsonRpcErrorCode)Code : null;
    // ... existing ctors retained verbatim
}

public sealed class RateLimitException : JsonRpcException
{
    public RateLimitException(JsonRpcError error) : base(error) { }
    // Code is always -5 by construction
}

public sealed class UntrustedIdentityException : JsonRpcException
{
    public UntrustedIdentityException(JsonRpcError error) : base(error) { }
    // Code is always -4 by construction
}
```

In `JsonRpcClient.InvokeMethodAsync` ([cs:494-495](../../../src/SignalCli/Services/Rpc/JsonRpcClient.cs)):
```csharp
if (response.Error != null)
    throw response.Error.Code switch
    {
        (int)JsonRpcErrorCode.RateLimit => new RateLimitException(response.Error),
        (int)JsonRpcErrorCode.UntrustedIdentity => new UntrustedIdentityException(response.Error),
        _ => new JsonRpcException(response.Error),
    };
```

### Why only two derived exceptions, not five?

Rationale, by code:
- `-5 RateLimit` — consumer-actionable (retry with backoff). Derived type → `catch (RateLimitException)` ergonomic.
- `-4 UntrustedIdentity` — consumer-actionable (verify safety number, prompt user). Same justification.
- `-1 UserError`, `-3 IoError`, `-6 CaptchaRejected` — situational; `catch (JsonRpcException ex) when (ex.KnownCode == JsonRpcErrorCode.UserError)` is fine. Adding a derived class per code would bloat the surface without clear consumer value.

### Tests

- `Tests/SignalCli.Tests/JsonRpcErrorTests.cs` (extending the file from `audit-followup-2026 §6.a`):
  - `Error_KnownSignalCliCode_ExposedAsKnownCodeEnum` — for each of the 10 codes (5 standard + 5 signal-cli), deserialize a synthetic response and assert `KnownCode` matches.
  - `Error_UnknownCode_KnownCodeIsNull` — feed code `-9999` and assert `KnownCode is null`.
  - `Error_RateLimit_ThrowsRateLimitException` — happy path through `JsonRpcClient` with mocked `_pendingRequests` resolution.
  - `Error_UntrustedIdentity_ThrowsUntrustedIdentityException` — same.

### Public API surface impact

Adds: enum + 2 derived exception types + 1 property = 4 lines in `PublicApiSurfaceTests` baseline. Baseline updated as part of the PR.

## 3. `field-barrier-hardening`

### Problem (race-smells found during the audit)

#### Smell A: `JsonRpcClientHostedService._client`

[`JsonRpcClientHostedService.cs:20`](../../../src/SignalCli/Services/Rpc/JsonRpcClientHostedService.cs):
```csharp
private IJsonRpcClient? _client;  // <-- plain field
```

Reads (concurrent, no lock):
- `Client` getter (line 45) → called from `SignalCliHealthMonitor.PingCliAsync`, `SignalEventService.StartAsync`, `SignalEventService.SubscribeAsync`.

Writes:
- `StartAsync` (line 77): `_client = _factory.Create();`
- `StopAsync` (line 113): `_client = null;`
- `DisposeAsync` (line 135): `_client = null;`

On x64, reference reads are atomic — no torn pointer. But the .NET memory model does NOT guarantee that a write in `StartAsync` becomes visible to a read in `SignalCliHealthMonitor.PingCliAsync` running on a different core, unless either (a) a memory barrier exists between them, or (b) the field is `volatile` / accessed through `Interlocked.*` / under a lock.

For x64, in practice, the JIT and the strong memory model make this nearly always work. **On ARM64** (.NET 10 supports ARM as a first-class target), the weaker model permits an actual stale read.

#### Smell B: `SignalCliHostedService.Dispose()` sync path

[`SignalCliHostedService.cs:678-694`](../../../src/SignalCli/Services/SignalCli/SignalCliHostedService.cs) — the `DisposeCore` helper called from sync `Dispose()`:
```csharp
if (_currentProcess != null && !_currentProcess.HasExited)
{
    _currentProcess.Kill(entireProcessTree: true);
}
```

`_currentProcess` is mutated under `_operationLock` (inside `StartProcessInternalAsyncNoLock` and `CleanupProcess`). Sync `Dispose()` does NOT take the lock. If `CleanupProcess` is mid-flight on another thread (under the lock-finally of `StopProcessInternalAsyncNoLock`), sync `Dispose()` may observe either the old non-null reference (after `_currentProcess.Dispose()` ran but before the field was set null) or the new null (after the field was nulled). The non-null-but-disposed observation leads to `ObjectDisposedException` from `HasExited`.

Note: `DisposeAsync` already drains `_operationLock.WaitAsync` for up to 2s before calling `DisposeCore`, so the *async* path is safe. The sync path is the gap.

### Approach

#### Fix A:

Change [`JsonRpcClientHostedService.cs:20`](../../../src/SignalCli/Services/Rpc/JsonRpcClientHostedService.cs):
```csharp
private volatile IJsonRpcClient? _client;
```

`volatile` on a reference field is exactly the contract we want: every read has acquire semantics; every write has release semantics. No `Interlocked.Exchange` needed because we never do compare-and-set on this field.

#### Fix B:

Change [`SignalCliHostedService.cs:627-632`](../../../src/SignalCli/Services/SignalCli/SignalCliHostedService.cs) sync `Dispose()`:
```csharp
public void Dispose()
{
    if (Interlocked.Exchange(ref _disposedFlag, 1) != 0) return;
    GC.SuppressFinalize(this);

    // post-modernize-tuning §X.Y (signal-cli-protocol-alignment §3): synchronize
    // with CleanupProcess's lock-finally to avoid observing a torn _currentProcess.
    // 50ms is enough to drain a typical cleanup; if it times out we proceed anyway
    // (worst case: existing race window — same as before this change).
    bool lockTaken = false;
    try
    {
        lockTaken = _operationLock.Wait(TimeSpan.FromMilliseconds(50));
        DisposeCore();
    }
    finally
    {
        if (lockTaken) _operationLock.Release();
    }
}
```

The `Wait(TimeSpan)` overload returns `false` on timeout instead of throwing — keeps sync `Dispose` from blocking forever. If it times out we still call `DisposeCore` (preserves current behavior under contention).

### Tests

- `Tests/SignalCli.Tests/JsonRpcClientHostedServiceTests.cs`:
  - `Client_ConcurrentStartStop_NoNullRefException` — `Task.WhenAll(StartAsync, sleep+StopAsync, readClient×100)` racing on the same service; no `NullReferenceException` or `InvalidOperationException` from any thread.
- `Tests/SignalCli.Tests/SignalCliHostedService/SignalCliHostedServiceDisposalTests.cs`:
  - `SyncDispose_DuringCleanup_AcquiresLock` — simulate a long-running `StopProcessInternalAsyncNoLock` (mock `WaitForExitAsync` to delay 100ms); concurrently call `Dispose()` from another thread; assert no exception and the disposal-complete log is emitted.

## 4. `signal-cli-quirks-doc`

### Approach

Add a new H2 section to `CLAUDE.md` after **"Conventions (match the existing code)"** and before **"Established patterns"**:

```markdown
## signal-cli protocol behavior we depend on

These are facts about the *upstream* signal-cli Java app that our wrapper relies on. Each is cited
to a specific signal-cli source file at commit bda4e7f (after 0.14.3). Re-verify against newer
signal-cli releases when bumping the pinned version in `SignalCli.runtime.csproj`.

- **Graceful shutdown trigger = stdin EOF or SIGTERM.** signal-cli has no `exit` JSON-RPC method
  and does not read literal text on stdin (every stdin line is parsed as JSON — see
  `JsonRpcReader.java:59-75`). Our wrapper closes stdin (`StandardInput.Close()`) in
  `StopProcessInternalAsyncNoLock`; signal-cli's reader-loop exits naturally, its dispatcher's
  finally-block clears subscriptions, and the JVM shuts down cleanly. **Critical rule:** never
  re-add `WriteLineAsync("exit")` — see `signal-cli-protocol-alignment` capability
  `graceful-shutdown-fix` for the history.

- **Stdout = pure JSON-RPC, line-flushed.** signal-cli's `JsonWriterImpl.write` calls
  `writer.flush()` after every JSON line (`JsonWriterImpl.java:30`), so our `ReadLineAsync` loop
  observes each message promptly. signal-cli never emits banner/version/log output on stdout;
  all diagnostics go to stderr via SLF4J/Logback. The `UnknownMessage` log line in our
  `ProcessMessageAsync` should fire approximately never in practice — if it fires, look for a
  protocol drift in a newer signal-cli release.

- **Parallel request processing → match by `id`, not by order.** signal-cli's `JsonRpcReader`
  uses `Executors.newVirtualThreadPerTaskExecutor()` to handle requests
  (`JsonRpcReader.java:58`). Response arrival order is non-deterministic. Our wrapper's
  `_pendingRequests : ConcurrentDictionary<string, TaskCompletionSource>` keyed by request `id`
  is mandatory — never refactor to a queue or order-based correlation.

- **`subscribeReceive` is NOT idempotent at the protocol level.** signal-cli returns a fresh
  ID via `AtomicInteger.getAndIncrement()` for every call
  (`SignalJsonRpcDispatcherHandler.java:143`). Our idempotency lives entirely in
  `SignalEventService._pendingSubscribes` (reservation TCS pattern). If our code path ever
  bypasses that reservation, signal-cli will deliver duplicate `receive` notifications.

- **Jackson `maxStringLength = 20_000_000` PER STRING TOKEN.** signal-cli uses Jackson 2.20.2
  (`gradle/libs.versions.toml:10`) with `StreamReadConstraints` defaults. Our
  `MaxInlineEncodedAttachmentBytes = 12_000_000` (after `attachment-threshold-margin`) keeps the
  base64-encoded attachment string ≤ 16M with 4M of margin for the rest of the `send` request.
  Total-JSON-line length is also checked in `JsonRpcClient.SendRequestAsync` against 20M (a
  separate, looser check).

- **Custom error codes outside JSON-RPC 2.0 standard.** signal-cli emits these in addition to
  `-32600..-32603` and `-32700` (`SignalJsonRpcCommandHandler.java:35-280`):
  - `-1` UserError (bad input, invalid number)
  - `-3` IoError (file system / network)
  - `-4` UntrustedIdentity (key verification failure) — surfaced as `UntrustedIdentityException`
  - `-5` RateLimit (server throttle) — surfaced as `RateLimitException`
  - `-6` CaptchaRejected
  See `JsonRpcErrorCode` enum + `JsonRpcException.KnownCode` for the typed surface.

- **Java 25 requirement.** signal-cli 0.14.0+ requires JDK 25 (`build.gradle.kts:7-8`).
  `signal-cli 0.14.3` (our pinned version in `SignalCli.runtime.csproj`) is the first 0.14.x.
  Bumping signal-cli later than 0.14.x without bumping JDK fails at JVM startup with
  `UnsupportedClassVersionError`.
```

The point of this section: every fact has a source-file citation. When signal-cli bumps to 0.15 and any of these change, a future maintainer can grep for "`bda4e7f`" or the specific file name and re-verify.

## 5. `attachment-threshold-margin`

### Approach

[`SignalMessage.cs:35`](../../../src/SignalCli/Services/Signal/SignalMessage.cs):
```csharp
private const long MaxInlineEncodedAttachmentBytes = 15_000_000;
```
becomes
```csharp
// signal-cli's Jackson 2.20.2 enforces StreamReadConstraints.maxStringLength = 20_000_000
// characters PER STRING TOKEN. base64 encoding inflates raw bytes by 4/3, so 12M raw →
// 16M encoded, leaving 4M of margin for the surrounding `send` JSON (recipient, message
// body, mentions[], quote*, sticker, …). The old value 15M gave 20M encoded — exactly at
// the Jackson cap with zero margin. See CLAUDE.md "signal-cli protocol behavior we depend
// on" → Jackson maxStringLength.
private const long MaxInlineEncodedAttachmentBytes = 12_000_000;
```

The downstream `JsonRpcClient.SendRequestAsync` check on **total JSON line length > 20M** ([JsonRpcClient.cs:560](../../../src/SignalCli/Services/Rpc/JsonRpcClient.cs)) stays — it catches the rare case where many small fields combined breach 20M. Not the same constraint as Jackson's per-token check; both are needed.

### Tests

`audit-followup-2026 §6.b` already specifies `AttachmentEntryTests.EncodedSize_ExactlyAtBoundary_UsesTempFile`. After this change, that test's threshold constant updates from `15_000_000` → `12_000_000`. If `audit-followup-2026` has already merged, the boundary test is rewritten in this PR; otherwise the audit-followup-2026 boundary value is set to 12_000_000 from the start (coordinate in `audit-followup-2026 §6.b` task — note left in tasks.md).

## Verification

After all five capabilities land:

```bash
dotnet build SignalCli.sln -p:TreatWarningsAsErrors=true
# Expected: 0 warnings. The removed ExitWriteFailed [LoggerMessage] is gone with the
# capability; no orphan reference remains.

dotnet test SignalCli.sln
# Expected: test count grows by ~7 (1 stdin-close unit test + 1 graceful-shutdown E2E +
# 4 JsonRpcErrorTests cases + 2 race-prober tests = 8 net new; -1 from
# StopProcessInternalAsyncNoLock removing the ExitWriteFailed assertion if such a test
# exists today — confirm via grep). After audit-followup-2026 + this change, expected
# count ≈ 243.

grep -rn 'WriteLineAsync\("exit"' src/
# Expected: 0 matches.

grep -rn 'ExitWriteFailed' src/SignalCli/Logging/
# Expected: 0 matches.
```

Integration E2E `Process_GracefulShutdown_ExitsWithoutKillTimeout` is the canonical proof of the bug fix. Run on Win/macOS bundled-JRE and Linux native — the path must succeed on all three.
