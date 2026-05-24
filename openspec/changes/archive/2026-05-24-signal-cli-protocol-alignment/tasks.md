# Tasks — signal-cli-protocol-alignment

## 0. Setup

- [ ] 0.1 Branch `claude/signal-cli-protocol-alignment` from current `main`.
- [ ] 0.2 `npx -y @fission-ai/openspec@latest validate signal-cli-protocol-alignment --strict` — green.

## 1. `graceful-shutdown-fix` *(3.0.2 patch — own PR)*

- [ ] 1.1 In `src/SignalCli/Services/SignalCli/SignalCliHostedService.cs` `StopProcessInternalAsyncNoLock`, replace lines 370-378 (the `WriteLineAsync("exit") + FlushAsync` block + its catch) with:
  ```csharp
  if (_currentStreamPair is not null)
  {
      try
      {
          _currentStreamPair.StandardInput.Close();
      }
      catch (Exception ex) when (ex is IOException or ObjectDisposedException)
      {
          SignalCliHostedServiceLog.StdinCloseFailed(_logger, ex);
      }
  }
  ```
- [ ] 1.2 In `src/SignalCli/Logging/SignalCliHostedServiceLog.cs`:
  - Delete the `ExitWriteFailed` `[LoggerMessage]` method.
  - Add a new `[LoggerMessage]` method `StdinCloseFailed(ILogger logger, Exception ex)` at `Debug` level, using the same EventId block 100-199 (pick the next free slot — current `ExitWriteFailed` slot is fine to reuse).
- [ ] 1.3 New unit test in `Tests/SignalCli.Tests/SignalCliHostedService/SignalCliHostedServiceShutdownTests.cs`:
  - `StopAsync_ClosesStdin_DoesNotWriteExitText` — capture the stdin `StreamWriter`'s underlying `MemoryStream`; after `StopAsync`, assert (a) stream contents do not contain the byte sequence for "exit", (b) the writer/stream is closed.
- [ ] 1.4 New Integration E2E test in `Tests/SignalCli.Tests.Integration/SignalCliE2EGracefulShutdownTests.cs`:
  - `Process_GracefulShutdown_ExitsWithoutKillTimeout` — use the existing `TryBuildHost` skip-gate. Inject a `FakeLogger<SignalCliHostedService>` via DI. Start real signal-cli; capture the child process ID; call `await hostedService.StopAsync(CancellationToken.None)`; assert (a) within `StopTimeoutSeconds + 2s` wall-clock the process has exited (`Process.GetProcessById(pid).HasExited`), (b) the captured `FakeLogger` recorded NO `ProcessKillTimeout` entry.
- [ ] 1.5 `grep -rn 'WriteLineAsync("exit"' src/ Tests/` — confirm zero matches.
- [ ] 1.6 `grep -rn 'ExitWriteFailed' src/ Tests/` — confirm zero matches.
- [ ] 1.7 `dotnet build -p:TreatWarningsAsErrors=true && dotnet test SignalCli.sln` — clean.
- [ ] 1.8 **Commit** `fix: close stdin instead of writing literal "exit" for graceful signal-cli shutdown`.
- [ ] 1.9 **Open PR** targeting `main`. CHANGELOG entry under `[3.0.2]` with the bug-fix note. Bump `<Version>3.0.2</Version>` (patch).
- [ ] 1.10 After merge: tag `v3.0.2`. Other capabilities below rebase on top.

## 2. `typed-rpc-errors` *(3.1.0)*

- [ ] 2.1 New file `src/SignalCli/Exceptions/JsonRpcErrorCode.cs`:
  ```csharp
  namespace SignalCli.Exceptions;

  /// <summary>
  /// JSON-RPC error codes used by signal-cli — standard JSON-RPC 2.0 codes
  /// (-32700..-32603) plus signal-cli-specific codes (-1..-6).
  /// Source: SignalJsonRpcCommandHandler.java:35-280 at commit bda4e7f.
  /// </summary>
  public enum JsonRpcErrorCode
  {
      ParseError = -32700,
      InvalidRequest = -32600,
      MethodNotFound = -32601,
      InvalidParams = -32602,
      InternalError = -32603,
      UserError = -1,
      IoError = -3,
      UntrustedIdentity = -4,
      RateLimit = -5,
      CaptchaRejected = -6,
  }
  ```
