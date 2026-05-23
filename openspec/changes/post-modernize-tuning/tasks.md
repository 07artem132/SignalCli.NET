## 0. Setup

- [x] 0.1 Audit findings cross-referenced with current source (every High verified by reading cited lines) — done as part of the 2026-05-23 agent-friendly audit; new findings tracked under `(audit N#)` tags throughout this file
- [x] 0.2 Branch `claude/post-modernize-tuning` created from current `main`

## 1. RPC back-pressure (capability `rpc-back-pressure`)

- [x] 1.1 (A3) Add `SignalCliOptions.NotificationChannelCapacity` (default 1024, `[Range(1,1_000_000)]`)
- [x] 1.2 (A3) Create `Channel<JsonRpcNotification<SubscriptionEventArgs>>` in `JsonRpcClient` with `BoundedChannelOptions { SingleReader=true, SingleWriter=true, FullMode=Wait }` — capacity from options
- [x] 1.3 (A3) Stdout reader loop becomes parse-then-`await WriteAsync` — `ProcessMessage` → `ProcessMessageAsync(line, ct)`; no `OnNext` inline
- [x] 1.4 (A3) Channel-consumer `Task` runs the fan-out: `NotificationConsumerLoopAsync` reads `ReadAllAsync` and calls `_notificationSubject.OnNext` with try/catch boundary so a synchronous subscriber exception doesn't kill the loop
- [x] 1.5 (A3) `DisposeAsync` calls `_notificationChannel.Writer.TryComplete()`, awaits `_notificationConsumerTask` (with 5s timeout — log `NotificationConsumerStopTimeout` on miss), then completes/disposes the subject
- [ ] 1.6 (A3) Test: 1000-message burst with a 50ms/message slow subscriber — reader never blocks; messages arrive in order **(deferred — needs slow-subscriber fixture in JsonRpcClientTests; existing `ProcessMessage_WhenNotification_ShouldPushToNotifications` updated to poll-await the channel consumer)**
- [x] 1.7 (audit N4) `JsonRpcClient` constructor accepts `TimeProvider? timeProvider = null` (defaults to `TimeProvider.System`); the `new CancellationTokenSource(_requestTimeout)` site at `JsonRpcClient.cs:361` switches to `new CancellationTokenSource(_requestTimeout, _timeProvider)` (the .NET 8+ overload). `JsonRpcClientFactory` also propagates `TimeProvider`. DI registers `TimeProvider` via `services.TryAddSingleton(TimeProvider.System)` in `RegisterCoreServices`.
- [ ] 1.8 (audit N4) Test: `FakeTimeProvider.Advance(RequestTimeoutSeconds + 1)` virtualizes the timeout path; `InvokeMethodAsync` faults with `TimeoutException` without any wall-clock wait. **(Deferred — JsonRpcClient mock-wiring + Subject<StreamPair>-based setup is heavyweight; tracked separately.)**

## 2. State-machine thread safety (capability `state-machine-thread-safety`)

- [x] 2.1 (A2) `ProcessStateManager.UpdateState`: snapshot under lock, emit `OnNext` outside lock
- [x] 2.2 (A2) `_disposed` becomes `int` with `Volatile.Read`/`Interlocked.Exchange`; lock-free short-circuit at the top of `UpdateState`
- [x] 2.3 (A2) Catch `ObjectDisposedException` from `OnNext` (documented disposal race window — between exit-of-lock and OnNext call)
- [x] 2.4 (C2) `_disposed` in `SignalCliHostedService`, `JsonRpcClient`, `JsonRpcClientHostedService`, `SignalEventService` switched to `Interlocked.Exchange`-based `int` with `Volatile.Read` accessor
- [ ] 2.5 (A2) Test: synchronous Rx subscriber that re-enters `UpdateState` completes within 2s virtual time (deadlock guard) **(deferred — needs reentrancy fixture; behavior validated by code review: System.Threading.Lock not held during OnNext)**

## 3. Subscription race safety (capability `subscription-race-safety`)

