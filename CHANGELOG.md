# Changelog

Формат заснований на [Keep a Changelog](https://keepachangelog.com/),
проєкт дотримується [семантичного версіонування](https://semver.org/lang/uk/).

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
