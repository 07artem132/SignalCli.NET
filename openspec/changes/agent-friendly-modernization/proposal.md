## Why

Аудит проти Microsoft Learn (.NET 10 / C# 14) показав, що базова архітектура SignalCli.NET модерна (DI, hosted services, `TimeProvider`, `System.Threading.Lock`, primary constructors, records, source-generated JSON), але кілька конвенцій ще не приведено до того, що Microsoft рекомендує для **AI-/agent-friendly** бібліотек: типізована конфігурація з валідацією замість мутабельного класу, source-generated logging замість рядкових шаблонів, `IAsyncEnumerable<T>` поряд із `IObservable<T>` для подій, `BackgroundService`+`PeriodicTimer` замість ручного `Task.Run`-циклу, і дрібні API-конвенції (`Async`-суфікс, явний `CancellationToken`).

Це не bug-fix-и — поточний код працює. Це **підвищення discoverability для LLM-агентів і людей**: `IOptions<T>` + `[LoggerMessage]` + `IAsyncEnumerable` дають типи й сигнатури, які агент знаходить механічно (через рефлексію DI / автокомпліт), без потреби читати імплементацію. Усі зміни — additive, окрім кількох документованих перейменувань.

## What Changes

- **Options pattern (`options-pattern`)** — замінити `AddSingleton(Config)` на `AddOptions<SignalCliOptions>().Configure(...).ValidateDataAnnotations().ValidateOnStart()`. `Config` стає immutable-`record` (або клас із `init`-only) із `[Required]`/`[Range]` атрибутами. Існуючий публічний `Config` лишається як `[Obsolete]`-shim для backward compat одного мажорного релізу. Помилка «JavaExecutable порожній» з’являється на старті хоста, а не на спробі запустити signal-cli.

- **Background monitor (`background-monitor`)** — `SignalCliHealthMonitor` перевести з ручного `Task.Run` + `MonitorLoop` на `BackgroundService` + `PeriodicTimer(interval, TimeProvider)`. Аналогічно `SignalCliHostedService.ScheduleRestartWindowReset` переписати на `_timeProvider.CreateTimer(...)` замість сирого `Task.Run(async () => Task.Delay(...))`. Усуває розбіжність між моніторингом (вже з `TimeProvider`) і вікном стабільності (без), дає прозорий lifecycle.

- **Source-generated logging (`source-generated-logging`)** — для всіх `ILogger<T>` ввести `internal static partial class XxxLog` із `[LoggerMessage]`-методами. Замінює ≈80+ викликів `_logger.LogInformation("шаблон {P}", v)` на типобезпечні `XxxLog.Started(_logger, pid)`. Покриває CA1848/CA1873; дає AOT-safe фасад логів і чистий контракт для агентів.

- **Async-stream events (`async-stream-events`)** — `ISignalEventService` дістає дублі поточних `IObservable<T>` як `IAsyncEnumerable<T>` методи (`TextMessagesAsync(ct)` тощо), реалізовані через `Channel.CreateBounded` з `FullMode = DropOldest`. Існуючі Rx-API лишаються незмінними; нові методи — стандартний C# `await foreach`, який LLM-агент пише за умовчанням. Дає back-pressure (чого `Subject<T>` не має).

- **Agent-friendly API conventions (`agent-friendly-api`)** — низка дрібних, але помітних для агента речей: `ISignalCliClient.Version()` → `VersionAsync()`; явний `CancellationToken cancellationToken = default` як параметр на `SignalMessage.Send*Async` (поряд із полем у `*Options`); прибрати `IDisposable` з `IJsonRpcClient` (лишити лише `IAsyncDisposable`); `JsonRpcClientFactory.CreateAsync` → синхронний `Create` (або `ValueTask`); `AtomicCounter` спростити до `unchecked Interlocked.Increment`; передавати токен у `TrySetCanceled(token)` усюди; замінити stringly-typed `textMode = "styled"` на `enum TextStyleMode`; `BeginScope` для subscriptionId/account у `SignalEventService`; `[CallerArgumentExpression]` у `ValidateRecipients`; прибрати порожні `IDisposable`-реалізації з `SignalAccounts`/`SignalGroups`/`SignalDevices`/`SignalService`.

## Capabilities

### New Capabilities
- `options-pattern`: типізована конфігурація через `IOptions<TOptions>` із валідацією на старті хоста.
- `background-monitor`: фонові цикли через `BackgroundService` + `PeriodicTimer` із `TimeProvider`.
- `source-generated-logging`: статичні `partial`-методи з `[LoggerMessage]` як єдиний фасад логів бібліотеки.
- `async-stream-events`: дублювання Rx-потоків подій через `IAsyncEnumerable<T>` поверх `Channel<T>`.
- `agent-friendly-api`: дрібні конвенції API, що підвищують discoverability для LLM-агентів і людей.

### Modified Capabilities
<!-- Жодну існуючу спеку не модифікуємо: усі п’ять capability-ів — нові, additive. -->

## Impact

- **Код:**
  - `Models/Config.cs` → `Models/SignalCliOptions.cs` (+ shim) ; `Extensions/ServiceCollectionExtensions.cs`.
  - `Services/SignalCli/SignalCliHealthMonitor.cs` (→ `BackgroundService`); `Services/SignalCli/SignalCliHostedService.cs` (`ScheduleRestartWindowReset` → `TimeProvider.CreateTimer`).
  - Новий `Logging/*Log.cs` (по одному `partial` per service); усі `Services/**/*.cs` міняють виклики `_logger.Log*` на згенеровані методи.
  - `Services/Signal/SignalEventService.cs` (+ `Channel<T>` поверх кожного Subject); `Interfaces/Signal/ISignalEventService.cs` (+ `*Async` методи).
  - `Interfaces/SignalCli/ISignalCliClient.cs` (`Version` → `VersionAsync`); `Services/Signal/SignalService.cs`.
  - `Services/Signal/SignalMessage.cs` (+ `CancellationToken` як параметр; `TextStyleMode` enum); `Models/Signal/Message/*Options.cs` (depr `CancellationToken` property).
  - `Interfaces/Rpc/IJsonRpcClient.cs` (зняти `IDisposable`); `Services/Rpc/JsonRpcClient.cs`; `Services/Rpc/JsonRpcClientHostedService.cs`.
  - `Services/Rpc/JsonRpcClientFactory.cs` (`CreateAsync` → `Create`); `Interfaces/Rpc/IJsonRpcClientFactory.cs`.
  - `Utilities/AtomicCounter.cs` (спрощення).
  - `Services/Signal/{SignalAccounts,SignalGroups,SignalDevices,SignalService}.cs` (прибрати `IDisposable`).

- **Тести:** новий `Tests/SignalCli.Tests/OptionsValidationTests.cs` (валідація на старті); адаптація `JsonRpcClientHostedServiceTests`, `SignalCliHealthMonitorLoopTests` (`PeriodicTimer` + `FakeTimeProvider`); нові `AsyncEnumerableEventDispatchTests`; уся стара поведінка лишається зеленою (152 тести → ≥ 160).

- **Документація:** `README.md` — приклад нового `AddSignalCli` + `Action<SignalCliOptions>` + `appsettings.json`-секція; `CHANGELOG.md` — депрекейти й нові API; `CLAUDE.md` — згадка про source-gen logging як обов’язковий патерн.

- **Поведінка:**
  - Викликачі побачать `OptionsValidationException` на `host.Start()` замість `InvalidOperationException` у `ToProcessConfig()` (раніше за щоразу).
  - Логи мають стабільні `EventId`-и → можна фільтрувати у телеметрії.
  - `JsonRpcClient` більше не реалізує `IDisposable` — викликачі повинні використовувати `await using` або вже отримують auto-dispose від DI.
  - `Version()` залишається як `[Obsolete]`-shim що делегує на `VersionAsync()` — м’яка міграція.

- **Послідовність / ризик:** п’ять незалежних кластерів. Рекомендований порядок: `agent-friendly-api` (XS/S, нульовий ризик) → `background-monitor` (S, замкнено в одному файлі) → `source-generated-logging` (L, mechanical) → `options-pattern` (M, торкається DI кореня) → `async-stream-events` (M, additive до Rx). Кожен крок — окремий PR, кожен незалежно відкатний.
