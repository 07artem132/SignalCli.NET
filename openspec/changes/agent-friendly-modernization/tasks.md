## A. Agent-friendly API (нульовий ризик, перший PR)

- [ ] A.1 (E1) `ISignalCliClient.VersionAsync()` додати; `Version()` лишити як `[Obsolete("Use VersionAsync; will be removed in 3.0")]` → делегує на новий
- [ ] A.2 (E1) `SignalService.VersionAsync()` реалізувати; адаптувати `JsonRpcClientHostedService.StartAsync` на новий виклик
- [ ] A.3 (E2) `SignalMessage.SendTextMessageAsync(TextMessageOptions, CancellationToken = default)` overload; усередині лінкуємо `options.CancellationToken` й параметр через `CreateLinkedTokenSource`
- [ ] A.4 (E2) Аналогічно для `SendAttachmentAsync` і `SendStickerAsync`
- [ ] A.5 (E2) `TextMessageOptions.CancellationToken` (і пара інших) → `[Obsolete("Pass CancellationToken to SendXxxAsync directly; will be removed in 3.0")]`
- [ ] A.6 (E3) Зняти `IDisposable` з `IJsonRpcClient` (лишити лише `IAsyncDisposable`); прибрати ручний `Dispose()` із `JsonRpcClient`; `JsonRpcClientHostedService.StopAsync` уже використовує `IAsyncDisposable` шлях
- [ ] A.7 (E4) `IJsonRpcClientFactory.Create()` (sync) замість `CreateAsync`; внутрішніх викликачів — 1 (`JsonRpcClientHostedService`)
- [ ] A.8 (E5) `AtomicCounter.Increment` → `unchecked((int)Interlocked.Increment(ref _seed))`; видалити reset-CAS гілку; додати unit test, що 2 потоки на 100k інкрементів не дублюються
- [ ] A.9 (E6) В `JsonRpcClient.OnStreamPairChanged` і `DisposeAsync` `TrySetCanceled()` → `TrySetCanceled(token)` із відповідним маркерним токеном (новий `_disposeCts.Token` для disposal; `OnStreamPairChanged` бере «маркер» зі скасованого нового CTS)
- [ ] A.10 (E7) `enum TextStyleMode { None, Styled }`; `SignalMessage.SendUnifiedMessageAsync` приймає `TextStyleMode` замість `string? textMode`; виклики з `*MessageOptions` мапляться через `options.UseStyle ? Styled : None`
- [ ] A.11 (E8) `SignalEventService.OnNotificationReceived`: `using var scope = _logger.BeginScope(new Dictionary<string, object> { ["SubscriptionId"] = subscriptionId, ["Account"] = account })`
- [ ] A.12 (E9) `ValidateRecipients` приймає `[CallerArgumentExpression(nameof(recipients))] string? paramName = null`; забрати дефолт `"recipients"`
- [ ] A.13 (E10) Прибрати `IDisposable` (й порожні `Dispose()`) з `SignalAccounts`, `SignalDevices`, `SignalGroups`, `SignalService`, `SignalMessage`; зафіксувати в `CHANGELOG.md` як breaking (2.1.0)
- [ ] A.14 `dotnet build -warnaserror` зелений; `dotnet test` зелений; усі shim-и анотовані `[Obsolete]`

## B. Background monitor

