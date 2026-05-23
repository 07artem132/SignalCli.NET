# Design — agent-friendly modernization

## Context

Цей change з’являється після `address-audit-findings`, `address-audit-findings-2`, `modernize-architecture` і `agent-ready-conventions`. Ті чотири закрили коректність, продуктивність і базову модернізацію. Наступний крок — **discoverability**: бібліотеку має бути зручно вживати **і людині, і LLM-агенту**, який ніколи не читав вихідного коду й покладається лише на DI-сигнатури, типи, автокомпліт і документацію API.

Microsoft Learn у 2025–2026 послідовно просуває п’ять патернів, які закривають цю прогалину:

1. **Options pattern із валідацією на старті** (`Microsoft.Extensions.Options`, `OptionsBuilderExtensions.ValidateOnStart`, `OptionsBuilderDataAnnotationsExtensions.ValidateDataAnnotations`, source-generator у `Microsoft.Extensions.Options.SourceGeneration`).
2. **`BackgroundService` + `PeriodicTimer`** як канонічний механізм фонових циклів; з .NET 10 `BackgroundService.ExecuteAsync` повністю на background thread, тож не блокує старт інших сервісів.
3. **Source-generated logging** (`[LoggerMessage]`) — Microsoft аналізатори вмикають CA1848/CA1873 за умовчанням у .NET 10.
4. **`IAsyncEnumerable<T>` + `System.Threading.Channels`** як стандартний канал «producer → consumer» у новому C#; LLM-агенти пишуть `await foreach` за замовчуванням.
5. **Конвенції .NET API design** (`Async`-суфікс, `CancellationToken` як останній параметр, `IAsyncDisposable` для класів із асинхронним cleanup, `[CallerArgumentExpression]` для `Validate*`).

## Goals / Non-Goals

**Goals:**
- Перевести бібліотеку на канонічні Microsoft-патерни 2026 без ламання публічного контракту в `2.x` (там, де можливо).
- Дати LLM-агенту достатньо машинно-зчитуваного контексту (типи, атрибути, `[LoggerMessage]`-методи), щоб генерувати правильний код без читання `internal`.
- Зберегти 100% поточних 152 тестів зеленими.

**Non-Goals:**
- Native AOT end-to-end (лишається ціль наступного релізу; джерело-генерована частина його не блокує).
- Виносити обробку нотифікацій у TPL Dataflow (overkill для поточного навантаження).
- Замінювати власний `JsonRpcClient` на `StreamJsonRpc` (`Microsoft.VisualStudio.Threading.JsonRpc`) — overkill, переписало б усю транспортну логіку.
- Прибирати Rx-API подій (залежність `System.Reactive` лишається, бо є зовнішні споживачі).

## Decisions

### A. Options pattern (`options-pattern`)

**Що:** новий `SignalCliOptions` як `sealed class` із `init`-only сетерами й DataAnnotations:

```csharp
public sealed class SignalCliOptions
{
    [Required] public required string AppHome { get; init; }
    [Required] public required string LibDirectory { get; init; }
    public string? JavaExecutable { get; init; }
    public string? SignalCliExecutable { get; init; }
    [Range(0, 100)] public int MaxRestartAttempts { get; init; } = 3;
    [Range(1, 3600)] public int RequestTimeoutSeconds { get; init; } = 30;
    [Range(1, 3600)] public int HealthCheckIntervalSeconds { get; init; } = 40;
    // … інші поля з Config.cs ; усі з [Range]/[Required].
}
```

**Реєстрація** через `AddOptions<SignalCliOptions>().Configure(configure).ValidateDataAnnotations().Validate(o => …, "повідомлення").ValidateOnStart()` ; внутрішні сервіси приймають `IOptions<SignalCliOptions>` (singleton-кешований).

**Сумісність:** старий `Config` лишається `[Obsolete("Used SignalCliOptions; this shim mapping will be removed in 3.0")]` і конвертується в `SignalCliOptions` у extension `AddSignalCli(Action<Config>?)`. Документуємо новий `AddSignalCli(Action<SignalCliOptions>?)` overload.

*Альтернативи:* лишити `Config` як є — відкинуто (втрачаємо fail-fast і source-gen валідацію). Зробити `record` із позиційним конструктором — відкинуто (порушує доступність зі споживацького `Action<>`-делегата).

### B. Background monitor (`background-monitor`)

**`SignalCliHealthMonitor`:**