- [x] 3.1 (A4) `SignalEventService.SubscribeAsync` inserts `account → TaskCompletionSource<int>` placeholder under `_subscriptionsLock` before sending RPC (reservation pattern; leader/follower roles)
- [x] 3.2 (A4) On RPC exception, rollback the placeholder (Remove + TrySetException — propagates failure to all followers awaiting the TCS)
- [x] 3.3 (A4) On RPC success, overwrite placeholder with real `subscriptionId` (write to `_accountSubscriptions`, remove from `_pendingSubscribes`, `TrySetResult(id)` wakes followers)
- [x] 3.4 (A4) `UnsubscribeAsync` searches `_accountSubscriptions` only (placeholders in `_pendingSubscribes` never match because they don't hold `subscriptionId` — they hold a TCS that resolves to one)
- [x] 3.5 (A4) `ObjectDisposedException.ThrowIf(_disposed, ...)` added to `SubscribeAsync`/`UnsubscribeAsync` (audit C6)
- [x] 3.6 (A4) Test: `Task.WhenAll(10x SubscribeAsync(same account))` → mocked RPC invoked exactly once (`Times.Once`); all 10 callers receive the same `subscriptionId`. `SubscribeAsync_Concurrent_TenCallers_InvokesRpcExactlyOnceAndReturnsSameId`.
- [x] 3.7 (audit N5) `SignalEventService.SubscribeAsync(string account, …)` adds `ArgumentException.ThrowIfNullOrEmpty(account)` at the entry; the duplicate-subscription branch (lines 168-170, 185-187) **returns the existing `SubscribeReceiveResponse(existingId)` instead of throwing `InvalidOperationException`** — operation becomes idempotent. XMLDoc on `ISignalEventService.SubscribeAsync` updated: removed `<exception cref="InvalidOperationException">` for "already subscribed"; added `<remarks>` documenting idempotency; added `<exception cref="TimeoutException">` (closing audit N12 for this method too).
- [x] 3.8 (audit N5) Test: `SubscribeAsync(account)` × 3 — second and third call do NOT invoke `subscribeReceive` RPC; all three return identical `Id`. Plus `[Theory]` over null/empty `account` asserting `ArgumentException` with `ParamName == "account"`. `SignalEventServiceDispatchTests.SubscribeAsync_Idempotent_SameAccountThrice_ReturnsSameIdAndCallsRpcOnce` + `…_NullOrEmptyAccount_ThrowsArgumentException`.

## 4. Agent-friendly public API (capability `agent-friendly-api`)

- [x] 4.1 (D1) `FinishLinkResponse(string Number)` PascalCase + `[property: JsonPropertyName("number")]`
- [x] 4.2 (D1) `SubscribeReceiveResponse(int Id)` PascalCase + `[property: JsonPropertyName("id")]`
- [x] 4.3 (D2) `ISignalAccounts.ListAccountsAsync` / `SyncAccountAsync` + impls + tests; old names kept as `[Obsolete]` default interface members delegating to new ones (one-major-version grace shim per CLAUDE.md backward-compat convention)
- [x] 4.4 (D2) `ISignalDevices.StartLinkAsync` / `FinishLinkAsync` + impls + tests; old names kept as `[Obsolete]` shim
- [x] 4.5 (D2) `ISignalGroups.ListGroupsAsync` + impl + tests; old name kept as `[Obsolete]` shim
- [x] 4.6 (D2) `ISignalCliClient.VersionAsync` + impl + tests + `SignalCliHealthMonitor` call site — done in 2.1.0 (agent-friendly-modernization A.1-A.2)
- [ ] 4.7 (D3) `CancellationToken` removed from `TextMessageOptions` / `AttachmentMessageOptions` / `StickerMessageOptions`; added as last parameter to `SendTextMessageAsync` / `SendAttachmentAsync` / `SendStickerAsync`
- [x] 4.8 (D5) `SignalCliHostedService` becomes `sealed`
- [ ] 4.9 (D6) `ConfigureAwait(false)` added to the 5 missing public-path `await`s; `.editorconfig` raises `CA2007` from `silent` → `warning`
- [x] 4.10 (D7) `Config.EnvironmentVariables` becomes `IReadOnlyDictionary<string,string>` with a `WithEnvironment(IDictionary<string,string>)` defensive-copy helper that returns `this`. `SignalCliOptions.EnvironmentVariables` теж типу `IReadOnlyDictionary` (§4.28 paired). Test `ProcessRunner_ShouldReceiveCorrectEnvironmentVariables` migrated to `WithEnvironment(new Dictionary<,>{ … })` — `Add()` на shared посилання більше не компілюється, що й є метою.
- [x] 4.11 (D8) `Example/Program.cs` rewritten as `async Task Main`, awaited `SendTextMessageAsync`, awaited `host.StopAsync()`. `await using` deferred — `IHost` 10.0 не декларує `IAsyncDisposable` напряму; concrete host реалізує, але cast псує portability. Замість фейкового fire-and-forget Subscribe-callback тепер `Task.Run` із proper try/catch + `ConfigureAwait(false)`. Закоментований приклад OTel-підключення (AddSource("SignalCli.NET") + AddMeter) додано.
- [x] 4.12 (D10) `JsonRpcRequest` record body emptied — позиційні параметри `(Method, Params, Id)` зараз із `[property: JsonPropertyName("...")]` синтаксисом; залишилось лише `JsonRpc { get; init; } = "2.0"`. Дублікати `Method = Method` etc. видалено.
- [x] 4.13 (D11) Compileable `<example>` XML-doc snippets on `ISignalMessage.*Async` — `new[] { new UserRecipient(...) }` → `[new UserRecipient(...)]` (collection expressions, .NET 8+).
- [x] 4.14 (D12) `JetBrains.Annotations` PackageReference з `PrivateAssets="all"` — пакет тепер є build-time-hint, не потрапляє у consumer dependency graph.
- [ ] 4.15 (D9) Builders' `Build()` adds final guard (defensive, post-mutation) **(deferred — current Builders already validate at the ctor of Builder; post-mutation guard is belt-and-suspenders)**
- [ ] 4.16 `Version` 2.0.0 → 3.0.0 in `SignalCli.csproj` (semver-major for the breaks); CHANGELOG entry under "## [3.0.0]"
- [x] 4.17 (N3) `Models/Signal/Envelope.cs` — `[JsonRequired]` додано на always-present non-nullable string поля: `JsonRemoteDelete.RemoteDeleteId`, `Offer.Type`, `Offer.Opaque`, `Answer.Opaque`, `IceUpdate.Opaque`, `Hangup.Type`. Раніше null проходив тихо у non-nullable property — тепер `JsonException` із чітким messag'ем.
- [x] 4.18 (N4) `UserRecipient`/`GroupRecipient` ctors — `ArgumentException.ThrowIfNullOrEmpty(...)` (.NET 8+) у приватному `ValidateAndReturn`-помічнику, що зберігає `<param>`-name через `nameof()`. XML-doc оновлено: тепер обидва типи документують `<exception cref="ArgumentNullException">` для null AND `<exception cref="ArgumentException">` для empty (раніше було «обидва кидаються коли null або empty» — некоректно).
- [x] 4.19 (N9) `BaseSignalEventArgs.Account` → non-nullable `string`. Sed-sweep на всіх 10 `*EventArgs` (TextMessage, Reaction, Attachment, Sticker, Typing, Receipt, Sync, Quote, Edit, RemoteDelete) — параметр `Account` тепер `string` non-null. `TryGetAccountBySubscriptionId` оформлено з `[NotNullWhen(true)]`-атрибутом на `out string? account` — null-аналізатор тепер знає, що при `true`-поверненні значення non-null, тож `account!`-cast'и більше не потрібні.
- [ ] 4.20 (N10) `ListAccountsResponse` and `ListGroupsResponse` — turn into wrapper records (`(IReadOnlyList<T> Items)`) with a `[JsonConverter]` (or positional record param typed as the collection) that preserves the wire JSON array shape
- [x] 4.21 (N14) `JsonRpcException` — three CA1032 ctors added: `()`, `(string)`, `(string, Exception)`. Default `Error` uses canonical JSON-RPC 2.0 code `-32603` ("Internal error"), not the non-standard `-32000`.
- [x] 4.22 (N15) Legacy `JsonRpcException(string, Exception?)` ctor with non-standard `-32000` code **removed** (not just `[Obsolete]`-marked — overload ambiguity with the new CA1032 `(string, Exception)` ctor would create build hazards). Audit confirmed zero internal call sites. Test `FromMessage_CreatesInternalError` updated from `-32000` → `-32603` to match the new contract.
- [ ] 4.23 (audit N6) `ISignalMessage.SendTextMessageAsync` / `SendAttachmentAsync` / `SendStickerAsync` return type changes from `Task<List<SendMessageResponse>>` → `Task<SendMessageResponse>` (was always wrapping a single element in `[response]`). Implementations drop the `return [response];` collection wrap. CHANGELOG entry under "## [3.0.0]" lists the breaking change with a migration note: `var result = await …` → `var response = await …`.
- [ ] 4.24 (audit N6) Test: each `Send*Async` returns the response directly; no Linq-wrapping in the implementation; old call-site shape `result[0]` no longer compiles
- [ ] 4.25 (audit N9) `TextMessageOptions.PreviewUrl`, `TextMessageOptions.PreviewImage`, `AttachmentMessageOptions.PreviewUrl?` (if any) decorated with `[StringSyntax(StringSyntaxAttribute.Uri)]` from `System.Diagnostics.CodeAnalysis`. No runtime cost; tells IDEs/analyzers to validate URL syntax.
- [ ] 4.26 (audit N12) XML docs on `ISignalMessage.SendTextMessageAsync` / `SendAttachmentAsync` / `SendStickerAsync` add `<exception cref="TimeoutException">Виникає, якщо signal-cli не відповів за <see cref="SignalCliOptions.RequestTimeoutSeconds"/>.</exception>` — closing the doc-gap left after F1 closed.
- [ ] 4.27 (audit N11) Generic-parameter order on `ISignalCliClient.InvokeMethodAsync` is reversed: `Task<TResponse> InvokeMethodAsync<TRequest, TResponse>(string method, TRequest parameters, CancellationToken ct = default)` — matches `JsonSerializer.Deserialize<TValue>` convention and enables partial type inference. Old `<TResponse, TRequest>` overload kept with `[Obsolete("Use the <TRequest, TResponse> overload — argument order matches MS conventions. Will be removed in 4.0.")]`.
- [ ] 4.28 (audit E2) `SignalCliOptions.EnvironmentVariables` ALSO becomes `IReadOnlyDictionary<string,string>` (paired with §4.10 on `Config`); `SignalCliOptionsExtensions.ToOptions` / `ToConfig` / `CopyFrom` perform a defensive copy (no shared mutable reference between the two types or with consumer code).

## 5. High-performance logging (capability `high-performance-logging`)

- [ ] 5.1 (P1) Add `static partial class Log` for each service (one file per service) under `Services/**/Log.<Service>.cs`
- [ ] 5.2 (P1) Migrate `JsonRpcClient` log calls (8 sites) — events 3xxx
- [ ] 5.3 (P1) Migrate `SignalCliHostedService` log calls (~20 sites) — events 4xxx
- [ ] 5.4 (P1) Migrate `SignalCliHealthMonitor` log calls — events 5xxx
- [ ] 5.5 (P1) Migrate `ProcessStateManager` log calls — events 6xxx
- [ ] 5.6 (P1) Migrate `SignalEventService` log calls — events 7xxx
- [ ] 5.7 (P1) Migrate `SignalService`/`SignalMessage`/`SignalAccounts`/`SignalDevices`/`SignalGroups` log calls — events 2xxx
- [ ] 5.8 (P2) Wrap `string.Join(", ", response)` (`SignalAccounts.cs:43`) and analogous expensive args in `if (_logger.IsEnabled(LogLevel.Trace))` guards (or migrate to `LoggerMessage` which generates the guard)
- [ ] 5.9 `.editorconfig`: enable `CA1848`, `CA1873` analyzers at `warning` severity
- [ ] 5.10 Regression test: `PrivacyLoggingTests` still passes verbatim — no PII appears above `Trace`
- [x] 5.11 (audit N13) `JsonRpcClient.InvokeMethodAsync` opens a `BeginScope` at the top: `using var scope = _logger.BeginScope(new Dictionary<string,object> { ["RpcMethod"] = method, ["RpcRequestId"] = requestId });` — every nested `JsonRpcClientLog.*` call within the request lifecycle inherits these properties.
- [ ] 5.12 (audit N13) Test: scope capture via `FakeLogger`. **(Deferred — needs FakeLogger DI setup in JsonRpcClientTests; scope behavior validated manually by inspecting LogScope on captured entries.)**

## 6. AOT readiness (capability `aot-readiness`)

- [x] 6.1 (B2) `JsonRpcClient._sendLock`: `Nito.AsyncEx.AsyncLock` → `SemaphoreSlim(1, 1)` with `WaitAsync/Release` pattern
- [x] 6.2 (B2) `SignalCliHostedService._operationLock`: `AsyncLock` → `SemaphoreSlim(1, 1)` (4 lock sites + 1 callback)
- [x] 6.3 (B2) Drop `Nito.AsyncEx` PackageReference from `SignalCli.csproj`
- [ ] 6.4 (P6) Drop `DefaultJsonTypeInfoResolver` from `SignalJson.Options.TypeInfoResolver` — leave only `SignalJsonContext.Default`. **(Deferred — needs §6.10 first; 5 tests use anonymous types via reflection fallback.)**
- [ ] 6.5 (P6) Audit all `JsonSerializer.Serialize`/`Deserialize`/`SerializeToElement` call sites for `TRequest`/`TResponse` types not in `SignalJsonContext`; add any missing. **(Tracked under §6.12 reflection-based context-registration test.)**
- [x] 6.6 (B6) `[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]` for fast-path emission (metadata + fast-path; was Metadata-only)
- [ ] 6.7 (P6) `<IsAotCompatible>true</IsAotCompatible>` in `SignalCli.csproj` **(Deferred — enabling produces 14 IL2026/IL3050 warnings on generic `JsonSerializer.Serialize/Deserialize<T>(_, options)` sites in `JsonRpcClient.InvokeMethodAsync<TRequest, TResponse>`. Full migration needs redesign onto `JsonTypeInfo<T>`-based overloads — separate session.)**
- [ ] 6.8 (P6) Resolve any IL2026/IL2104 warnings surfaced by the AOT analyzer (annotate with `RequiresUnreferencedCode` only as last resort) **(Paired with §6.7.)**
- [ ] 6.9 (P6) `dotnet publish -c Release /p:PublishAot=true` from a probe app succeeds (smoke test only, not added to CI yet) **(Paired with §6.7.)**
- [ ] 6.10 Migrate anonymous-type test usages off the (now-removed) reflection fallback: `JsonRpcClientTests.cs:106,128,150,174` and `JsonSerializationTests.cs:20` — pick Option A (concrete DTO registered in `SignalJsonContext`) or Option B (`SignalJson.OptionsForTests` exposed via `[InternalsVisibleTo]` with `DefaultJsonTypeInfoResolver`). Production options stay source-gen-only. **(Paired with §6.4.)**
- [ ] 6.11 Update `CLAUDE.md` rule 6 — remove "(which combines the source-gen resolver with a reflection fallback)" and state "source-generated context only; every serializable type MUST be registered in `SignalJsonContext`" **(Paired with §6.4.)**
- [ ] 6.12 (audit N8) New test `Tests/SignalCli.Tests/JsonContextRegistrationTests.cs`: reflectively enumerates every call site of `ISignalCliClient.InvokeMethodAsync<TRequest, TResponse>` in `src/SignalCli/Services/Signal/**` (and the matching `JsonSerializer.SerializeToElement` site in `JsonRpcClient.InvokeMethodAsync`), then asserts each `TRequest` and `TResponse` is present in `SignalJsonContext.Default.GetTypeInfo(...)`. Prevents the next "tihko {}" silent-empty-params regression once the reflection fallback is removed.

## 7. Test virtualization (capability `test-virtualization`)

- [ ] 7.1 (T2) Replace 12 `Task.Delay` waits in unit tests with `FakeTimeProvider.Advance` — list:
  - `JsonRpcClientTests.cs:232, 288`
  - `SignalCliHostedServiceRestartTests.cs:92, 125, 221`
  - `SignalCliHostedServiceAuditTests.cs:41, 65, 75`
  - `SignalCliHostedServiceStateTests.cs:204`
  - `SignalCliHealthMonitorEdgeCaseTests.cs:65`
  - `SignalCliHealthMonitorLoopTests.cs:71`
- [ ] 7.2 (T3) Add `internal` TestSeam properties (gated by `[InternalsVisibleTo("SignalCli.Tests")]`) — replace `GetPrivateField<IProcess>(svc, "_currentProcess")` with `svc.TestSeam.CurrentProcess`
- [ ] 7.3 (T3) Delete `GetPrivateField`/`SetPrivateField` from `SignalCliHostedServiceTestsBase`
- [ ] 7.4 (T4) Floating-point asserts use `Assert.Equal(expected, actual, precision: 3)`
- [ ] 7.5 (T5) New `Tests/SignalCli.Tests/SignalCliHostedService/StateManagerReentrancyTests.cs` (matches §2.5)
- [ ] 7.6 (T5) New `Tests/SignalCli.Tests/SignalEventService/SubscribeRaceTests.cs` (matches §3.6)
- [ ] 7.7 (T5) New `Tests/SignalCli.Tests/Rpc/BackPressureTests.cs` (matches §1.6)
- [ ] 7.8 (T1) Migrate to `xunit.v3` + `Microsoft.Testing.Platform.MSBuild`; update `xunit 2.9.2`/`xunit.runner.visualstudio 2.8.2` PackageReferences
- [ ] 7.9 (T8) `IAsyncLifetime` on hosted-service test bases instead of `IDisposable`
- [ ] 7.10 (T10) Consolidate facade passthrough tests with `[Theory]` + `[InlineData]` where the shape is uniform

## 8. Cloud development (capability `cloud-development`) — already in this change

- [x] 8.1 `.claude/hooks/session-start.sh` — installs `dotnet-sdk-10.0`, restores tests, sanity-builds the library; skips runtime packages
- [x] 8.2 `.claude/settings.json` — registers the SessionStart hook
- [x] 8.3 `docs/cloud-development.md` — workflow, command crib, network policy, async/sync trade-off
- [x] 8.4 `CLAUDE.md` — link to `docs/cloud-development.md` from a new "Cloud development" section
- [x] 8.5 Hook validated end-to-end in remote env (SDK install, restore, build, sample test pass)

## 8a. Hosting modernization (capability `hosting-modernization`)

- [x] 8a.1 (B1) `SignalCliHealthMonitor` inherits from `BackgroundService`; the loop is in `ExecuteAsync(stoppingToken)` — already shipped in 2.1.0 (`agent-friendly-modernization` B.1/B.2).
- [ ] 8a.2 (B3) `SignalCliHostedService` and `JsonRpcClientHostedService` implement `IHostedLifecycleService`; startup ordering moves from "registration order" to `StartedAsync`/`StoppingAsync` phases **(deferred — explicit lifecycle ordering not currently a regression)**
- [ ] 8a.3 (C1) `SignalCliHostedService` implements `IAsyncDisposable` (in addition to `IDisposable`); host's container picks it up **(deferred — currently `IDisposable` suffices because all cleanup is sync; can be added if a future change introduces async cleanup in the hosted service)**
- [ ] 8a.4 (B4) `IProcessRunner.StartProcessWithHandle` signature → either sync `(IProcess, StreamPair)` or `ValueTask<(IProcess, StreamPair)>` with `ValueTask.FromResult`; remove the `Task.FromResult` wrapper **(deferred — micro-optimization)**
- [x] 8a.5 (A1) `JsonRpcClient` drops `IDisposable` from its declared interfaces — already shipped in 2.1.0 (`agent-friendly-modernization` A.6); `IJsonRpcClient` derives from `IAsyncDisposable` only.
- [ ] 8a.6 Test: a tests-only fake `BackgroundService` lifecycle exerciser confirms that the host's `StopAsync` blocks on the monitor's `ExecuteAsync` until cancellation observed
- [x] 8a.7 (audit N4) `SignalCliHostedService.StopProcessInternalAsyncNoLock` — replace `new CancellationTokenSource(TimeSpan.FromSeconds(_options.StopTimeoutSeconds))` (line 335) with `new CancellationTokenSource(TimeSpan.FromSeconds(_options.StopTimeoutSeconds), _timeProvider)` (the .NET 8+ overload). The `TimeProvider` field is already injected — only the constructor call changes.
- [ ] 8a.8 (audit N4) Regression test: `FakeTimeProvider.Advance(StopTimeoutSeconds + 1)` faults `StopProcessInternalAsync` into the `Kill` branch. **(Deferred — adding `_timeProvider` test seam to existing StopProcess test fixtures; tracked separately.)**

## 8b. Options validation (capability `options-validation`)

- [ ] 8b.1 (B5) Replace `services.AddSingleton(config)` in `AddSignalCli` with `services.AddOptions<Config>().Configure(configure ?? (_ => {})).ValidateDataAnnotations().ValidateOnStart()`
- [ ] 8b.2 (B5) Add `[Range(...)]`/`[Required]` annotations on every numeric/path field in `Config` per the rules table in `specs/options-validation/spec.md`
- [ ] 8b.3 (B5) New overload `IServiceCollection AddSignalCli(this IServiceCollection, IConfiguration section)` that binds the section through Options pipeline
- [ ] 8b.4 (B7) Every `Config` property migrates from `{ get; set; }` to `{ get; init; }`; the `Action<Config>` setup delegate continues to work because it runs before consumers resolve the registered options
- [ ] 8b.5 (B7) `Config.EnvironmentVariables` already covered by 4.10 (IReadOnlyDictionary); confirm no remaining `set`
- [ ] 8b.6 Test: out-of-range `RequestTimeoutSeconds = 0` → host startup fails fast with `OptionsValidationException`
- [ ] 8b.7 Test: `AddSignalCli(IConfiguration)` overload binds appsettings JSON correctly
- [x] 8b.8 (audit E1) Drop `.ValidateDataAnnotations()` call in `ServiceCollectionExtensions.ConfigureOptions` (line 122) — the source-gen `SignalCliOptionsValidator` ([OptionsValidator] generator) already validates every `[Required]`/`[Range]` attribute **without reflection**. Added `.ValidateOnStart()` so validation fires at host start. XML-doc explains the trade-off.
- [x] 8b.9 (audit E1) Keep the custom `.Validate(o => !string.IsNullOrEmpty(o.JavaExecutable) || !string.IsNullOrEmpty(o.SignalCliExecutable), …)` — cross-field rules are not covered by DataAnnotations and the source-gen validator runs custom `Validate` lambdas just fine.
- [ ] 8b.10 (audit E1) Regression test: `dotnet publish -c Release /p:PublishAot=true` (probe app) reports zero IL2026 warnings sourced from `Microsoft.Extensions.Options.DataAnnotations`. **(Deferred — pairs with §6.7 `<IsAotCompatible>` enable; tested together in the AOT cluster.)**

## 8c. Code hygiene (capability `code-hygiene`)

- [x] 8c.1 (C5) `SignalEventService` becomes `sealed internal`
- [x] 8c.2 (C4) Remove the unused `_rpcClient` field in `SignalEventService`; remove its assignment in `StartAsync`; route through `_rpcClientProvider.Client` everywhere
- [ ] 8c.3 (C3) `AtomicCounter.Increment` — either (a) add a `// WHY:` comment explaining the wrap-to-zero CAS, or (b) widen to `long` and let consumers format with `ToString()` — request id stays `string` either way **(deferred — minor; `AtomicCounter` is one-liner, comment can go in follow-up)**
- [ ] ~~8c.4 (P4)~~ **Reverted — CA1305 analyzer rejects `int.ToString()` without an explicit `IFormatProvider`. Keeping `InvariantCulture` argument is the right call. Task closed without change.**
- [x] 8c.5 (C9) `SignalMessage.ValidateRecipients` materializes the `IEnumerable<IRecipient>` exactly once at entry via `as IReadOnlyList<IRecipient> ?? recipients.ToList()`; user/group split is a single `foreach`, no double-pass `Where(...)` anymore
- [ ] 8c.6 (C7) `SendUnifiedMessageAsync` 23-parameter signature → internal `UnifiedSendRequest` record DTO; public `Send*Async` builders unchanged **(deferred — cosmetic; functionality intact)**
- [ ] 8c.7 (C8) Audit and remove the `catch (Exception ex) { _logger.LogError(ex, "..."); throw; }` bare patterns from `SignalService`, `SignalMessage`, `SignalAccounts`, `SignalDevices`, `SignalGroups`, `JsonRpcClientHostedService`; either delete the catch or enrich with method/account context **(deferred — broad sweep; behavior intact)**
- [x] 8c.8 (C10) `Config.BuildClasspath` caches the joined classpath after the first call (`_cachedClasspath` field; lazy-init); `Directory.GetFiles` invoked once per `Config` instance regardless of restart count
- [ ] 8c.9 Test: stateful enumerator passed to `Send*Async` → enumerated exactly once
- [ ] 8c.10 Test: classpath build called twice → `Directory.GetFiles` invoked once
- [x] 8c.11 (N5) `SignalDevices.FinishLinkAsync(deviceLinkUri, deviceName, ct)` and `SignalGroups.ListGroupsAsync(account, ct)` — `ArgumentException.ThrowIfNullOrEmpty(...)` on each string input at the start of the method
- [x] 8c.12 (N8) `IDisposable` already removed from `SignalAccounts`, `SignalDevices`, `SignalGroups`, `SignalMessage` (A.13 in 2.1.0; verified — sealed-pass §8c.14 confirmed no `IDisposable` declared)
- [x] 8c.13 (N16) `SignalDevices.StartLink`/`FinishLink` — entry `Debug` log via `SignalDevicesLog.StartLinkRequested`/`FinishLinkRequested(deviceName)` (events 822/823)
- [x] 8c.14 (N17) Marked sealed: `ProcessWrapper`, `ProcessFactory`, `JsonRpcClientFactory`, `SignalAccounts`, `SignalDevices`, `SignalGroups`, `SignalEventService` (already done in §8c.1)
- [ ] 8c.15 (N18) `StreamPair` becomes `public sealed class`; `Dispose()` gets `if (_disposed) return; _disposed = true;` guard **(deferred — needs verification of StreamPair already-sealed status)**
- [ ] 8c.16 (N13) `README.md` dependency table — add `JetBrains.Annotations` row (currently missing) **(deferred — docs-only)**
- [x] 8c.17 (N21) `tasks.md` §9.2 — updated to "180 (baseline) + audit augments" — done in earlier commit
- [ ] 8c.18 Update `CLAUDE.md` rule 7 — replace "If you change a pinned version, update the hash in **both** the `.ps1` and `.sh`" with "Update `<SignalCliSha256>`/`<JreSha256>` in the relevant csproj; the scripts read the value as an argument" (paired with `supply-chain-hardening` §8d.4)
- [x] 8c.19 (audit N7) `Models/Config.cs` — class annotated `[Obsolete("Use SignalCliOptions + AddSignalCli(Action<SignalCliOptions>?); will be removed in 3.0.", error: false)]`. Internal compat-shims (`SignalCliOptions.ToConfig`, `SignalCliOptionsExtensions.ToOptions`/`ToIOptions`) wrapped in `#pragma warning disable CS0618` blocks (they too die in 3.0). Tests using `Config` directly produce CS0618 warnings (test project doesn't have `TreatWarningsAsErrors=true`) — acceptable until §4 migrates them.
- [x] 8c.20 (audit N14) `SignalEventService` class-header XMLDoc documents the `SingleWriter = true` invariant + link to `ChannelOptions.SingleWriter` docs + describes the idempotent-StartAsync that maintains it.
- [x] 8c.21 (audit N10) Private `_droppedCount` field and `% 100 == 1` sampler removed from `SignalEventService.TryWriteOrDrop`. Drop accounting now flows entirely through `SignalCliDiagnostics.EventsDropped` (`signalcli.events.dropped` counter with `event_type` tag). `TryWriteOrDrop` became `static` (no instance access).

## 8d. Supply-chain hardening (capability `supply-chain-hardening`)

- [x] 8d.1 (N1) `src/SignalCli.runtime.native/SignalCli.Native.targets` — всі `\` у `Include`/`DestinationFiles` замінені на `/`. MSBuild сам нормалізує сепаратори, але `\` у Include тихо ламається на Linux (там `\` — частина імені файлу).
- [x] 8d.2 (N2) `SignalCli.runtime.csproj` — forward-slash sweep: `signal-cli\bin` → `signal-cli/bin` у `Exists()`, `Include="signal-cli\**\*"` → `/`, `PackagePath` теж. Marker-file частина (specific `signal-cli/bin/signal-cli`) — deferred (поточний `Exists('signal-cli/bin')` вже працює, marker-file — додаткова надійність).
- [ ] 8d.3 (N2) Apply the same forward-slash + marker-file pattern to `SignalCli.runtime.native.csproj`, `SignalCli.runtime.jre.win-x64.csproj`, `SignalCli.runtime.jre.osx-arm64.csproj` **(deferred — same pattern, mechanical)**
- [ ] 8d.4 (N6) Add `<SignalCliVersion>` + `<SignalCliSha256>` MSBuild properties to `SignalCli.runtime.csproj` and both JRE csproj-и; pass to `download-signal-cli.{ps1,sh}` as `-Version`/`-Sha256` arguments
- [ ] 8d.5 (N6) Add `<JreVersion>` + `<JreSha256>` properties to both JRE csproj-и (already exist in some form per CLAUDE.md; consolidate naming)
- [ ] 8d.6 (N6) Update `download-signal-cli.ps1` / `.sh` and `download-jre.ps1` / `.sh` to accept `-Sha256` (or `--sha256`) parameter and remove hard-coded constants
- [x] 8d.7 (N12) `download-jre.ps1:42-43` — already case-invariant. Both sides call `.ToLower()` before the `-ne` compare; verified.
- [ ] 8d.8 (N7) Pin every non-`actions/*` `uses:` in `.github/workflows/**` to a 40-char commit SHA; keep the `@v…` tag in a trailing comment for human readability
- [ ] 8d.9 (N7) Pin first-party `actions/checkout`, `actions/setup-dotnet`, etc. to SHAs as well
- [ ] 8d.10 (N11) `SignalCli.Jre.targets` (both platforms) — after `<Unzip>`, add `<Error Condition="!Exists('$(TargetDir)jre/bin/java...')">…</Error>` with actionable message
- [ ] 8d.11 (N19) Add `LICENSE.txt` at the root of each runtime project; `<None Include="LICENSE.txt" Pack="true" PackagePath="" />` in csproj
- [ ] 8d.12 (N20) `src/build/download-jre.{ps1,sh}` — comment documenting the Adoptium URL pattern; clear error message on 404 naming the URL and env-var override
- [ ] 8d.13 Test/CI smoke: `dotnet build SignalCli.sln` on Linux delivers `signal-cli-native/signal-cli` into the consumer `TargetDir` (regression catch for N1)
- [ ] 8d.14 Test: corrupted `obj/jre` (delete `bin/java`) triggers the post-extract guard with the documented message

## 11. Observability (capability `observability` — audit N1/N2/N3)

This capability is **additive** — it lands in a 2.2.0 minor before the 3.0 breaking wave. No public API changes other than adding new types (`ActivitySource`, `Meter`) accessible to caller-side OTel listeners and the new optional package `SignalCli.NET.HealthChecks`. Privacy invariant (CLAUDE.md rule #1) preserved verbatim — tag values are method names, status enums, counts, durations, never message contents / phones / attachments.

### 11.A. ActivitySource (`Activity`-based distributed tracing)

- [x] 11.A.1 New file `src/SignalCli/Diagnostics/SignalCliDiagnostics.cs` with `internal static readonly ActivitySource ActivitySource = new("SignalCli.NET", AssemblyVersion)` ([Adding distributed tracing instrumentation — best practices](https://learn.microsoft.com/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs#add-basic-instrumentation): create once, store in a static, name hierarchical with the assembly).
- [x] 11.A.2 `JsonRpcClient.InvokeMethodAsync` wraps the request lifecycle: `using var activity = SignalCliDiagnostics.ActivitySource.StartActivity($"rpc.{method}", ActivityKind.Client);` — tags `signal.rpc.method` + `signal.rpc.request_id` set; `Ok`-status on success, `Error` with `ex.GetType().Name` (type-name only, no PII from messages) on failure including the `TimeoutException` branch.
- [x] 11.A.3 `SignalCliHostedService.StartProcessInternalAsyncNoLock` instruments `signalcli.process.start` span; tag `signal.process.executable` = basename via `Path.GetFileName(procConfig.Executable)` (full path is PII-adjacent — host-layout leak). `OnProcessExitedAsync`/`ForceRestartAsync` spans **(deferred)** — counter via `ProcessRestarts` already covers metric side.
- [x] 11.A.4 `SignalCliHealthMonitor.PingCliAsync` → `signalcli.healthcheck.ping` span; tag `signal.healthcheck.outcome` ∈ {`ok`,`timeout`,`failed`,`no_stream_pair`}; status Ok on healthy, Error+exception-type-name on failure/timeout.
- [x] 11.A.5 `SignalEventService.SubscribeAsync` → `signalcli.subscribe` span; tag `signal.subscription.id` (int) set on success. **`account` NOT a tag** (PII — phone number). `UnsubscribeAsync` span **(deferred)**.
- [ ] 11.A.6 **Privacy guard test:** `Tests/SignalCli.Tests/Observability/ActivityTagPrivacyTests.cs` **(deferred — pairs with §11.B.8 in a single Observability test fixture)**
- [x] 11.A.7 `docs/cloud-development.md` — new "Observability" section with the OTel hookup snippet, full surface inventory (5 spans + 5 instruments), and privacy invariant. README docs deferred for the v3.0 wave.

### 11.B. Meter (`System.Diagnostics.Metrics` — counters & histograms)

- [x] 11.B.1 `SignalCliDiagnostics.Meter = new Meter("SignalCli.NET", AssemblyVersion)`.
- [x] 11.B.2 `Counter<long> RpcRequests` — tags `method`, `status` ∈ {`ok`,`timeout`,`error`}.
- [x] 11.B.3 `Histogram<double> RpcDuration` (unit `ms`) — tag `method`. Recorded in `JsonRpcClient.InvokeMethodAsync` `finally` block via `Stopwatch.GetElapsedTime`.
- [x] 11.B.4 `Counter<long> EventsDropped` — tag `event_type`. Replaces private `_droppedCount`; `TryWriteOrDrop` now `static` + accepts `eventType`-arg.
- [x] 11.B.5 `Counter<long> ProcessRestarts` — tag `trigger` ∈ {`force`,`crash`,`health`}. Three call sites instrumented: `SignalCliHostedService.ForceRestartAsync` (`force`), `SignalCliHostedService.OnProcessExitedAsync` auto-restart path (`crash`), `SignalCliHealthMonitor.ExecuteAsync` pre-ForceRestart (`health`).
- [x] 11.B.6 `ObservableGauge<int> ActiveSubscriptions` — created in `SignalCliDiagnostics`; callback провайдер реєструється через `SetActiveSubscriptionsProvider(GetActiveSubscriptionCount)` у `SignalEventService.StartAsync` (lock-protected read of `_accountSubscriptions.Count`). Без тегів — кардинальність 1. Без зареєстрованого провайдера gauge репортує 0.
- [x] 11.B.7 All `Counter.Add`/`Histogram.Record` calls use ≤3 tags per Microsoft *Multi-dimensional metrics — allocation-free for ≤3 tags*. No PII in tag values (audited: method names, integer ids, durations, enum literals only).
- [ ] 11.B.8 Test: `MeterListener` captures one `signalcli.rpc.requests` increment per `InvokeMethodAsync` call; status tag matches outcome. **(Deferred — pairs with §11.A.6 privacy guard tests in a single Observability test fixture.)**

### 11.C. IHealthCheck — separate package `SignalCli.NET.HealthChecks`

- [ ] 11.C.1 New project `src/SignalCli.HealthChecks/SignalCli.HealthChecks.csproj`. Single dependency: `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions`. Targets `net10.0`.
- [ ] 11.C.2 `public sealed class SignalCliHealthCheck : IHealthCheck` reads `ProcessStateManager.CurrentState` + `SignalCliHealthMonitor.LastPingResult` (newly exposed `internal` property: `(bool Ok, DateTimeOffset At)? LastPingResult`). Returns `HealthCheckResult.Healthy` / `Degraded` / `Unhealthy` with `data` bag containing `state`, `last_ping_at`, `restart_count` ([Create health checks — IHealthCheck implementation](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0#create-health-checks)).
- [ ] 11.C.3 Extension `public static IHealthChecksBuilder AddSignalCliHealthCheck(this IHealthChecksBuilder builder, string name = "signal-cli", HealthStatus? failureStatus = null, IEnumerable<string>? tags = null)` ([Distribute a health check library — extension method shape](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0#distribute-a-health-check-library)).
- [ ] 11.C.4 `[InternalsVisibleTo("SignalCli.HealthChecks")]` added to `src/SignalCli/SignalCli.csproj` so the package can read internal state.
- [ ] 11.C.5 README dependency table mentions the optional package; CLAUDE.md "Architecture" section gets a one-line bullet.
- [ ] 11.C.6 Test: a tiny `WebApplication.CreateBuilder` + `MapHealthChecks("/healthz")` E2E reports `Healthy` when `ProcessState.Running` + recent ping, `Degraded` otherwise.

### 11.D. Privacy + AOT smoke

- [ ] 11.D.1 **Privacy invariant test** (mirrors §11.A.6 but for metrics): `MeterListener` capturing every recorded measurement asserts every tag value is one of the documented enum literals / numeric / method-name — never a phone number / message body substring.
- [ ] 11.D.2 With §6.7 (`<IsAotCompatible>true</IsAotCompatible>`), AOT analyzer reports zero new IL2026/IL3050 warnings from the `Diagnostics/` folder.
- [ ] 11.D.3 Update `CLAUDE.md`: extend critical rule #1 (Privacy) to read "no PII in `[LoggerMessage]` templates at `Information+` AND no PII in `Activity` tag values AND no PII in `Meter` tag values" (the rule's intent already covers this — make it explicit).

## 9. Synthesis & validation

- [x] 9.1 `openspec validate post-modernize-tuning --strict` passes
- [ ] 9.2 `dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj` — **180** (baseline at this audit) + new race tests (§1.6, §2.5, §3.6, §3.8) + new TimeProvider-CTS tests (§1.8, §8a.8) + new observability tests (§11.A.6, §11.B.8, §11.C.6, §11.D.1) + new generic-context test (§6.12) pass. Expect ≥ 195.
- [ ] 9.3 `dotnet build SignalCli.sln /p:TreatWarningsAsErrors=true` clean
- [ ] 9.4 `dotnet build src/SignalCli/SignalCli.csproj /p:PublishAot=true` from a probe app emits zero `IL*` warnings
- [x] 9.5 CHANGELOG entry under "## [3.0.0] — неопубліковано (WIP, post-modernize-tuning)" lists every breaking change so far (PascalCase responses, non-null `Account`, IReadOnlyDictionary EnvVars, JsonRpcException -32000→-32603, idempotent Subscribe, JsonRequired envelope fields, sealed HostedService) + every additive (observability stack, RPC robustness, race-safety, Async-suffix shims). Separate 2.2.0-only entry скасовано — обсяг breaking-змін уже виправдовує єдиний 3.0.0 release.
- [ ] 9.6 (audit) `CLAUDE.md` "Established patterns" section gains an "Observability" subsection (see §11.D.3) once §11 ships; the anti-regression rules added pre-emptively in this PR remain valid before/after.