- [ ] B.1 `SignalCliHealthMonitor : BackgroundService` (замість `IHostedService`, `IDisposable`); `MonitorLoop` → `ExecuteAsync`
- [ ] B.2 `MonitorLoop` переписати на `using var timer = new PeriodicTimer(interval, _timeProvider); while (await timer.WaitForNextTickAsync(ct)) { … }`
- [ ] B.3 Тести `SignalCliHealthMonitorLoopTests`/`*StartStopTests`: переконатися, що `FakeTimeProvider.Advance(interval)` крутить `PeriodicTimer` так само (`FakeTimeProvider` офіційно з ним сумісний)
- [ ] B.4 `SignalCliHostedService.ScheduleRestartWindowReset`: замість `Task.Run(async () => Task.Delay(window, token))` — `_timeProvider.CreateTimer(callback, state, window, Timeout.InfiniteTimeSpan)`; колбек бере `_operationLock` і обнуляє `_restartCount` за тих самих гард-умов
- [ ] B.5 Інʼєктувати `TimeProvider` в `SignalCliHostedService` (зараз його там немає); реєстрація — `services.TryAddSingleton(TimeProvider.System)`
- [ ] B.6 Тест: `FakeTimeProvider.Advance(RestartWindowSeconds + 1)` обнуляє `_restartCount` без потреби в реальному `Task.Delay`
- [ ] B.7 `dotnet test` зелений; жоден тест `SignalCliHealthMonitor*`/`SignalCliHostedService*Restart*` не використовує `Task.Delay` понад 10 мс

## C. Source-generated logging

- [ ] C.1 Створити `src/SignalCli/Logging/SignalCliHostedServiceLog.cs` із `[LoggerMessage]`-методами (EventId 100–199); замінити всі `_logger.Log*` у `SignalCliHostedService.cs` на нові виклики; зберігти ідентичні повідомлення (Ukrainian)
- [ ] C.2 Створити `Logging/SignalCliHealthMonitorLog.cs` (200–299); замінити в `SignalCliHealthMonitor.cs`
- [ ] C.3 Створити `Logging/JsonRpcClientLog.cs` (300–399) + `JsonRpcClientHostedServiceLog.cs` (400–499); замінити в обох
- [ ] C.4 Створити `Logging/SignalEventServiceLog.cs` (500–599); замінити в `SignalEventService.cs` (із новим `BeginScope` із A.11)
- [ ] C.5 Створити `Logging/SignalServiceLog.cs` (600–699), `SignalMessageLog.cs` (700–799), `SignalAccountsLog`/`SignalDevicesLog`/`SignalGroupsLog.cs` (800–899); замінити в `Services/Signal/*.cs`
- [ ] C.6 Створити `Logging/ProcessRunnerLog.cs` і `ProcessStateManagerLog.cs` (900–999); замінити в `Services/SignalCli/{ProcessRunner,ProcessStateManager}.cs`
- [ ] C.7 Прибрати лишок старого `BeginScope`/`LoggerExtensions.Log*`, окрім справді одноразових діагностичних повідомлень (документувати кожен виняток)
- [ ] C.8 `PrivacyLoggingTests` оновити, щоб перевіряло конкретні `EventId`, а не текстові підрядки (підвищує стійкість)
- [ ] C.9 `dotnet build -warnaserror` чистий; CA1848/CA1873 не порушуються; `dotnet test` зелений
- [ ] C.10 README + CLAUDE.md: додати правило «всі нові `ILogger`-виклики йдуть через `*Log`-partial-методи» (для майбутніх PR)

## D. Options pattern

