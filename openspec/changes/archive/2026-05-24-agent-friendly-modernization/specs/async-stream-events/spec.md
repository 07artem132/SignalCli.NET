## ADDED Requirements

### Requirement: Async-stream API alongside Rx for events
`ISignalEventService` SHALL надавати `IAsyncEnumerable<TEventArgs>`-методи (`TextMessagesAsync(ct)`, `ReactionAsync(ct)`, `AttachmentsAsync(ct)`, `StickerAsync(ct)`, `TypingAsync(ct)`, `ReceiptsAsync(ct)`, `SyncsAsync(ct)`, `QuotesAsync(ct)`, `EditsAsync(ct)`, `RemoteDeletesAsync(ct)`) як парні до існуючих `IObservable<TEventArgs>`-властивостей. Існуючі Rx-API SHALL лишитися незмінними.

#### Scenario: Consumer uses await foreach
- **GIVEN** активна підписка `subscribeReceive` для account-у
- **WHEN** споживач робить `await foreach (var msg in eventService.TextMessagesAsync(stoppingToken))`
- **THEN** кожна вхідна текстова нотифікація доставляється як один елемент послідовності
- **AND** скасування `stoppingToken` коректно завершує цикл без виключень

#### Scenario: Both APIs receive the same notification
- **WHEN** підписники одночасно слухають `TextMessages` (Rx) і `TextMessagesAsync` (async-stream)
- **THEN** Rx-підписник отримує всі нотифікації (fan-out, як зараз)
- **AND** один `TextMessagesAsync`-споживач отримує всі нотифікації, що з’явилися після початку його `await foreach`

### Requirement: Bounded channel with back-pressure (drop-oldest)
Async-stream-канали SHALL бути реалізовані поверх `Channel.CreateBounded<T>` з ємністю 1024 і `BoundedChannelFullMode.DropOldest`, щоб повільний споживач не з’їдав пам’ять і не блокував RPC-обробку.

#### Scenario: Overflow drops oldest items, not newest
- **GIVEN** єдиний споживач `await foreach` пасивний і не читає
- **WHEN** до каналу записано 1500 елементів
- **THEN** наступне читання дає елементи 477…1500 (drop-oldest вижене перші 476)
- **AND** факт дропу залогований на `Debug` із лічильником

### Requirement: Documented single-consumer semantics
XML-doc на `*Async`-методах SHALL явно вказувати, що `IAsyncEnumerable`-варіант призначений для **exclusive consumption** (один споживач читає кожен елемент рівно один раз), на відміну від `IObservable`-варіанта (broadcast). Це усуває плутанину, бо обидва API виглядають подібно.

#### Scenario: Doc mentions exclusivity
- **WHEN** генерується XML-документація бібліотеки
- **THEN** XMLDoc кожного `*Async`-методу містить речення, що описує single-consumer / no-fan-out семантику й рекомендує `IObservable`-аналог, якщо потрібен broadcast

### Requirement: Channels close cleanly on dispose
При диспозі `SignalEventService` всі канали SHALL отримати `Writer.TryComplete()` до того, як зупиняється підписка на JSON-RPC нотифікації. Усі активні `await foreach` SHALL завершитися без виключень.

#### Scenario: Dispose terminates active loops
- **GIVEN** активний `await foreach` на `TextMessagesAsync`
- **WHEN** контейнер DI диспозить `ISignalEventService`
- **THEN** цикл завершується нормально (як після `Writer.Complete()`), без `OperationCanceledException` чи `ObjectDisposedException`
