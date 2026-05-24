# Changelog

Формат заснований на [Keep a Changelog](https://keepachangelog.com/),
проєкт дотримується [семантичного версіонування](https://semver.org/lang/uk/).

## [4.0.1] — 2026-05-24

Patch: завершує **усі чотири "Pending follow-up"** позиції з [4.0.0] — включаючи 4 з 5 integration-tests-expansion E2E, які раніше вважалися non-feasible без CI runtime. Нульові breaking changes — лише hardening, observability test-coverage, реальна активація AOT-friendly configuration-binding, і skip-gated E2E повного set'у.

### 🐛 Виправлено

#### Capability `configuration-binder-aot-completion`

- **`AddSignalCli(IConfiguration)` тепер AOT-safe — `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]` атрибути зняті.** Корінь проблеми в 4.0.0: `<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>` згадувався у CHANGELOG-ентрі, але **не був доданий** у `SignalCli.csproj`. Без активного source-generator'а `OptionsBuilder.Bind` справді залишався reflection-based і attribute мав сенс. У 4.0.1 флаг нарешті присутній — source-gen intercepts `OptionsBuilderConfigurationExtensions.Bind` call-site і substitutes reflection-free generated binder (per [Microsoft Docs — Configuration source generator](https://learn.microsoft.com/dotnet/core/extensions/configuration-generator): *"all APIs that eventually call into these various binding methods are intercepted and replaced with generated code"*). AOT-targeting consumers тепер можуть використовувати overload без warning'ів.

#### Capability `safe-filename-hardening`

- **`AttachmentEntry.SafeFileName` тепер фільтрує control-чартки і bidi-override-маркери.** Окрім path-traversal-захисту (Path.GetFileName, був з 1.0), додатково відкидає:
  - **Control characters** U+0000..U+001F + U+007F (DEL). NUL byte у середині імені файлу труньчить на багатьох ОС і дозволяє "невидиме" розширення.
  - **Bidi-override / formatting** U+202A..U+202E (LRE/RLE/PDF/LRO/RLO) + U+2066..U+2069 (LRI/RLI/FSI/PDI). Класична UI-spoofing атака: `evil<U+202E>gpj.exe` у Explorer відображається як `evilexe.jpg`, користувач думає що відкриває картинку.
  - **`Path.GetInvalidFileNameChars()`** — крос-платформенний union (Windows строгіший).
  - Реалізація через `System.Buffers.SearchValues<char>` (zero-alloc fast-path на чистих іменах) + посимвольну фільтрацію (з stack-buffer ≤256 chars, heap-fallback інакше).
- За повністю-небезпечного імені (всі символи відфільтровані) — fallback на літерал `"attachment"`.

### 🛡️ Захист від регресій

- **`AttachmentEntryTests` розширено з 3 → 12 тестів** (×4): NUL byte у середині, U+202E RLO стрипінг, кожен з 9 bidi/control-символів (Theory), повністю-небезпечне ім'я, `SaveToTempFile` re-entry (`InvalidOperationException`), heap-buffer-path при іменах > 256 chars.
- **`ObservabilityCounterTests` (3 нових)** — раніше privacy-guards перевіряли лише *відсутність* PII у тагах, а *factual increment* counter'ів був untested invariant у CLAUDE.md "Future development guardrails". Тепер закрите:
  - `RpcDuration_OnSuccessRoundTrip_RecordsPositiveDurationAndOkStatus` — happy-path round-trip через `JsonRpcClient` фіксує `signalcli.rpc.duration` + `signalcli.rpc.requests{status=ok}` через MeterListener.
  - `EventsDropped_OnChannelOverflow_IncrementsWithCorrectEventType` — прокидає 1100 typing-нотифікацій (capacity=1024, no consumer) → асертить `signalcli.events.dropped{event_type=typing}` тікнув exactly 76 разів.
  - `ProcessRestarts_OnForceRestart_IncrementsWithForceTrigger` — викликає `ForceRestartAsync()` → асертить `signalcli.process.restarts{trigger=force}` тікнув ≥1.
- **`SyncDisposeDuringCleanupTests` (2 нових)** — пінує `field-barrier-hardening` invariant з [4.0.0]: `SignalCliHostedService.Dispose()` sync-path дренує `_operationLock` із 50ms fallback'ом і не дедлокає навіть з held-lock. Перший тест тримає семафор через reflection і викликає `Dispose()` синхронно; другий — happy-path lock-free assertion.
- **`SignalCliE2EAdditionalTests` (5 нових E2E, skip-gated)** — закриває останній follow-up з [4.0.0] ("4 з 5 integration-tests-expansion E2E require real signal-cli runtime"). Той самий runtime-availability gate що `SignalCliE2EVersionTests` / `SignalCliE2EGracefulShutdownTests` — без bundled JRE / native бінарника тести return early з `[SKIP] …` маркером у Console.Error; інакше виконують повний E2E:
  - `Process_StartStopRestart_TransitionsObservedCorrectly` — повний цикл Start → ForceRestart → Stop, асерти `ProcessStateManager.CurrentState` transitions + unique PID між циклами.
  - `Process_KilledExternally_AutoRestartReclaimsProcess` — `Process.Kill()` real signal-cli, чекаємо до 60с watchdog tick, асертимо новий PID + новий процес відповідає на `version`.
  - `HealthMonitor_OverInterval_LastPingResultUpdates` — `HealthCheckIntervalSeconds=2`, ждемо 6с, асертимо `LastPingResult.Ok==true` + timestamp fresh (<10s old).
  - `DisposeAsync_MidFlight_LeavesNoOrphanProcess` — fire `VersionAsync` паралельно з `host.DisposeAsync()`, чекаємо до 5с race-window, асертимо `Process.GetProcessById(pid).HasExited`.
  - `Configuration_FromIConfiguration_StartsRealCli` — validates `configuration-binder-aot-completion` end-to-end: `AddSignalCli(IConfiguration)` overload + `InMemoryCollection`-bound options → real signal-cli start → `version` returns "0.14.x".

### 🛠 Інше

- `<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>` нарешті у [`SignalCli.csproj`](src/SignalCli/SignalCli.csproj) — фікс root-cause проблеми з 4.0.0.
- Test count: unit 254 → 273 (+19); integration 2 → 7 (+5 skip-gated E2E).

### Pending follow-up

_(нічого — всі чотири follow-up'и з [4.0.0] повністю закриті)_

---

## [4.0.0] — 2026-05-24

Третя велика хвиля — фокус на завершенні deprecated-shim-cycle, типізації RPC-помилок, і correctness-fix'у graceful-shutdown. Cargo з трьох OpenSpec changes: `audit-followup-2026` + `signal-cli-protocol-alignment` + `deprecated-shim-removal`. **Містить breaking changes**; повна migration table нижче.

### ⚠️ Breaking (v4.0)

**Усе in-flight-to-4.0 з CLAUDE.md "Backward compatibility convention" нарешті видалено.** Один-мажорний-грейс зреалізовано.

- **`SignalCli.Models.Config`** клас повністю видалено. Заміна — `SignalCliOptions` (вже існував з 2.1.0). Resolver-логіка (`Config.ResolveBundledJava`, `ResolveOnPath`, `TryResolveJavaPath`) переїхала в новий internal `SignalCli.Utilities.JavaPathResolver`. `Config.ToProcessConfig` логіка переїхала в `SignalCli.Models.SignalCliOptionsExtensions.ToProcessConfig` (internal extension). Classpath кеш скасовано (1 виклик на life-cycle — overhead negligible).
- **`ServiceCollectionExtensions.AddSignalCli(Action<Config>?)`** overload видалено. Заміна для consumer'ів пакетів `SignalCli.Runtime.Jre.*` — новий `AddSignalCliWithBundledRuntimeDefaults(Action<SignalCliOptions>?)` extension, що wires auto-resolve (bundled JRE/JAVA_HOME/PATH) через `JavaPathResolver` + дає consumer override-hook через delegate. Без bundled-runtime — `AddSignalCli(Action<SignalCliOptions>?)` напряму.
- **`SignalCliOptionsExtensions.ToOptions(Config)` / `ToIOptions(Config)` adapters** видалено разом з Config-типом.
- **`ServiceCollectionExtensions.CopyFrom(SignalCliOptions, SignalCliOptions)` helper** видалено (був тільки для legacy `Action<Config>?`-flow).
- **`SignalCliOptions.ToConfig()` shim** видалено.
- **`ISignalCliClient.Version()`** DIM shim видалено. Заміна — `VersionAsync()`. Migration: `s/\.Version(/\.VersionAsync(/g`.
- **`ISignalAccounts.ListAccounts` / `SyncAccount`** DIM shims видалено. Migration: `s/\.ListAccounts(/\.ListAccountsAsync(/g`, `s/\.SyncAccount(/\.SyncAccountAsync(/g`.
- **`ISignalDevices.StartLink` / `FinishLink`** DIM shims видалено. Migration: `s/\.StartLink(/\.StartLinkAsync(/g`, `s/\.FinishLink(/\.FinishLinkAsync(/g`.
- **`ISignalGroups.ListGroups`** DIM shim видалено. Migration: `s/\.ListGroups(/\.ListGroupsAsync(/g`.

### ✨ Додано

#### Capability `typed-rpc-errors` (раніше `signal-cli-protocol-alignment`)

- Новий enum **`SignalCli.Exceptions.JsonRpcErrorCode`** з 10 значеннями: 5 JSON-RPC 2.0 standard (`ParseError -32700`, `InvalidRequest -32600`, `MethodNotFound -32601`, `InvalidParams -32602`, `InternalError -32603`) + 5 signal-cli specific (`UserError -1`, `IoError -3`, `UntrustedIdentity -4`, `RateLimit -5`, `CaptchaRejected -6`). Цитується до `SignalJsonRpcCommandHandler.java:35-280` @ signal-cli bda4e7f.
- Нова public property **`JsonRpcException.KnownCode { get; }`** — типізована мапа з `Error.Code` на `JsonRpcErrorCode?` (null для unknown codes — forward-compat).
- Два нові derived exception types: **`RateLimitException`** (code -5) і **`UntrustedIdentityException`** (code -4) — для consumer-actionable error-кодів які типічно catch'аться by type (retry-with-backoff / verify-safety-number). `JsonRpcClient.InvokeMethodAsync` тепер кидає derived типи коли wire-code співпадає; інші коди лишаються базовим `JsonRpcException`.

#### Capability `config-auto-resolve-migration`

- Нове public extension **`ServiceCollectionExtensions.AddSignalCliWithBundledRuntimeDefaults(Action<SignalCliOptions>? = null)`** — replacement для legacy `AddSignalCli(Action<Config>?)` що робив auto-resolve через `Config.CreateDefault()`. Wires `AppHome = AppContext.BaseDirectory`, `LibDirectory = "SignalCli/lib"`, `JavaExecutable` resolved через bundled JRE → JAVA_HOME → Windows Oracle → PATH. Consumer override приходить пізніше через delegate.

### 🐛 Виправлено

#### Capability `graceful-shutdown-fix` (critical correctness bug, в.чав з 1.0)

- **`SignalCliHostedService.StopProcessInternalAsyncNoLock` тепер закриває stdin** (`StandardInput.Close()`) замість того щоб писати літеральне `"exit"` як рядок. signal-cli не має JSON-RPC методу `exit` і парсить кожен stdin-рядок як JSON (`JsonRpcReader.java:59-75` @ bda4e7f) — наш літерал виробляв `-32700 Parse error` response на stdout, процес ЗАЛИШАВСЯ ЖИВИЙ, наш wait-for-exit timeout вистрілював, і ми завжди falled through до `Kill(entireProcessTree: true)`. **КОЖЕН** graceful shutdown був насправді hard-kill (TerminateProcess на Win, SIGKILL на Unix), bypass'ачи signal-cli shutdown hooks → потенційна SQLite corruption. Fix: stdin EOF — signal-cli reader-loop природньо завершується, dispatcher finally clears subscriptions, JVM exit clean.
- **`SignalCliHostedServiceLog.ExitWriteFailed`** видалено, заміна — **`StdinCloseFailed`** (тей самий EventId 117, Debug-level).

#### Capability `addsignalcli-idempotency-fix`

- **`ServiceCollectionExtensions.AddSignalCli` нарешті дійсно idempotent.** Pre-fix guard `services.Any(d => d.ServiceType == typeof(IOptions<SignalCliOptions>) || d.ServiceType == typeof(SignalCliOptions))` НІКОЛИ не fire'ив, бо `IOptions<T>` зареєстровано open-generic а не concrete. Repeated `AddSignalCli` calls (a) re-run'или configure delegate (second-wins), (b) додавали 3 duplicate `IHostedService` descriptor'и → подвійний startup. CHANGELOG `[3.0.0]` декларація idempotency була over-broad. Fix: private sentinel-type `SignalCliRegistrationMarker` реєструється на першому виклику, перевіряється на наступних. Now correct for the first time.

#### Capability `badge-url-fix`

- README.md coverage badges і 4 emission sites у `.github/workflows/dotnet-desktop.yml` тепер використовують absolute `https://raw.githubusercontent.com/07artem132/SignalCli.NET/main/.github/badges/*.svg` URLs. Relative paths `.github/badges/*.svg` працювали тільки на github.com — інші renderers (NuGet.org, IDE previewers, third-party gallery sites) інтерпретували `.github` як hostname і виробляли broken `http://.github/badges/*.svg`.
- `SignalCli.csproj` тепер має `<PackageReadmeFile>README.md</PackageReadmeFile>` + `<None Include="..\..\README.md" Pack="true" PackagePath="\" />`. Build warning *"The package SignalCli.NET.x.x.x is missing a readme"* зник; README тепер у NuGet pack.

### 🛡️ Defensive

#### Capability `json-hardening`

- **`SignalJson.Options.AllowDuplicateProperties = false`** (новий .NET 10 flag). signal-cli response з duplicate-key — protocol violation per JSON-RPC 2.0; раніше `System.Text.Json` мовчки слідував last-wins; тепер `JsonException` fire'ить при deserialization.

#### Capability `attachment-threshold-margin`

- **`SignalMessage.MaxInlineEncodedAttachmentBytes` знижено з 15M → 12M**. signal-cli's Jackson 2.20.2 enforces `StreamReadConstraints.maxStringLength = 20_000_000` per STRING TOKEN (`gradle/libs.versions.toml:10` @ bda4e7f). base64 inflation 4/3: 12M raw × 4/3 = 16M encoded → 4M margin для решти `send` JSON envelope. Old 15M давало 20M encoded — exactly at cap, zero margin → occasional StreamConstraintsException на attachments близьких до межі.

#### Capability `field-barrier-hardening`

- **`JsonRpcClientHostedService._client`** змінено на `volatile IJsonRpcClient?`. Field читається з кількох потоків (`SignalCliHealthMonitor.PingCliAsync`, `SignalEventService.StartAsync`) без локу — на x64 reference-read атомарний, але на ARM64 (.NET 10 first-class) без acquire/release-семантики reader міг би побачити stale null. `volatile` додає memory barrier з nullov-runtime-cost на x64.
- **`SignalCliHostedService.Dispose()` sync path** тепер бере `_operationLock.Wait(TimeSpan.FromMilliseconds(50))` перед `DisposeCore()` — синхронізує read `_currentProcess` з write'ами в `CleanupProcess` (під lock-finally у `StopProcessInternalAsyncNoLock`). 50ms drain timeout — worst case identical to pre-fix.

### 🛠 Інше

- **`<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>`** у `SignalCli.csproj` — допомагає source-gen-перехоплюваним configuration-binding call-site'ам у внутрішніх шляхах. **АЛЕ** `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` на `AddSignalCli(IConfiguration)` довелося ЛИШИТИ: `OptionsBuilder.Bind<T>(IConfiguration)` сам framework-annotated у `Microsoft.Extensions.Options.ConfigurationExtensions`, і source-gen цей call-site не перехоплює. Full AOT-fix для overload'у — окремий PR з rewrite binding path. AOT-targeting consumers MUST use `AddSignalCli(Action<SignalCliOptions>?)`.
- **CLAUDE.md** отримав новий H2 розділ **"signal-cli protocol behavior we depend on"** з 7 cited facts про upstream signal-cli (stdin EOF graceful, stdout pure-JSON line-flushed, virtual-thread parallel dispatch, `subscribeReceive` non-idempotent at protocol level, Jackson `maxStringLength = 20M`, custom error codes `-1..-6`, Java 25 requirement). Кожен факт pin'ить до signal-cli source file:line @ commit bda4e7f. Bumping `<SignalCliVersion>` має сопровождатися re-verify-pass за тими ж facts.
- 6 stale `[Obsolete("…will be removed in 3.0")]` повідомлень переписано на `4.0` (codebase уже був 3.0.0 коли message казало 3.0 — drift trained agents to disbelieve `[Obsolete]` lifetime claims).
- Three new **regression-guard tests** під `Tests/SignalCli.Tests/RegressionGuards/`:
  - **`ObsoleteMessageConsistencyTests`** — reflectively scans every `[Obsolete]` attribute, parses `"will be removed in N.0"`, asserts N > current major. Drift class неможлива going forward.
  - **`EventIdBlockTests`** — Theory × 12 `*Log.cs` classes, asserts EventId lies в reserved block per CLAUDE.md "Established patterns → Logging".
  - **`PublicApiSurfaceTests`** — reflective walker generates canonical-form line per public member, diffs against `SignalCli.public-api.txt` baseline (1087 lines after this release). Будь-який accidental public-API drift — fail з unified diff.
- Edge-case test coverage додано: `AtomicCounter` int32 wrap, JSON-RPC `error.data` field preservation, `JsonRpcResponse` with both `result` and `error`, attachment encoded-size boundary, EnvironmentVariables read-only-snapshot semantics, AddSignalCli idempotency × 3.
- Integration E2E `SignalCliE2EGracefulShutdownTests` валідує `graceful-shutdown-fix` через real signal-cli runtime (skip-gated like existing `SignalCliE2EVersionTests`).
- Test count: 215 baseline → 254 (+39 net new).
- Race-prober `Client_ConcurrentAccessUninitialized_DoesNotThrowNullRef` (50 parallel readers, JsonRpcClientHostedService) — пінує volatile-семантику.
- Example `Program.cs` тепер використовує typed lambda parameter `(SignalCliOptions o) => {...}` замість cast — overload resolution однозначний без надмірного annotation noise.
- **CLAUDE.md "Future development guardrails"** секція документує що ще лишилося як untested invariants (NUL/RTL filename sanitization, ForceRestart no-op states, NotificationChannelCapacity=1 boundary, observability counter increment assertions, SubscribeAsync leader-cancelled propagation) для майбутніх PRs.

### Pending follow-up

_(усі чотири позиції повністю закриті у [4.0.1] — див. вище)_

- ✅ Configuration-binder full AOT fix → `configuration-binder-aot-completion` (4.0.1).
- ✅ 4 з 5 integration-tests-expansion E2E → `SignalCliE2EAdditionalTests` (5 skip-gated tests, 4.0.1).
- ✅ 6 з 12 edge-case-coverage tests → `safe-filename-hardening` + `observability-counter-assertions` (4.0.1).
- ✅ 1 з 2 race-prober tests → `SyncDisposeDuringCleanupTests` (4.0.1).

---

## [3.0.0] — 2026-05-24

Друга велика хвиля модернізації — фокус на correctness/observability/agent-friendly-API. **Містить breaking changes**, перерахованих нижче.
Реалізація триває; цей розділ оновлюється у міру викочування кластерів. Див. `openspec/changes/post-modernize-tuning/`.

### ⚠️ Breaking

- `FinishLinkResponse.number` → **`FinishLinkResponse.Number`** (PascalCase property; JSON wire-name збережено через `[JsonPropertyName("number")]`).
- `SubscribeReceiveResponse.id` → **`SubscribeReceiveResponse.Id`** (так само PascalCase).
- `BaseSignalEventArgs.Account` тепер `string` (non-nullable). Те ж саме поширено на всі 10 `*EventArgs`-records. Раніше було `string?`, що змушувало кожного підписника null-чекати гарантовано-присутнє значення.
- `Config.EnvironmentVariables` і `SignalCliOptions.EnvironmentVariables` тепер `IReadOnlyDictionary<string,string>` на читання. Для мутації — `Config.WithEnvironment(IDictionary<string,string>)` (defensive copy + fluent return). Раніше можна було `.Add(key, value)` на shared посилання після DI-capture.
- `JsonRpcException(string, Exception?)` ctor з нестандартним кодом **-32000** видалено. Замість нього — три CA1032-стандартні ctors: `()`, `(string)`, `(string, Exception)` — усі з канонічним JSON-RPC 2.0 кодом **-32603** ("Internal error"). Консумери, що каталі legacy-конструктор, мають мігрувати на CA1032-ctors або передавати власний `JsonRpcError`.
- `SignalEventService.SubscribeAsync(account)` тепер **ідемпотентний** — повторні виклики для того самого облікового запису повертають той самий `subscriptionId` без RPC. Раніше другий виклик кидав `InvalidOperationException`. `catch (InvalidOperationException) when (msg.Contains("вже підписаний"))` більше не зловить — операція тепер успіх.
- `JsonRequired` на always-present полях `Envelope.cs`: `JsonRemoteDelete.RemoteDeleteId`, `Offer.Type`/`Offer.Opaque`, `Answer.Opaque`, `IceUpdate.Opaque`, `Hangup.Type`. Якщо signal-cli колись поверне ці поля з `null` — десеріалізація фолтиться з `JsonException` замість тихо пропустити `null` у non-nullable property.
- `UserRecipient`/`GroupRecipient` ctor: null → `ArgumentNullException`, empty → `ArgumentException`. Раніше empty теж кидав `ArgumentNullException` (порушення контракту обох типів).
- `SignalCliHostedService` тепер `sealed` — інхеріт не підтримується.
- Стандартний шлях `dotnet publish /p:PublishAot=true` ще не enable'нений (deferred — потребує redesign на `JsonTypeInfo<T>` overloads), але всі предумови (drop Nito.AsyncEx, drop `.ValidateDataAnnotations()`, source-gen JSON fast-path) на місці.
- **(round 9 §4.7)** `CancellationToken`-property + `WithCancellationToken`-Builder-method видалено з `TextMessageOptions` / `AttachmentMessageOptions` / `StickerMessageOptions`. Єдиний шлях скасування — параметр `Send*Async(options, cancellationToken)`. `[Obsolete]`-shim після одного major-релізу (як обіцяно в CLAUDE.md "Backward compatibility convention"). Migration: `.WithCancellationToken(ct).Build(); → .Build();` + передати `ct` другим аргументом.
- **(round 9 §4.23/4.24)** `ISignalMessage.{SendText,SendAttachment,SendSticker}MessageAsync` повертають `Task<SendMessageResponse>` (single response), а не `Task<List<SendMessageResponse>>` — все одно завжди було `[response]`-wrap. Migration: `(await SendTextMessageAsync(opts))[0] → await SendTextMessageAsync(opts)`.
- **(round 9 §4.27)** Generic-параметри `InvokeMethodAsync` поміняли порядок: `<TResponse, TRequest>` → `<TRequest, TResponse>` на `ISignalCliClient`, `IJsonRpcSender`, обох impls + ~22 callsites. Узгоджено з `JsonSerializer.Deserialize<TValue>`-конвенцією. **Shim неможливий** — C# не розрізняє overload'и за порядком typeparam'ів (same runtime signature). Migration: розверни `<X, Y>` → `<Y, X>` на кожному виклику.

### ✨ Додано

#### Observability (capability `observability`)
- Єдиний `internal static readonly ActivitySource SignalCliDiagnostics.ActivitySource = new("SignalCli.NET", AssemblyVersion)` — спани `rpc.<method>`, `signalcli.process.start`, `signalcli.healthcheck.ping`, `signalcli.subscribe`. Теги: method name, status enum, integer id, exception type name — без PII.
- Єдиний `internal static readonly Meter SignalCliDiagnostics.Meter = new("SignalCli.NET", AssemblyVersion)`:
  - `Counter<long> signalcli.rpc.requests` (теги `method`, `status` ∈ {`ok`,`timeout`,`error`})
  - `Histogram<double> signalcli.rpc.duration` (мс, тег `method`)
  - `Counter<long> signalcli.process.restarts` (тег `trigger` ∈ {`force`,`crash`,`health`})
  - `Counter<long> signalcli.events.dropped` (тег `event_type` ∈ 10 значень) — замінює приватний `_droppedCount`.
  - `ObservableGauge<int> signalcli.subscriptions.active`.
- Документація: `docs/cloud-development.md` має нову секцію Observability з drop-in OTel-snippet.

#### RPC robustness
- `SignalCliOptions.NotificationChannelCapacity` (default 1024). Між stdout-парсером і fan-out-споживачем — bounded Channel; повільний підписник створює back-pressure аж до signal-cli.
- `JsonRpcClient` приймає `TimeProvider` — `CancellationTokenSource(_requestTimeout, _timeProvider)` робить timeout-шлях віртуалізованим у тестах.
- `SignalCliHostedService.StopProcessInternalAsyncNoLock` теж використовує `CancellationTokenSource(_, _timeProvider)`.
- `BeginScope(RpcMethod, RpcRequestId)` у `JsonRpcClient.InvokeMethodAsync` — кожний нижчий `JsonRpcClientLog.*` несе structured-properties.

#### Subscription race safety
- Reservation placeholder pattern у `SignalEventService.SubscribeAsync` через `Dictionary<string, TaskCompletionSource<int>> _pendingSubscribes`. Конкурентні виклики для того самого облікового запису роблять РІВНО 1 RPC; усі N викликачів отримують той самий ID.
- `ObjectDisposedException.ThrowIf(_disposed, this)` на `SubscribeAsync`/`UnsubscribeAsync` (audit C6).

#### Async-suffix shims (one-major-grace)
- `ISignalAccounts.ListAccountsAsync`/`SyncAccountAsync`, `ISignalDevices.StartLinkAsync`/`FinishLinkAsync`, `ISignalGroups.ListGroupsAsync` — нові методи + `[Obsolete]` DIM-shims на старі імена ("will be removed in 4.0").

### 🛠 Внутрішнє

- `ProcessStateManager`: snapshot-then-emit (OnNext поза локом — System.Threading.Lock не реентрантний). `_disposed` всюди → `int` з `Interlocked.Exchange` (lock-free disposal short-circuit). Catch `ObjectDisposedException` з OnNext (documented disposal race window).
- `_disposed` стандартизовано як `int + Interlocked.Exchange` у `SignalCliHostedService`, `JsonRpcClient`, `JsonRpcClientHostedService`, `SignalEventService`.
- `Nito.AsyncEx` видалено. `JsonRpcClient._sendLock` і `SignalCliHostedService._operationLock` → `SemaphoreSlim(1,1)` з `WaitAsync`/`Release`.
- `.ValidateDataAnnotations()` видалено з options-pipeline — `[OptionsValidator]` source-gen самостійно перевіряє `[Required]`/`[Range]` без reflection. Знято останній AOT-blocker у options-шляху.
- `SignalJsonContext.GenerationMode = Default` (fast-path emission + metadata) замість Metadata-only.
- `SignalEventService`, `ProcessWrapper`, `ProcessFactory`, `JsonRpcClientFactory`, `SignalAccounts`, `SignalDevices`, `SignalGroups` — sealed (CA1052).
- `Config.BuildClasspath` кешує classpath; `Directory.GetFiles` викликається рівно 1 раз на `Config`-інстанс.
- `ValidateRecipients`: single-pass materialization + один `foreach` на user/group split (раніше — 3 пройдення).
- `ArgumentException.ThrowIfNullOrEmpty` boundary checks у `SignalDevices.FinishLinkAsync`/`SignalGroups.ListGroupsAsync`.
- `JetBrains.Annotations` PackageReference `PrivateAssets="all"` — більше не leak у consumer dependency graph.
- `Example/Program.cs` повністю переписаний на `async Task Main`/`await host.StopAsync()`/awaited `SendTextMessageAsync` — LLM-агенти, що копіюють приклад, успадковують правильні async-патерни.
- Forward-slash MSBuild paths у `SignalCli.runtime.csproj` і `SignalCli.Native.targets` — Linux-збірки runtime-пакетів більше не ламаються тихо.
- **(round 8)** Logging-perf analyzer rules: `CA1848 → warning` (блокує regression на direct `_logger.Log*` — кожна нова log-callsite має йти через `[LoggerMessage]`); `CA1873 → suggestion` (analyzer не розпізнає manual `IsEnabled`-guards, тож `warning`-рівень дає false-positives на legitimate Trace-only eager-eval сайтах). Trade-off задокументовано в `.editorconfig`.
- **(round 8)** Manual `if (_logger.IsEnabled(LogLevel.Trace))` guards над `string.Join(", ", response)` у `SignalAccounts.ListAccountsAsync` + `SignalGroups.ListGroupsAsync` — `[LoggerMessage]`-внутрішній IsEnabled живе всередині generated-methоду, тож callsite-level allocation платилася на КОЖНОМУ виклику (навіть Info-level). Економить N × string-allocation на listAccounts/listGroups.
- **(round 8)** `ObservabilityPrivacyTests` flake-fix: lock-snapshot pattern для `_capturedActivities`/`_capturedMeasurements`. ActivityListener/MeterListener реєструються глобально на ActivitySource/Meter, тож callback'и можуть прилітати з потоків паралельних тестів — `List<T>` не thread-safe → `Collection was modified` intermittent. Всі writes тепер під `Lock`, читачі enumerate'ять snapshot.
- **(round 9 §4.9)** `.editorconfig` піднято `CA2007 (ConfigureAwait)` до `warning` після audit-перевірки: 0 missing-sites у `src/SignalCli/**`. Тепер регресія неможлива — будь-який майбутній bare `await` ловиться build-warning'ом.
- **(round 9 §4.25)** `[StringSyntax(StringSyntaxAttribute.Uri)]` на `TextMessageOptions.PreviewUrl`, `PreviewImage`, та параметрах `Builder.WithPreview(previewUrl, …, previewImage)`. Zero runtime cost; IDEs тепер валідують URL-syntax.
- **(round 9 §4.26)** XMLDoc'и на 3 `Send*Async`-методах в `ISignalMessage` отримали `<exception cref="TimeoutException">` із посиланням на `SignalCliOptions.RequestTimeoutSeconds`. Closes audit-doc-gap.
- **(round 10 §6.12)** Новий test-file `JsonContextRegistrationTests` — рефлексивно стверджує, що кожен `*Parameters`/`*Response` DTO у `SignalCli.Models.Signal.*` зареєстрований у `SignalJsonContext` через `[JsonSerializable(typeof(...))]`. Захист від "silent {}"-регресії, коли source-gen контекст не знає тип і `JsonSerializer.SerializeToElement` тихо повертає порожній об'єкт. Закриває audit N8.
- **(round 10 §7.4)** `Assert.Equal(1.0, …)` → `Assert.Equal(1.0, …, precision: 3)` у `MonitorLoop_ShouldRespectHealthCheckInterval`. CA2243 best-practice — explicit precision на double-asserts.
- **(round 10 §4.9 follow-up)** Scope-обмеження `CA2007 → warning` тільки для `src/SignalCli/**`; у `Tests/**` залишили `none` (xUnit-runner запускає тести без SynchronizationContext, тож `.ConfigureAwait(false)` no-op'не).
- **(round 11 §4.20)** `ListAccountsResponse` і `ListGroupsResponse` тепер wrapper-records над `IReadOnlyList<T>` із custom `JsonConverter` (зберігає плоский JSON-array на wire). Раніше успадковували `List<T>` — консумер міг мутувати дані сервера через `.Add`/`.Clear`/`.Sort`. **Breaking для тих, хто мутував** (compile-time error). Реєстрації `List<Account>` + `List<Group>` додано в `SignalJsonContext` (source-gen у .NET 7+ не має reflection-fallback). `Account` отримав `[JsonPropertyName("number")]` — корекція wire-shape на write. 2 нові round-trip тести в `JsonSerializationTests`.
- **(round 11 §8b.3)** Новий public overload `AddSignalCli(this IServiceCollection, IConfiguration)` — канонічний шлях для `appsettings.json`-конфігурації. Robить `AddOptions<SignalCliOptions>().Bind(section)` із тими ж валідаційними правилами (XOR Java/Native + source-gen validator + ValidateOnStart), що й `Action<SignalCliOptions>`-overload. Залежність `Microsoft.Extensions.Options.ConfigurationExtensions 10.0.0` додано в `SignalCli.csproj`. 3 нові тести в `OptionsValidationTests`. Closes audit B5 hookup.
- **(round 12 §8c.9)** Test: `SendTextMessageAsync_StatefulEnumerableRecipients_AreEnumeratedExactlyOnce` — захист §8c.5 single-pass-materialization від регресії до 3-х проходів (validate + 2× Where) на stateful IEnumerable.
- **(round 12 §8c.10)** Test: `ToProcessConfig_CachesClasspath_SecondCall_DoesNotEnumerateFiles` — observable-pattern (delete jar between calls), захист §8c.8 classpath-кешування.
- **(round 12 §9.6/§11.C.5)** CLAUDE.md "Established patterns" — нова **Observability** subsection: single ActivitySource/Meter `"SignalCli.NET"`, canonical tag-key set `{method, status, trigger, event_type}` (pinned by `MeterTagValues_AreOnlyKnownEnumLiterals`), HealthChecks adapter як ОКРЕМИЙ optional-package (NEVER hard dep на `Microsoft.Extensions.Diagnostics.HealthChecks` у core), lock+snapshot pattern для listener-fan-out тестів.
- **(round 13 §7.2/§7.3)** Reflection helpers `GetPrivateField<T>`/`SetPrivateField` видалено з `SignalCliHostedServiceTestsBase`. Замість них — `internal IProcess? SignalCliHostedService.CurrentProcessForTests` + `CurrentStreamPairForTests` (typed test-seam, видимий через `InternalsVisibleTo("SignalCli.Tests")`). 35 reflection-сайтів у 7 test-файлах перекинуто на типовий доступ. Renames приватних полів тепер ламають білд (compile-error), а не повертають мовчазний null.

#### AOT (capability `aot-readiness`) — round 14

- **(round 14 §6.7) `<IsAotCompatible>true</IsAotCompatible>` УВІМКНЕНО** в `SignalCli.csproj`. Library тепер ship'иться як AOT-сумісна — консумери можуть `dotnet publish /p:PublishAot=true` свої app'и без IL2026/IL3050 warnings, що приходять із нас. **Cold-start win**, **smaller native binary**, **WASM/iOS-friendly**.
- ⚠️ **(round 14 §6.7)** Breaking: `ISignalCliClient.InvokeMethodAsync<TReq, TResp>` тепер вимагає 2 нових параметри — `JsonTypeInfo<TRequest> requestTypeInfo` + `JsonTypeInfo<TResponse> responseTypeInfo`. Те ж саме на `IJsonRpcSender`. Migration: `client.InvokeMethodAsync<FooReq, FooResp>("m", req, ct)` → `client.InvokeMethodAsync("m", req, SignalJsonContext.Default.FooReq, SignalJsonContext.Default.FooResp, ct)`. Це **enables AOT-safety** — generic-overload `JsonSerializer.Serialize<T>(_, options)` (reflection-based) повністю відсутній з production-path.
- **(round 14 §6.4)** `SignalJson.Options.TypeInfoResolver` тепер **тільки** `SignalJsonContext.Default` — reflection fallback видалено. Будь-який тип, що крос-уйде JSON-кордон з `src/SignalCli/**` MAY бути зареєстрований у `SignalJsonContext`, інакше — runtime `NotSupportedException` (захист через `JsonContextRegistrationTests` (§6.12)).
- **(round 14 §6.10)** Новий `SignalJson.OptionsForTests` property (`[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`) — test-only path із reflection-fallback для анонімних типів. Анонімні-payload usages у `JsonRpcClientTests` (8 сайтів) замінено на `TestProbeRequest`/`TestProbeResponse`-records у новому `Tests/SignalCli.Tests/TestSerializationContext` (test-local `JsonSerializerContext`, не забруднює production).
- **(round 14 §6.11)** CLAUDE.md rule #6 оновлено: source-gen-only invariant + `OptionsForTests`-test-path задокументовано.
- **(round 14 §8b.10/§11.D.2/§9.4)** З AOT-увімкненим: library build = 0 IL2026/IL3050 warnings (включно з `Diagnostics/`, options-pipeline). `AddSignalCli(IConfiguration)` overload позначено `[RequiresUnreferencedCode]` (бо `Bind` тягне reflection) — для AOT-deploy використовуйте `AddSignalCli(Action<SignalCliOptions>)`.

#### Deferred-cluster (round 15) — усі тести з 2026-05-23 audit реалізовано

- **(round 15 §1.6/§7.7)** `BackPressureTests.NotificationBurst_WithSlowSubscriber_AllMessagesDeliveredInOrder`: 100-message burst через приватний ProcessMessageAsync (reflection), bounded channel capacity=8, sync-subscriber 5ms/msg → всі 100 доставлено в FIFO-порядку. Захист від drop'ів і реордерінгу при slow-consumer back-pressure.
- **(round 15 §1.8)** `TimeoutVirtualizationTests` × 2: `_InvokeMethodAsync_TimeoutPath_VirtualizedByFakeTimeProvider_ThrowsTimeoutException` (FakeTimeProvider.Advance(61s) триггерить timeoutCts → TimeoutException без real wall-clock) + sanity `_CallerCancellation_DoesNotFalselyAttributeToTimeout`. Сертифікує §1.7 TimeProvider-CTS wire-up.
- **(round 15 §2.5/§7.5)** `StateManagerReentrancyTests` × 2: synchronous Rx-subscriber виклика повторний `UpdateState` із OnNext-handler — ланцюг доходить до Stopping за <2с (інакше WaitAsync фейлить як deadlock). Concurrent-callers contention теж покрито.
- **(round 15 §4.15)** `*Options.Builder.Build()` post-mutation guard на 3 типах — кидає `InvalidOperationException` якщо обов'язкові поля обнулено між ctor і Build (захист від reflection / record-`with` mutation).
- **(round 15 §5.12)** `ScopeCaptureTests.InvokeMethodAsync_OpensScope_WithRpcMethod_AndRpcRequestId` через `FakeLogger<JsonRpcClient>` (пакет `Microsoft.Extensions.Diagnostics.Testing`) — фіксує що `RpcMethod` + `RpcRequestId` structured-scope-properties присутні на кожному log-entry, як обіцяно §5.11.
- **(round 15 §8a.6)** `BackgroundServiceLifecycleTests.StopAsync_BlocksUntilExecuteAsync_ObservesCancellation`: FakeTimeProvider-driven tick → ping observed → StopAsync → ExecuteTask.IsCompleted upto 5s real-time. Доказує що base.StopAsync блокує до завершення ExecuteAsync.
- **(round 15 §8a.8)** `StopProcessTimeoutVirtualizationTests.StopAsync_WhenWaitForExitTimesOut_KillsProcess_OnVirtualClock`: mock-process'у `WaitForExitAsync` блокує на CancellationToken.Register; `fakeTime.Advance(StopTimeoutSeconds + 1)` тригерить kill-branch. Сертифікує §1.7/§8a.7 TimeProvider-CTS wire-up на StopProcessInternalAsync.

#### Hosting modernization + CI smoke (round 16) — закриває останні 4 пункти

- **(round 16 §8a.2)** `SignalCliHostedService` і `JsonRpcClientHostedService` тепер `IHostedLifecycleService` (extends `IHostedService` із 4 додатковими phase-методами: `StartingAsync`/`StartedAsync`/`StoppingAsync`/`StoppedAsync`). Реалізації — no-op (поточна поведінка не зміняється); generic-host автоматично детектить interface і викликає phase'и у визначеному order'і. Foundation для майбутніх ordering-refinement'ів (warm-up ping після всіх start'ів тощо).
- **(round 16 §8a.3)** `SignalCliHostedService` тепер реалізує **обидва** `IAsyncDisposable` + `IDisposable`. `DisposeAsync` дренує `_operationLock.WaitAsync` із 2с-fallback-timeout (in-flight `Start/Stop/Restart` має шанс завершитися cleanly перед kill'ом); потім — спільний `DisposeCore` із sync-cleanup'ом. `Dispose()` — sync-only, без drain'у. **CLAUDE.md rule #9** (no sync-over-async in disposal) дотримано: обидва шляхи мають незалежні реалізації, спільне ядро. DI-контейнер preferр'ить `DisposeAsync` при scope-tear-down. Новий log-event `DisposeAsyncDrainTimeout` (EventId 132). 5 нових тестів у `AsyncDisposalLifecycleTests`.
- **(round 16 §8d.13/§8d.14)** Новий GitHub Actions workflow `.github/workflows/runtime-smoke.yml` із двома Linux-job'ами:
  - `native-runtime-delivery` — повна `dotnet build SignalCli.sln`; assertion: `signal-cli-native/signal-cli` дойшов у consumer TargetDir і має executable-bit. Захист від forward-/back-slash-регресії у MSBuild `Include`/`PackagePath` (closes audit N1 §8d.1).
  - `jre-guard-corruption` — build jre-runtime, delete `bin/java*`, re-build expected to fail із actionable message. Захист від видалення/деградації §8d.10 post-extract `<Error Condition>`-guard.
  - Path-filtered (`src/SignalCli.runtime*`, `src/build/`), `workflow_dispatch` для manual-run. `actions/*` pinned до commit-SHA per §8d.9 supply-chain.

Tests: **215/215 ✅** (baseline 180 → 215). Окрім тестів у workflow'ах — всі OpenSpec-таски виконано.

## [2.1.0] — неопубліковано

**Agent-friendly modernization** — п'ять незалежно вмикаємих кластерів, що приводять
бібліотеку у відповідність до сучасних патернів .NET 10 / C# 14 і підвищують
discoverability для AI-агентів і людей. Усі зміни (окрім трьох дрібних, явно
позначених нижче) — additive: старий код продовжує працювати з `[Obsolete]`-warning-ами.

### ✨ Додано

#### Agent-friendly API (cluster A)
- `ISignalCliClient.VersionAsync(CancellationToken)` — новий метод; старий `Version()`
  лишається як `[Obsolete]`-shim до 3.0.
- `ISignalMessage.Send{Text,Attachment,Sticker}MessageAsync` отримали явний параметр
  `CancellationToken cancellationToken = default` (лінкується з deprecated
  `options.CancellationToken` через `CreateLinkedTokenSource`).
- `TextStyleMode` enum замість stringly-typed `string? textMode = "styled"` (internal).
- `[CallerArgumentExpression]` у валідаторах — `ArgumentException.ParamName` тепер
  автоматично береться з виразу-аргументу.
- Усі `TaskCompletionSource<T>.TrySetCanceled` у `JsonRpcClient` тепер передають
  токен — викликач бачить причину скасування через `OperationCanceledException.CancellationToken`.
- `AtomicCounter` спрощено: `unchecked Interlocked.Increment` без CAS-reset гілки.

#### Background monitor (cluster B)
- `SignalCliHealthMonitor` тепер `BackgroundService` із `PeriodicTimer(interval, TimeProvider)`
  замість ручного `Task.Run` + `while (!ct.IsCancellationRequested) await Task.Delay(...)`.
- `SignalCliHostedService` приймає опціональний `TimeProvider`; усі `Task.Delay`/таймери
  всередині пропущені через нього (включно з вікном стабільності рестартів — раніше
  було сирий `Task.Run` + `Task.Delay`). Тестам можна підкласти `FakeTimeProvider`.

#### Async-stream events (cluster E)
- `ISignalEventService` розширено десятьма `*Async`-методами
  (`TextMessagesAsync`, `ReactionAsync`, `AttachmentsAsync`, …), які повертають
  `IAsyncEnumerable<TEventArgs>` поверх `Channel.CreateBounded<T>(1024, DropOldest)`.
  Стандартний C# `await foreach`, back-pressure (чого `Subject<T>` не має), drop-oldest
  при переповненні з лічильником у Debug-логах. Існуючі `IObservable<T>`-API залишаються
  для fan-out-сценаріїв.

#### Options pattern (cluster D)
- Новий `SignalCliOptions` (звичайні setter-и + `[Required]`/`[Range]`
  DataAnnotations) + `AddSignalCli(Action<SignalCliOptions>?)`-overload з
  `ValidateDataAnnotations() + Validate(...) + ValidateOnStart()`. Помилки конфігу
  фейляться на старті хоста з `OptionsValidationException` (не на `ToProcessConfig()`).
- `[OptionsValidator]` source-gen-валідатор: DataAnnotations перевіряються без
  reflection (AOT-safe).
- **D.4 повна міграція:** усі внутрішні сервіси
  (`SignalCliHostedService`, `SignalCliHealthMonitor`, `JsonRpcClientFactory`, `JsonRpcClient`)
  тепер приймають `IOptions<SignalCliOptions>` замість `Config`.
- Внутрішні сервіси читають `_options.Value` один раз у конструкторі (immutable).

#### Source-generated logging (cluster C)
- Усі 109 `ILogger` callsites переведено на `[LoggerMessage]`-`partial`-методи в
  `src/SignalCli/Logging/*Log.cs` (11 файлів, по одному на сервіс). Фіксовані
  EventId-блоки за сервісами: 100s — HostedService, 200s — HealthMonitor,
  300s — JsonRpcClient, 400s — JsonRpcClientHostedService, 500s — SignalEventService,
  600s — SignalService, 700s — SignalMessage, 800s — Accounts/Devices/Groups,
  900s — ProcessRunner/ProcessStateManager.
- Закриває CA1848 (`LoggerMessage`) і CA1873 (`AvoidExpensiveLogging`).
- `SignalEventService.OnNotificationReceived` тепер обгортає обробку нотифікації
  в `ILogger.BeginScope` зі структурованими `SubscriptionId`/`Account` —
  усі downstream-логи успадковують контекст.

### ⚠️ Несумісні зміни (BREAKING)
- **`IJsonRpcClient` більше не успадковує `IDisposable`** — лише `IAsyncDisposable`.
  Сторонні споживачі мають використовувати `await using` замість `using`. Прибрано
  внутрішній sync-over-async `Dispose()` (`DisposeAsync().GetAwaiter().GetResult()`).
- **Фасади `SignalAccounts`/`SignalDevices`/`SignalGroups`/`SignalService`/`SignalMessage`
  більше не імплементують `IDisposable`** (вони не тримали ресурсів; порожні `Dispose()`
  лише плутали). Зовнішні `using (signalAccounts)` тепер не компілюються — приберіть.
- **`IJsonRpcClientFactory.CreateAsync` → `Create()`** (синхронний). Фабрика не робила
  async-роботи; фейк-Async-суфікс прибрано.
- `Microsoft.Extensions.Options.DataAnnotations` 10.0.0 — нова залежність бібліотеки.

### 🛠 Інше
- `Config` лишається як `[Obsolete]`-shim, що мапиться у `SignalCliOptions` через
  адаптер. Буде видалений у 3.0.
- `*Options.CancellationToken` (`TextMessageOptions`, `AttachmentMessageOptions`,
  `StickerMessageOptions`) та `WithCancellationToken`-білдери позначено `[Obsolete]` —
  передавайте токен прямо в `Send*Async(options, ct)`. Буде видалено в 3.0.
- Тести: 173 → 180 (нові `OptionsValidationTests` × 4, `AsyncEnumerableEventDispatchTests` × 3).
  Усі стабільні; раніше flaky `ForceRestart*Delay*` тести переведено на `FakeTimeProvider`.

## [2.0.0] — неопубліковано

### ⚠️ Несумісні зміни (BREAKING)
- **Цільова платформа `net9.0` → `net10.0` (LTS).** Споживачам потрібен .NET 10 SDK/рантайм.
- **Прибрано залежність `Newtonsoft.Json`** — серіалізація повністю на `System.Text.Json`
  (з source-generated контекстом). Моделі тепер використовують `[JsonPropertyName]`.
- `JsonRpcRequest.Params` і `JsonRpcResponse.Result` тепер `System.Text.Json.JsonElement`
  (раніше `Newtonsoft.Json.Linq.JToken`).
- Узагальнене обмеження `InvokeMethodAsync<TResponse, TRequest>` змінено з `where TResponse : class`
  на `where TResponse : notnull` (тепер підтримує value-типи, напр. `JsonElement`).

### ✨ Додано
- **Native-режим без Java:** `Config.SignalCliExecutable` запускає нативний (GraalVM)
  бінарник signal-cli напряму, без JVM. Новий пакет **`SignalCli.Runtime.Native`**
  бандлить офіційний native-білд (Linux x64, SHA-256-перевірений). `Config.CreateDefault()`
  більше не вимагає Java — її відсутність не кидає виняток на етапі реєстрації.
  *(Офіційних native-білдів для Windows/macOS немає — там потрібна Java.)*
- **Bundled-JRE варіанти без системної Java (Windows/macOS):** нові пакети
  **`SignalCli.Runtime.Jre.win-x64`** та **`SignalCli.Runtime.Jre.osx-arm64`** містять
  вбудований Eclipse Temurin 25 JRE (SHA-256-перевірений) разом із signal-cli. Це
  drop-in заміна `SignalCli.Runtime`: достатньо підключити пакет — `Config.JavaExecutable`
  автоматично резолвиться у `jre/bin/java[.exe]` (новий метод `Config.ResolveBundledJava`),
  системна Java не потрібна. Перевірено наскрізно на Windows (signal-cli стартує під
  вбудованим JRE, JSON-RPC працює).
- **Важливо:** signal-cli 0.14.3 скомпільовано під **Java 25** (class-file version 69.0),
  тож JVM-режим тепер потребує **JDK/JRE 25+** (раніше в документації значилось 21+).
- signal-cli оновлено до **v0.14.3** із перевіркою цілісності завантаження (SHA-256).
- Граційне завершення signal-cli: ізоляція в окремій групі процесів (Windows, .NET 10)
  + конфігурований таймаут `Config.StopTimeoutSeconds` перед примусовим завершенням.
- Кросплатформний пошук Java (Windows/Linux/macOS): `JAVA_HOME` → `PATH`.
- `CLAUDE.md`, `.editorconfig` та аналізатори для якості коду; бібліотека warning-clean
  (`TreatWarningsAsErrors`).

### 🐛 Виправлено
- **Приватність:** тіла повідомлень, номери та вкладення більше не логуються вище за `Trace`.
- **Втрата подій:** одне повідомлення з текстом + вкладенням тепер піднімає всі відповідні
  реактивні події (раніше — лише першу).
- **Path traversal** у тимчасових файлах вкладень (`AttachmentEntry`).
- **Безпека аргументів процесу:** перехід на `ProcessStartInfo.ArgumentList`.
- Локаленезалежні назви стилів тексту (`ToUpperInvariant`).
- Уніфіковано стан процесу: `ProcessStateManager` — єдине джерело істини.

### 🔧 Інше
- `Newtonsoft.Json` 13.0.1 → видалено; `Microsoft.Extensions.*` → 10.0.0.
- `ProcessWrapper` використовує `Process.WaitForExitAsync`.