- [ ] D.1 Створити `src/SignalCli/Models/SignalCliOptions.cs` як `sealed class` із `init`-only properties + DataAnnotations (`[Required]`, `[Range]`) — повна мапа полів `Config`
- [ ] D.2 Розширення `extension(IServiceCollection)`: новий метод `AddSignalCli(Action<SignalCliOptions>? configureOptions)` ; усередині `services.AddOptions<SignalCliOptions>().Configure(configureOptions ?? (_ => { })).ValidateDataAnnotations().Validate(o => …, "JavaExecutable або SignalCliExecutable обов’язкові").ValidateOnStart()`
- [ ] D.3 Адаптер `Config → SignalCliOptions` для shim `AddSignalCli(Action<Config>?)`; **обидва overload-и співіснують у `2.1`**; старий маркується `[Obsolete("Use AddSignalCli(Action<SignalCliOptions>); will be removed in 3.0")]`
- [ ] D.4 Усі сервіси, що зараз приймають `Config`, переписати на `IOptions<SignalCliOptions>`: `SignalCliHostedService`, `SignalCliHealthMonitor`, `JsonRpcClient(Factory)`, `ProcessRunner`, `Config.ToProcessConfig` → винести в `SignalCliOptionsExtensions.ToProcessConfig(this SignalCliOptions o)`
- [ ] D.5 Внутрішня реалізація сервісів читає `_options.Value` один раз у конструкторі; кешуємо в `private readonly SignalCliOptions _options;` (опції immutable)
- [ ] D.6 Новий тест-кейс `OptionsValidationTests`: `AppHome` порожній → `OptionsValidationException` на `host.Start()`; `MaxRestartAttempts = -1` → теж; обидва екзекьюти і `JavaExecutable`/`SignalCliExecutable` порожні → теж
- [ ] D.7 Адаптувати ВСІ існуючі тести `ConfigTests` під новий `SignalCliOptions` (одна passes-as-is перевірка) ; додати парне `SignalCliOptionsTests`
- [ ] D.8 README — приклад `appsettings.json`-секції `SignalCli` + `builder.Services.AddOptions<SignalCliOptions>().Bind(builder.Configuration.GetSection("SignalCli")).ValidateOnStart()`
- [ ] D.9 (Опційно) Додати референс на `Microsoft.Extensions.Options.SourceGeneration` пакет; зробити `SignalCliOptions` `partial` й позначити `[OptionsValidator]`-генерованим валідатором (AOT-safe). Реалізувати тільки якщо це не тягне `8.0.x` → `9.0.x` перенесення інших пакетів.
- [ ] D.10 `dotnet test` зелений; новий `OptionsValidationTests` додає 4+ тести

## E. Async-stream events

- [ ] E.1 У `SignalEventService` для кожного `Subject<TEventArgs>` (9 потоків) додати парний `Channel.CreateBounded<TEventArgs>(new BoundedChannelOptions(1024) { FullMode = DropOldest, SingleReader = false, SingleWriter = true })`
- [ ] E.2 У `OnNotificationReceived` після `_xxx.OnNext(evt)` робити `_xxxChannel.Writer.TryWrite(evt)` (drop-on-full тихо логнути на `Debug` із лічильником)
- [ ] E.3 `ISignalEventService` розширити дев’ятьма методами: `IAsyncEnumerable<TextMessageEventArgs> TextMessagesAsync(CancellationToken ct = default)` тощо; XMLDoc явно: «exclusive consumption — для fan-out використовуйте `TextMessages` Rx»
- [ ] E.4 `Dispose`: `_xxxChannel.Writer.TryComplete()` перед `OnCompleted()`/`Dispose()` для Subject
- [ ] E.5 Новий тест-файл `AsyncEnumerableEventDispatchTests`: підписатися через `await foreach`, надіслати 3 нотифікації, переконатися, що `await foreach` отримав усі 3 у порядку; перевірити DropOldest behaviour на 1500 елементів із буфером 1024
- [ ] E.6 Документація: в README додати приклад `await foreach (var msg in eventService.TextMessagesAsync(stoppingToken))`
- [ ] E.7 `dotnet test` зелений; нові тести (≥3) додаються

## Z. Verification (на кожному PR + сумарно)

- [ ] Z.1 `dotnet build SignalCli.sln -c Release` warning-clean (`TreatWarningsAsErrors=true`)
- [ ] Z.2 `dotnet test Tests/SignalCli.Tests/...` — усі попередні 152 + нові тести зелені; ≥ 165 тестів сумарно
- [ ] Z.3 `dotnet test Tests/SignalCli.Tests.Integration/...` `--filter Category=E2E` — без регресій
- [ ] Z.4 `openspec validate agent-friendly-modernization --strict` passes
- [ ] Z.5 README + CHANGELOG.md оновлено: депрекейти, нові API, migration steps
- [ ] Z.6 Smoke-тест із `Example/SignalCli.Example/Program.cs`, переписаний на нові API: працює end-to-end (manual)
- [ ] Z.7 Бамп `2.0.0` → `2.1.0` у `src/SignalCli/SignalCli.csproj` (semver: лише additive + `[Obsolete]`-shims, тож minor; A.13/E10 — пограничний, але `IDisposable` на facade-ах нікому не потрібен → minor + чітка нотатка у CHANGELOG)
