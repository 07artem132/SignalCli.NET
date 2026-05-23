# Design — post-modernize-tuning

## Method

The change addresses three categories that were **out of scope** of `comprehensive-code-audit`:

1. **Regressions from the previous remediation** (introduced while fixing F4/F25) — confirmed by reading the cited lines in the current code, see [Verification](#verification).
2. **Modernization items deliberately deferred** by `modernize-architecture` (the net9→net10 / Newtonsoft→STJ migration was scoped to *migration*, not *go further*).
3. **DX + tests + performance** items that need Microsoft-Learn-grounded best-practice judgement, not bug-fixing.

Implementation is split into eight capabilities (one spec each). Each capability ships independently and has its own tasks block in `tasks.md`.

## Subsystem-by-subsystem design

### 1. `rpc-back-pressure`

**Problem (Audit A3):** `JsonRpcClient.ProcessMessage` is invoked synchronously from the stdout reader loop (`JsonRpcClient.cs:199-210`). It then calls `_notificationSubject.OnNext(typed)` (line 297). If any subscriber is slow, stdout drain stops — and `signal-cli` keeps writing, eventually filling the OS pipe buffer and back-blocking the JVM.

**Approach:**

- Introduce a private `Channel<JsonRpcNotificationRaw>` created with `Channel.CreateBounded(new BoundedChannelOptions(N) { SingleReader=true, SingleWriter=true, FullMode=Wait })`.
- The stdout loop only does: parse → `await _channel.Writer.WriteAsync(...)`. Parsing failure → log + continue (existing behavior).
- A second `Task` consumes the channel and runs `_notificationSubject.OnNext` (same logic moved). If a subscriber is slow, the channel fills, and the reader's `WriteAsync` awaits — but it awaits *asynchronously*, releasing the reader thread to keep `ReadLineAsync` pumping (MS: *System.Threading.Channels library — Bounded creation patterns*).
- Capacity `N` is configurable via `Config.NotificationChannelCapacity` (default 1024 — large enough that a healthy stream never queues, small enough that a stuck subscriber is visible quickly).
- On `DisposeAsync`: `_channel.Writer.Complete()`, await the consumer task to drain, then `_notificationSubject.OnCompleted()`.

**Why a channel, not just `Task.Run(() => OnNext(...))`:** firing a task per message would (a) allocate per-message, (b) deliver out of order, (c) provide no back-pressure (unbounded queue equals OOM under bad subscribers).

### 2. `state-machine-thread-safety`

**Problem (Audit A2):** `ProcessStateManager.UpdateState` calls `_stateSubject.OnNext` while holding `_lock` (`ProcessStateManager.cs:86-99`). This was the F25 fix — but a synchronous Rx subscriber that re-enters `UpdateState` (or even just reads `CurrentState`) deadlocks. Today's subscribers happen not to do that. Fragile.

**Approach:**

- Refactor `UpdateState` into snapshot-then-emit:
  ```csharp
  ProcessStateInfo snapshot;
  bool wasDisposed;
  lock (_lock)
  {
      wasDisposed = _disposed;
      if (wasDisposed) return;
      _currentStateInfo = new ProcessStateInfo(newState, streamPair, error);
      snapshot = _currentStateInfo;
  }
  _logger.LogInformation("Стан процесу змінено на {NewState}", newState);
  // Emit OUTSIDE the lock. If _disposed was set between exit-lock and OnNext,
  // BehaviorSubject.OnNext on a disposed subject would throw ObjectDisposedException;
  // we catch it explicitly because the disposal race window is acceptable here.
  try { _stateSubject.OnNext(snapshot); }
  catch (ObjectDisposedException) { /* раса з Dispose — це ок, бо вже disposed */ }
  ```
- `Dispose` flips `_disposed = true` under the same lock *and* sets `Volatile.Write(ref _disposed, 1)` for safe lock-free reads.

**Net effect:** the F25 invariant ("no `OnNext` on disposed subject") is preserved — but no longer through lock-holding-during-emit. The disposal race is collapsed into a single `try/catch` block whose contract is documented inline.

### 3. `subscription-race-safety`

**Problem (Audit A4):** `SignalEventService.SubscribeAsync` (line 73-103) checks `_accountSubscriptions.ContainsKey(account)` under `_subscriptionsLock`, then calls `signal-cli` over RPC, then takes the lock again to insert. Between the two locks, a second caller can pass the first check and also send `subscribeReceive`. One of the two wins the second lock-insert; the other throws. But signal-cli has already created two subscriptions — one is now orphaned with no client tracking.

**Approach:** reservation placeholder.

```csharp
const int Pending = -1;
lock (_subscriptionsLock)
{
    if (_accountSubscriptions.ContainsKey(account))
        throw new InvalidOperationException("...вже підписаний...");
    _accountSubscriptions[account] = Pending;          // reservation
}
int subscriptionId;
try
{
    var resp = await _signalCliClient
        .InvokeMethodAsync<JsonElement, SubscribeReceiveParameters>(
            "subscribeReceive", new SubscribeReceiveParameters(account), ct)
        .ConfigureAwait(false);
    subscriptionId = resp.GetInt32();
}
catch
{
    lock (_subscriptionsLock) _accountSubscriptions.Remove(account);  // rollback
    throw;
}
lock (_subscriptionsLock) _accountSubscriptions[account] = subscriptionId;
```

A second caller arriving while `Pending` is in the dict gets the "already subscribed" exception **before** the second RPC fires. No orphan.

### 4. `agent-friendly-api`

**Problem (Audit D1-D11):** the public API has DX defects that mostly fall into "Microsoft naming conventions violated". Reasoning grounded in MS Learn:

- *Capitalization conventions* — properties are PascalCase.
- *TAP* — async methods end in `Async`.
- *Framework Design Guidelines* — CT is a parameter, not a member.
- *ConfigureAwait FAQ* — every library `await` configures continuation.

**Approach:** all breaks land in a single `Version=3.0.0` release. Migration guide:

| Was | Becomes |
| --- | --- |
| `resp.number` | `resp.Number` |
| `resp.id` | `resp.Id` |
| `signalAccounts.ListAccounts()` | `signalAccounts.ListAccountsAsync()` |
| `signalDevices.StartLink()` | `signalDevices.StartLinkAsync()` |
| `signalDevices.FinishLink(uri, name)` | `signalDevices.FinishLinkAsync(uri, name, ct)` |
| `signalGroups.ListGroups(...)` | `signalGroups.ListGroupsAsync(..., ct)` |
| `signalCli.Version(ct)` | `signalCli.VersionAsync(ct)` |
| `new TextMessageOptions.Builder(...).WithCancellationToken(ct).Build()` | `await signalMessage.SendTextMessageAsync(opts, ct)` |

JSON-RPC wire format is unchanged: PascalCase properties get explicit `[JsonPropertyName("number")]` / `[JsonPropertyName("id")]` to keep on-the-wire compatibility with signal-cli.

### 5. `high-performance-logging`

**Problem (Audit P1):** 106 `_logger.LogXxx(...)` sites. Two of them are on the hottest path:

- `JsonRpcClient.cs:208` — `LogTrace("Отримано рядок від signal-cli: {Line}", line)` on every JSON line.
- `JsonRpcClient.cs:283` — `LogDebug("Отримано повідомлення: Method={Method}", ...)` on every notification.
- `ProcessStateManager.cs:97` — `LogInformation("Стан процесу змінено на {NewState}", newState)` boxes a `ProcessState` enum every state change.

MS *High-performance logging in .NET* + analyzer `CA1848`: replace with `[LoggerMessage]` source-generated partial methods. Each service gets a `static partial class Log` (file-local, `internal`) next to it.

**Approach:** mechanical migration. The Ukrainian message strings stay identical; the change is purely structural. Each call:

```csharp
// Before
_logger.LogInformation("Виклик JSON-RPC методу: {Method}", method);

// After (in Log.SignalService.cs)
[LoggerMessage(EventId = 2001, Level = LogLevel.Debug,
    Message = "Виклик JSON-RPC методу: {Method}")]
internal static partial void InvokeStart(ILogger logger, string method);

// Call site
Log.InvokeStart(_logger, method);
```

Event IDs are namespaced by service (2xxx = SignalService, 3xxx = JsonRpcClient, …) so log aggregators can filter by stable IDs.

### 6. `aot-readiness`

**Problem (Audit P6):** `SignalCli.csproj` doesn't declare `<IsAotCompatible>true</IsAotCompatible>`. The two foreseeable blockers:

1. `SignalJson.Options` uses `JsonTypeInfoResolver.Combine(SignalJsonContext.Default, new DefaultJsonTypeInfoResolver())` — the second resolver is annotated `RequiresUnreferencedCode`.
2. `Nito.AsyncEx` uses internal reflection (its `AsyncContext` infrastructure traces via `AsyncLocal` + reflection).

**Approach:**

- Drop `Nito.AsyncEx` (per Audit B2): `AsyncLock` → `SemaphoreSlim(1, 1)` everywhere it's used (`JsonRpcClient._sendLock`, `SignalCliHostedService._operationLock`).
- Drop the `DefaultJsonTypeInfoResolver` fallback. Every serialized type must be registered in `SignalJsonContext`. The one offender is the test helper `JsonSerializer.SerializeToElement(parameters, SignalJson.Options)` where `parameters` is a `TRequest` — but every actual `TRequest` (per `SubscribeReceiveParameters`, `SendMessageFullParameters`, etc.) is already in the context. Tests that pass anonymous types switch to using one of the registered DTOs or to a tests-only `JsonSerializerContext`.
- Set `<IsAotCompatible>true</IsAotCompatible>`. This auto-enables `EnableTrimAnalyzer`, `EnableSingleFileAnalyzer`, `EnableAotAnalyzer` (MS *Native AOT — AOT-compatibility analyzers*). Surface remaining warnings as compile-time errors and resolve before merge.
- Flip `JsonSourceGenerationMode.Metadata` → `Default` (or per-type `JsonSourceGenerationMode.Serialization` for the hot small types). Fast-path serialization for short JSON-RPC payloads is 1.5-2× faster (MS *Reflection vs source generation — fast path*).

### 7. `test-virtualization`

**Problem (Audit T2, T3, T5):** 12 wall-clock `Task.Delay` waits across the suite; reflection-based access to private fields; no race-condition tests for the new findings.

**Approach:**

1. Every `Task.Delay` in `Tests/SignalCli.Tests/**` becomes a virtual-time wait via `FakeTimeProvider.Advance(...)` (already used in `SignalCliHealthMonitorLoopTests:180-227`). The pattern is well-trodden — apply to `SignalCliHostedServiceAuditTests.B6` (1s+ real wait → 1s virtual), `SignalCliHostedServiceRestartTests` (100ms × 3 → virtual ticks), etc.
2. Replace `GetPrivateField<T>` / `SetPrivateField` reflection helpers with internal `TestSeam` properties on the classes under test. The `[InternalsVisibleTo("SignalCli.Tests")]` attribute already exists (`SignalCli.csproj:31-33`).
3. Add three new tests (matching the three new race-safety capabilities):
   - `StateManagerReentrancyTests` — synchronous subscriber re-enters `UpdateState`; without the fix this deadlocks (test asserts completion within 2 s virtual).
   - `SubscribeRaceTests` — `Task.WhenAll(Enumerable.Range(0,10).Select(_ => svc.SubscribeAsync(acct)))`; assert exactly one RPC was sent (mocked `ISignalCliClient` counts invocations).
   - `BackPressureTests` — slow subscriber + 1000 simulated messages; assert reader keeps draining (channel never throws `ChannelFullException`; subscriber receives them in order).
4. Migrate `xunit 2.9.2` → `xunit.v3 1.x` + `Microsoft.Testing.Platform.MSBuild` (MS *Testing in .NET — xUnit.net v3*). This unlocks `IAsyncLifetime` for hosted-service fixtures and `Assert.Skip` for the integration project.

### 8. `cloud-development`

**Problem:** Claude Code on the Web sessions start with a fresh Ubuntu 24.04 container that has neither `dotnet` nor a primed NuGet cache. Every session paid the cost of figuring this out.

**Approach (already drafted in this change):**

- `.claude/hooks/session-start.sh` — synchronous SessionStart hook. Installs `dotnet-sdk-10.0` via apt, restores `Tests/SignalCli.Tests`, builds `src/SignalCli`. Skips the heavy runtime-package downloads (signal-cli + JRE) which would add ~200 MB and are only needed for E2E.
- `.claude/settings.json` — registers the hook.
- `docs/cloud-development.md` — explains what runs, what doesn't, and which hosts the network policy must allow.
- `CLAUDE.md` gets a one-line "Cloud development" pointer.

Synchronous-blocking mode (per `session-start-hook` skill default): a Claude session waits ~30-60s on first start but never hits "where's dotnet?" surprises. Switch to `{"async": true}` only if startup latency becomes the dominant cost.

### 9. `hosting-modernization`

**Problem (Audit B1, B3, B4, C1, A1):** several hosted-service patterns predate the .NET 8/10 additions.

- `SignalCliHealthMonitor` hand-rolls `Task.Run(MonitorLoop)` instead of inheriting `BackgroundService` (MS *Worker Services in .NET*).
- Startup ordering between `SignalCliHostedService` and `JsonRpcClientHostedService` relies on registration order rather than `IHostedLifecycleService.StartedAsync` (MS *Generic Host*).
- `SignalCliHostedService.Dispose` is sync-only despite holding a child process, Rx subjects, and a timer CTS.
- `ProcessRunner.StartProcessWithHandle` allocates `Task` via `Task.FromResult` for purely synchronous work.
- `JsonRpcClient.Dispose()` is the sync-over-async bridge `DisposeAsync().AsTask().GetAwaiter().GetResult()` (MS *Common async/await bugs*) — a regression introduced when `IAsyncDisposable` was added.

**Approach:** mechanical. `BackgroundService` for the monitor; `IHostedLifecycleService` on the two hosted services with the dependency expressed in `StartedAsync`; `IAsyncDisposable` on `SignalCliHostedService`; `ProcessRunner` returns sync or `ValueTask` with `ValueTask.FromResult`; `JsonRpcClient` drops `IDisposable` from its declared interfaces. The DI consumer already does `is IAsyncDisposable` (`JsonRpcClientHostedService.StopAsync`), so the public contract change is invisible to it.

### 10. `options-validation`

**Problem (Audit B5, B7):** `Config` is registered as a plain singleton (`services.AddSingleton(config)`); no validation; mixed `init` and `set` properties; no way to bind from `IConfiguration`.

**Approach (MS *Options pattern in .NET*):**

```csharp
services.AddOptions<Config>()
    .Configure(configure ?? (_ => { }))
    .ValidateDataAnnotations()
    .Validate(c => c.JvmModeRequiresLibDir(), "LibDirectory required in JVM mode")
    .ValidateOnStart();
services.AddSingleton(sp => sp.GetRequiredService<IOptions<Config>>().Value);
```

Plus `[Range]`/`[Required]` annotations on every `Config` field; full `init`-only migration; new `services.AddSignalCli(IConfiguration)` overload that binds the section through the same pipeline.

### 11. `code-hygiene`

**Problem (Audit C3, C4, C5, C7, C8, C9, C10, P4):** a cluster of small chores accumulated across the previous remediation. Individually each is a 🟢 finding; together they remove ~40 lines and one analyzer-detectable bug class.

**Approach:** mechanical, one PR per cluster if needed:

- `SignalEventService` → `sealed`.
- Delete unused `_rpcClient` field + its assignment.
- `AtomicCounter` — either annotate the wrap-around with a `// WHY:` comment OR widen to `long`.
- Drop `CultureInfo.InvariantCulture` from the request-id `ToString()` (digits are culture-invariant; not buying anything).
- `SignalMessage.ValidateRecipients` — materialize once at entry; subsequent code consumes the list.
- `SendUnifiedMessageAsync(23 args)` → internal `record UnifiedSendRequest(...)`.
- Audit `catch (Exception ex) { _logger.LogError(...); throw; }` sites — delete (caller logs) or enrich (method/account context).
- `Config.BuildClasspath` caches `Directory.GetFiles` result.

### 12. `supply-chain-hardening`

**Problem (Audit N1-N2, N6-N7, N11-N12, N19-N20):** the `runtime*` MSBuild surface has correctness bugs on non-Windows; supply-chain pinning is spread across .ps1/.sh hard-coded constants with no MSBuild-property anchor; CI pins community actions to moving tags; downloaded JREs are not integrity-checked post-extraction.

**Approach (consistent with CLAUDE.md rule 7, expanded):**

- **Forward slashes everywhere in MSBuild.** `SignalCli.Native.targets` Include glob, Copy destination template, and the `Exists()` gate in `SignalCli.runtime.csproj` switch to `/`. On Linux MSBuild interprets `\` as a literal — meaning `Exists('…\bin')` is always false (re-downloads every build) and `Include='…\**\*'` matches nothing (native binary never delivered). Forward slashes work uniformly on every OS.
- **Marker-file `Exists()`.** Key the incremental gate on the actual extracted binary (`bin/signal-cli` / `bin/java`), not a directory that a partial download leaves behind.
- **csproj is the single source of truth for pinning.** Add `<SignalCliVersion>`, `<SignalCliSha256>`, `<JreVersion>`, `<JreSha256>` to the relevant csproj-и; pass via `-Sha256 $(…)` to the download scripts; scripts validate but no longer hard-code. CLAUDE.md rule 7 ("update the hash in **both** the .ps1 and .sh") is rewritten in this change to reflect the new flow (single edit in csproj). This does **not** weaken integrity — the SHA validation in scripts remains; only the source-of-truth location moves.
- **GitHub Actions to commit SHAs.** Per *Security hardening for GitHub Actions — Using third-party actions*: tag movement (whether innocent re-tag or compromise) cannot change CI behavior. First-party `actions/*` SHOULD also pin where reproducibility matters.
- **Post-extract integrity check.** After `<Unzip>`, fail loudly if `bin/java[.exe]` is missing. This is defense in depth on top of the download-time SHA pin.
- **Case-invariant PowerShell SHA compare.** Both sides `.ToLowerInvariant()` so an uppercase SHA in csproj doesn't false-negative against lowercase `Get-FileHash` output.
- **LICENSE.txt inline.** Pack `LICENSE.txt` into the .nupkg root (NuGet 5.10+ recommendation).
- **Adoptium URL fallback.** Document the assumed URL pattern in a script comment; on 404, fail with a message that names the URL tried and the env-var to override.

**Non-regression notes:**

- N6 changes the location of SHA truth from script to csproj, **not** the SHA-verification policy. The scripts still verify and fail before extraction. CLAUDE.md rule 7's intent ("don't ship unverified payloads") is preserved; only the "update in two places" mechanics changes.
- N1/N2 fix bugs that prevent the native-runtime package from working on Linux. There is no working baseline to regress against.

## Severity rubric

Inherited from `comprehensive-code-audit`:

- **High**: race, leak, deadlock-risk, blocking sync-over-async; or breaks an existing capability requirement.
- **Medium**: best-practice deviation with limited blast radius; brittle test; missing race-test.
- **Low**: style, naming, micro-optimization, doc.

## Verification

Every High finding was confirmed by reading the cited source lines in the **current** tree, not from the audit memo. The reentrancy risk in `ProcessStateManager` was confirmed by tracing `address-audit-findings-2` task `B.25 (F25)` through git history and re-reading the resulting code. The sync-over-async in `JsonRpcClient.Dispose` was confirmed by reading the `Dispose` body (`.AsTask().GetAwaiter().GetResult()` — verbatim).

## Non-goals

- No new public capabilities outside what's listed in proposal.md.
- No change to the JSON-RPC wire format: PascalCase response properties keep `[JsonPropertyName]` set to the wire name.
- No change to logging *content* — privacy invariant (no PII above `Trace`) is preserved verbatim through the `LoggerMessage` migration. The capability `logging-privacy` from `address-audit-findings` is unchanged.
- The integration-test gap (E2E for `send`/`subscribe`/`receive`) is acknowledged in audit T7 but left for a follow-up change — it requires a registered signal-cli account or a mock-server fixture, both substantial.