```csharp
public sealed class SignalCliHealthMonitor(
    IOptions<SignalCliOptions> options,
    IJsonRpcClientProvider clientProvider,
    SignalCliHostedService signalCliHostedService,
    ILogger<SignalCliHealthMonitor> logger,
    TimeProvider? timeProvider = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(options.Value.HealthCheckIntervalSeconds);
        using var timer = new PeriodicTimer(interval, timeProvider ?? TimeProvider.System);
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            try
            {
                if (!await PingCliAsync(...).ConfigureAwait(false))
                    await signalCliHostedService.ForceRestartAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex) // навмисний catch на межі циклу
            { HealthMonitorLog.IterationFailed(logger, ex); }
        }
    }
}
```

**`SignalCliHostedService.ScheduleRestartWindowReset`:** замінити `Task.Run(async () => Task.Delay(window, token))` на `_timeProvider.CreateTimer(callback, state, window, Timeout.InfiniteTimeSpan)`. Колбек бере `_operationLock` і ресетить `_restartCount` так само, як зараз. Тестам віддає `FakeTimeProvider` — і test wall-clock більше не задіяний.

*Альтернативи:* `Timer` (`System.Threading.Timer`) — відкинуто, бо `TimeProvider.CreateTimer` дає той самий API + тестується. Лишати ручний `Task.Run`-цикл у `SignalCliHealthMonitor` — відкинуто, бо саме його `BackgroundService` і замінює (`BackgroundService runs all of ExecuteAsync as a Task` — .NET 10 docs).

### C. Source-generated logging (`source-generated-logging`)

**Що:** один `internal static partial class` на сервіс, у файлі поруч (`Services/SignalCli/SignalCliHostedServiceLog.cs` тощо). Кожен метод — `[LoggerMessage(EventId, Level, Message)]`. EventId-блоки фіксуємо:

| Сервіс | Діапазон EventId |
|---|---|
| `SignalCliHostedServiceLog` | 100–199 |
| `SignalCliHealthMonitorLog` | 200–299 |
| `JsonRpcClientLog` | 300–399 |
| `JsonRpcClientHostedServiceLog` | 400–499 |
| `SignalEventServiceLog` | 500–599 |
| `SignalServiceLog` | 600–699 |
| `SignalMessageLog` | 700–799 |
| `SignalAccountsLog` / `SignalDevicesLog` / `SignalGroupsLog` | 800–899 |
| `ProcessRunnerLog` / `ProcessStateManagerLog` | 900–999 |

**Privacy:** ті самі правила з `address-audit-findings`-`logging-privacy` — `Trace`-only для тіл повідомлень/PII. Source-gen логи лиш роблять їх типобезпечними.

**Обсяг:** ≈80–100 викликів. Виконавчо — це search-and-replace, але кожен метод треба назвати осмислено. Розділимо на групи по сервісу, кожна — окремий комміт.