- [ ] 2.2 Modify `src/SignalCli/Exceptions/JsonRpcException.cs`:
  - Add `public JsonRpcErrorCode? KnownCode => Enum.IsDefined(typeof(JsonRpcErrorCode), Code) ? (JsonRpcErrorCode)Code : null;`.
  - XMLDoc updates documenting the full code table.
- [ ] 2.3 Two new files in `src/SignalCli/Exceptions/`:
  - `RateLimitException.cs` — `public sealed class RateLimitException(JsonRpcError error) : JsonRpcException(error)` with one-line XMLDoc and a `// Code is always -5 by construction.` comment.
  - `UntrustedIdentityException.cs` — same shape for code `-4`.
- [ ] 2.4 In `src/SignalCli/Services/Rpc/JsonRpcClient.cs` `InvokeMethodAsync`, replace the existing `throw new JsonRpcException(response.Error)` (line 495) with:
  ```csharp
  throw response.Error.Code switch
  {
      (int)JsonRpcErrorCode.RateLimit => new RateLimitException(response.Error),
      (int)JsonRpcErrorCode.UntrustedIdentity => new UntrustedIdentityException(response.Error),
      _ => new JsonRpcException(response.Error),
  };
  ```
- [ ] 2.5 New tests in `Tests/SignalCli.Tests/JsonRpcErrorTests.cs` (file may already exist from `audit-followup-2026 §6.a`; extend or create):
  - `[Theory]` over all 10 codes — assert deserialized `KnownCode` matches.
  - `Error_UnknownCode_KnownCodeIsNull` — feed `-9999`, assert `KnownCode is null`.
  - `Error_RateLimit_ThrowsRateLimitException` — round-trip through `JsonRpcClient` with mocked response.
  - `Error_UntrustedIdentity_ThrowsUntrustedIdentityException` — same.
- [ ] 2.6 Register the 3 new types (`JsonRpcErrorCode`, `RateLimitException`, `UntrustedIdentityException`) + the `KnownCode` property in `PublicApiSurfaceTests` baseline (or regenerate baseline if `audit-followup-2026` already shipped).
- [ ] 2.7 `dotnet build -p:TreatWarningsAsErrors=true && dotnet test SignalCli.sln` — clean.
- [ ] 2.8 **Commit** `feat: typed JsonRpcErrorCode enum + RateLimit/UntrustedIdentity derived exceptions`.

## 3. `field-barrier-hardening` *(3.1.0)*

- [ ] 3.1 In `src/SignalCli/Services/Rpc/JsonRpcClientHostedService.cs:20`:
  ```csharp
  private volatile IJsonRpcClient? _client;
  ```
  No other code changes required — the existing `Client` getter, `StartAsync` write, `StopAsync` clear, `DisposeAsync` clear all become acquire/release semantically with `volatile`.
