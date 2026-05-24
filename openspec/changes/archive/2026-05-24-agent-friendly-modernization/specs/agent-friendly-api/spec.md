## ADDED Requirements

### Requirement: Async suffix on all asynchronous public methods
Усі публічні методи бібліотеки, що повертають `Task`/`Task<T>`/`ValueTask`/`ValueTask<T>`, SHALL мати суфікс `Async` у назві. Методи, що не дотримуються цього (зокрема `ISignalCliClient.Version()`), SHALL отримати `Async`-перейменування, а старі — лишитися як `[Obsolete]`-shim на один мажорний реліз.

#### Scenario: VersionAsync replaces Version
- **GIVEN** новий `Task<VersionResponse> VersionAsync(CancellationToken)`
- **WHEN** споживач компілює виклик `client.Version()`
- **THEN** компілятор показує `[Obsolete]` warning із вказівкою «Use VersionAsync»
- **AND** виклик працює (делегує на новий метод)

### Requirement: Explicit CancellationToken on Send* methods
`ISignalMessage.Send*Async` SHALL приймати `CancellationToken cancellationToken = default` як останній параметр (поряд із полем `CancellationToken` у відповідних `*Options`). Якщо передано обидва, бібліотека SHALL лінкувати їх через `CreateLinkedTokenSource`.

#### Scenario: Token from parameter is honored
- **GIVEN** `SendTextMessageAsync(options, ctsFromCaller.Token)`
- **WHEN** `ctsFromCaller.Cancel()` викликано до завершення RPC
- **THEN** Task завершується з `OperationCanceledException`, токен якого == `ctsFromCaller.Token`

#### Scenario: Options field is honored when parameter is default
- **GIVEN** `options.CancellationToken = ctsFromOptions.Token`, виклик `SendTextMessageAsync(options)` (без параметра)
- **WHEN** `ctsFromOptions.Cancel()` викликано до завершення RPC
- **THEN** Task завершується з `OperationCanceledException`

### Requirement: IJsonRpcClient is IAsyncDisposable-only
`IJsonRpcClient` SHALL імплементувати тільки `IAsyncDisposable` (а не одночасно `IDisposable` і `IAsyncDisposable`). Реалізація SHALL використовувати `await using` або делегування з DI-контейнера; синхронного `Dispose()`-fallback із `GetAwaiter().GetResult()` SHALL не бути.

#### Scenario: No sync-over-async in disposal
- **WHEN** оглядається код `JsonRpcClient` після зміни
- **THEN** не існує `Dispose()`-методу, що блокує на `DisposeAsync().AsTask().GetAwaiter().GetResult()`

#### Scenario: DI host disposes via DisposeAsync
- **GIVEN** `JsonRpcClient` зарезолвлено через DI
- **WHEN** `host.StopAsync()` викликано
- **THEN** DI-контейнер викликає `DisposeAsync()`, а не `Dispose()`

### Requirement: Synchronous JSON-RPC client factory
`IJsonRpcClientFactory.Create()` (синхронний) SHALL замінити `CreateAsync()`, який нічого асинхронно не робить. Це усуває фейковий `Async`-суфікс і відповідає конвенції про чесні сигнатури.

#### Scenario: Factory returns directly
- **WHEN** викликається `factory.Create()`
- **THEN** новий `JsonRpcClient` повертається синхронно, без `Task.FromResult`-обгортки

### Requirement: Cancellation tokens propagate through TrySetCanceled
Усі виклики `TaskCompletionSource<T>.TrySetCanceled` у бібліотеці SHALL передавати `CancellationToken`, що відповідає причині скасування (caller-токен, dispose-токен, stream-change-маркер). Це робить `OperationCanceledException.CancellationToken` діагностично корисним.

#### Scenario: Disposal carries dispose token
- **GIVEN** `JsonRpcClient.DisposeAsync()` виставив `_disposeCts.Token`
- **WHEN** очікуючий запит фейлиться через `TrySetCanceled(_disposeCts.Token)`
- **THEN** `OperationCanceledException.CancellationToken == _disposeCts.Token`

### Requirement: Strongly-typed TextStyleMode replaces stringly-typed flag
Внутрішній `SignalMessage.SendUnifiedMessageAsync` SHALL приймати `TextStyleMode` enum замість `string? textMode`. Зовнішнє API (`*Options.UseStyle`) лишається `bool` для backward compat.

#### Scenario: enum eliminates magic strings
- **WHEN** оглядається код `SignalMessage`
- **THEN** немає рядкових порівнянь типу `textMode.Equals("styled", …)` ; натомість `switch` по `TextStyleMode`

### Requirement: AtomicCounter is simple and contention-free
`Utilities/AtomicCounter` SHALL бути реалізованим як `unchecked((int)Interlocked.Increment(ref _seed))` без CAS-reset гілки. Поведінка: int32 wraparound (нормальна для request-id).

#### Scenario: Concurrent increments produce unique values
- **WHEN** 4 потоки роблять по 100 000 викликів `Increment()`
- **THEN** усі 400 000 значень різні (mod 2^32) — жоден не дублюється

### Requirement: CallerArgumentExpression on Validate helpers
Усі статичні `Validate*`-помічники в бібліотеці SHALL використовувати `[CallerArgumentExpression(nameof(arg))]` для параметра `paramName`, замість захардкоженого рядка.

#### Scenario: Validation throws with caller-supplied param name
- **GIVEN** `ValidateRecipients(myRecipients)` де `myRecipients` порожній
- **WHEN** валідатор кидає `ArgumentException`
- **THEN** `ParamName == "myRecipients"` (а не дефолтний `"recipients"`)

### Requirement: No empty IDisposable on stateless facades
Класи `SignalAccounts`, `SignalDevices`, `SignalGroups`, `SignalService`, `SignalMessage` SHALL не імплементувати `IDisposable`, оскільки вони не тримають ресурсів. Це усуває порожні `Dispose()` шумові методи.

#### Scenario: Facades are not IDisposable
- **WHEN** оглядаються типи `SignalAccounts`/`SignalDevices`/`SignalGroups`/`SignalService`/`SignalMessage`
- **THEN** жоден із них не реалізує `IDisposable`
- **AND** `CHANGELOG.md` явно зазначає це як breaking-change мінорного релізу `2.1.0` (бо public-surface)