*Альтернативи:* `LoggerMessage.Define` — застарілий патерн (Microsoft docs: «If you're maintaining code that uses `LoggerMessage.Define`, consider migrating to source-generated logging»).

### D. Async-stream events (`async-stream-events`)

**Реалізація:** усередині `SignalEventService` поряд із кожним `Subject<T>` додаємо `Channel<T>`. У `OnNotificationReceived` після `_textMessages.OnNext(evt)` робимо `_textChannel.Writer.TryWrite(evt)`. Експозиція:

```csharp
public IAsyncEnumerable<TextMessageEventArgs> TextMessagesAsync(CancellationToken ct = default)
    => _textChannel.Reader.ReadAllAsync(ct);
```

**Channel config:** `Channel.CreateBounded<T>(new BoundedChannelOptions(1024) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = false, SingleWriter = true })`. `SingleReader = false` — щоб кілька споживачів могли паралельно `ReadAllAsync` (broadcast треба окремо; розглядаємо як non-goal — підписники зчитують exclusive).

Якщо broadcast потрібен — поясни в API doc, що `*Async` методи fan-out не роблять, на відміну від Rx. Це задокументована різниця.

**Disposal:** у `Dispose` додаємо `_textChannel.Writer.TryComplete()` (закриває всі `await foreach`).

*Альтернативи:* `BroadcastBlock<T>` (TPL Dataflow) — справді broadcast, але `IAsyncEnumerable` менш ергономічний. `IAsyncEnumerable` через `Subject<T>.ToAsyncEnumerable()` (Rx) — теж варіант, але тягне зайвий перетворювач і не дає back-pressure.

### E. Agent-friendly API (`agent-friendly-api`)

Дрібні зміни, об’єднані тематично:

| ID | Що | Файл | Сумісність |
|---|---|---|---|
| E1 | `ISignalCliClient.Version()` → `VersionAsync()` | `Interfaces/SignalCli/ISignalCliClient.cs` | старий — `[Obsolete]` shim, делегує на новий |
| E2 | `CancellationToken cancellationToken = default` як параметр на `SignalMessage.Send*Async` | `SignalMessage.cs`, `*Options.cs` | overload-додавання, поле в `*Options` лишається |
| E3 | Зняти `IDisposable` з `IJsonRpcClient` | `Interfaces/Rpc/IJsonRpcClient.cs`, `JsonRpcClient.cs` | **BREAKING для сторонніх імплементаторів**; внутрішньо все працює через `IAsyncDisposable` |
| E4 | `JsonRpcClientFactory.CreateAsync` → `Create` (синхронний) | `Interfaces/Rpc/IJsonRpcClientFactory.cs`, `JsonRpcClientFactory.cs` | внутрішня поверхня; внутрішніх викликів — 1 |
| E5 | Спростити `AtomicCounter` до `unchecked (int)Interlocked.Increment` | `Utilities/AtomicCounter.cs` | internal — без впливу на API |
| E6 | `tcs.TrySetCanceled(token)` усюди | `JsonRpcClient.cs` | без впливу на API |
| E7 | `enum TextStyleMode { None, Styled }` замість `string? textMode` | `SignalMessage.cs`, `*Options.cs` | internal API; зовнішнє `UseStyle` залишається |
| E8 | `_logger.BeginScope(new { SubscriptionId, Account })` навколо `OnNotificationReceived` | `SignalEventService.cs` | без впливу на API |
| E9 | `[CallerArgumentExpression]` у `ValidateRecipients` | `SignalMessage.cs` | без впливу на API |
| E10 | Прибрати порожні `IDisposable` з `SignalAccounts`/`SignalGroups`/`SignalDevices`/`SignalService`/`SignalMessage` | `Services/Signal/*.cs` | **BREAKING для зовнішніх `using (signalAccounts)`-патернів**; вкрай малоймовірно |

Усі breaking-зміни оформлюємо через `[Obsolete]` shim там, де можливо; для E3/E10 — фіксуємо в `CHANGELOG.md` як необхідні conventions cleanups для `2.1.0`.

## Risks / Trade-offs

- **`OptionsValidationException` на старті — поведінка, що видно одразу.** Існуючий код, що випадково покладався на `Config` без `AppHome`, тепер падатиме раніше. Це і є мета.
- **Source-gen logging — великий механічний diff.** Розіб’ємо на 5 PR (по сервісних кластерах), щоб ревʼю був реальний.
- **`IAsyncEnumerable` дублює API.** Two-way парний API легко плутати. Митигація: XMLDoc на обох наборах вказує контракт (fan-out vs single consumer).
- **Запропоновані `[Obsolete]` shims** живуть один мажор. Після `3.0` — видаляємо. Документуємо явно у `CHANGELOG.md`.
- **`BackgroundService` змінює lifecycle `SignalCliHealthMonitor`.** `StartAsync`/`StopAsync` мають базову реалізацію; усі поточні тести `SignalCliHealthMonitor*` адаптуємо (вже використовуємо `FakeTimeProvider` — тести стабільні).

## Migration Plan

1. **`agent-friendly-api` (E1–E10, нульовий ризик).** Один PR; усі дрібні. Обов’язково: usability-тест, що `await foreach` (з майбутнього D) і явний `cancellationToken` на `Send*Async` працюють.
2. **`background-monitor`.** Один PR; зачіпає 2 файли + 4 тестових файли (`SignalCliHealthMonitor*`, `SignalCliHostedService*Restart*`). Тести `FakeTimeProvider`-driven залишаються.
3. **`source-generated-logging`.** 5 PR-ів послідовно (по групах EventId). Кожен — pure mechanical, перевіряється тим, що `dotnet test` зеленіє і `dotnet build -warnaserror` чистий.
4. **`options-pattern`.** Один PR. Документуємо migration path. Внутрішні сервіси переходять на `IOptions<SignalCliOptions>`, але приймають **і** `Config` через адаптер (для shim-конструкторів).
5. **`async-stream-events`.** Один PR. Pure-additive: інтерфейс розширюється новими методами, Rx не чіпаємо.

Кожен крок незалежно зеленіє в CI; будь-який можна відкотити окремо.

## Verification

- `dotnet build SignalCli.sln -c Release` warning-clean (`TreatWarningsAsErrors=true`).
- `dotnet test Tests/SignalCli.Tests/...` — усі 152+ зелені; нові тести з кожного capability додаються в той самий PR.
- `dotnet test Tests/SignalCli.Tests.Integration/...` `--filter Category=E2E` — лишається зеленим без змін.
- `openspec validate agent-friendly-modernization --strict` — passes.
- Manual: швидкий smoke-тест із `Example/SignalCli.Example/Program.cs` — переписати на `await foreach` + `appsettings.json`-конфіг, переконатись що demo-сценарій ще працює.