- [ ] 3.2 In `src/SignalCli/Services/SignalCli/SignalCliHostedService.cs` sync `Dispose()` (lines 627-632), wrap the `DisposeCore()` call:
  ```csharp
  public void Dispose()
  {
      if (Interlocked.Exchange(ref _disposedFlag, 1) != 0) return;
      GC.SuppressFinalize(this);

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
- [ ] 3.3 New test `Tests/SignalCli.Tests/JsonRpcClientHostedServiceTests.cs`:
  - `Client_ConcurrentStartStop_NoNullRefException` — set up `Task.WhenAll(StartAsync, sleep 5ms + StopAsync, readClient×100)` racing on the same service. Assert no `NullReferenceException` thrown. (`InvalidOperationException("not initialized")` from the getter IS acceptable — it's the documented exception when `_client` is null.)
- [ ] 3.4 New test `Tests/SignalCli.Tests/SignalCliHostedService/SignalCliHostedServiceDisposalTests.cs`:
  - `SyncDispose_DuringCleanup_AcquiresLock` — set up a mock `WaitForExitAsync` that blocks for 100ms; call `StopAsync` on one thread, `Dispose()` on another thread immediately. Assert no exception leaked from either thread; assert the captured `FakeLogger` recorded the `Disposing` entry exactly once.
- [ ] 3.5 `dotnet build && dotnet test` — clean.
- [ ] 3.6 **Commit** `feat: volatile _client + lock-acquired sync Dispose to close race windows`.

## 4. `signal-cli-quirks-doc` *(3.1.0)*

- [ ] 4.1 Add a new H2 section to `CLAUDE.md` after "Conventions (match the existing code)" and before "Established patterns", titled **"signal-cli protocol behavior we depend on"**. Use the exact content from `design.md` §4 of this change.
- [ ] 4.2 Use the `[System.IO.File]` UTF-8-BOM-aware mass-edit pattern when writing the section (per CLAUDE.md "Mass-edit safety") — the section contains backticks and Cyrillic-adjacent characters.
- [ ] 4.3 **Commit** `docs: pin signal-cli protocol behaviors in CLAUDE.md`.

## 5. `attachment-threshold-margin` *(3.1.0)*

- [ ] 5.1 In `src/SignalCli/Services/Signal/SignalMessage.cs:35`:
  ```csharp
  // signal-cli's Jackson 2.20.2 enforces StreamReadConstraints.maxStringLength = 20_000_000
  // characters PER STRING TOKEN. base64 encoding inflates raw bytes by 4/3, so 12M raw →
  // 16M encoded, leaving 4M of margin for the surrounding `send` JSON envelope. The old
  // value 15M gave 20M encoded — exactly at the cap with zero margin.
  private const long MaxInlineEncodedAttachmentBytes = 12_000_000;
  ```
- [ ] 5.2 If `audit-followup-2026 §6.b` `AttachmentEntryTests.EncodedSize_ExactlyAtBoundary_UsesTempFile` has already merged, update its boundary value to `12_000_000`. If it has not merged yet, leave a note in `audit-followup-2026/tasks.md §6b.1` to use `12_000_000` from the start (cross-PR coordination).
- [ ] 5.3 `dotnet test` — clean (boundary test should now pin the new value).
- [ ] 5.4 **Commit** `defensive: lower inline-attachment threshold to 12M for 4M Jackson margin`.

## 6. Final pass (3.1.0 PR — single trailing commit)

- [ ] 6.1 Bump `<Version>3.1.0</Version>`, `<AssemblyVersion>3.1.0</AssemblyVersion>`, `<FileVersion>3.1.0</FileVersion>` in `src/SignalCli/SignalCli.csproj`.
- [ ] 6.2 `CHANGELOG.md [3.1.0]` entry:
  - `### ✨ Додано` — `JsonRpcErrorCode` enum, `JsonRpcException.KnownCode`, `RateLimitException`, `UntrustedIdentityException`.
  - `### 🛠 Внутрішнє` — `volatile _client`, lock-acquired sync Dispose, `MaxInlineEncodedAttachmentBytes` 15M → 12M, new CLAUDE.md section on signal-cli protocol behavior.
  - Reference the cited signal-cli commit `bda4e7f` so a future reader can re-verify.
- [ ] 6.3 Final `npx -y @fission-ai/openspec@latest validate signal-cli-protocol-alignment --strict` green.
- [ ] 6.4 **Commit** `chore: 3.1.0 release — version bump + CHANGELOG`.

## 7. Post-merge

- [ ] 7.1 Wait for CI green on `main` (both PRs — 3.0.2 patch and 3.1.0 minor).
- [ ] 7.2 `git pull --rebase origin main && git push origin main`.
- [ ] 7.3 `npx -y @fission-ai/openspec@latest archive signal-cli-protocol-alignment --yes --skip-specs`.
- [ ] 7.4 Update CLAUDE.md "Implemented, merged, archived" with a new bullet citing both release tracks.
- [ ] 7.5 Tag `v3.0.2` and `v3.1.0` on GitHub.
