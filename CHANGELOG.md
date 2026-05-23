# Changelog

Формат заснований на [Keep a Changelog](https://keepachangelog.com/),
проєкт дотримується [семантичного версіонування](https://semver.org/lang/uk/).

## [3.0.0] — неопубліковано (WIP, post-modernize-tuning)

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
