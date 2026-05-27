# `ISignalEventService` — підписка та потоки подій

Підписується на receive-події з акаунту і виставляє їх **двома парними поверхнями** для кожного з 17 event-kind'ів:

- **`IObservable<T>` (Rx)** — fan-out / broadcast. Кожен subscriber отримує копію кожного event'у. Не-blocking, OnNext синхронний.
- **`IAsyncEnumerable<T>` (Channels)** — exclusive consumption + back-pressure. Кожен елемент читає **рівно один** споживач. `Channel.CreateBounded<T>(1024, FullMode = DropOldest)`; drop логується на Debug + counter `signalcli.events.dropped`.

Симетрія enforce'ується `EventApiSymmetryTests` (RG06). Реалізує `IHostedService` — старт/стоп автоматичні через `AddSignalEvents()`.

Резолвиться як `host.Services.GetRequiredService<ISignalEventService>()`.

---

## Lifecycle

### `SubscribeAsync`

```csharp
Task<SubscribeReceiveResponse> SubscribeAsync(
    string account,
    CancellationToken cancellationToken = default);
```

Підписується на отримання подій з акаунту. **Idempotent** (audit N5 / CLAUDE.md rule #14): повторні виклики для того ж `account` повертають той самий `SubscribeReceiveResponse.Id`.

**Винятки:** `ArgumentException` якщо `account` — `null` або порожній; `JsonRpcException`; `TimeoutException` (`RequestTimeoutSeconds`).

```csharp
var resp = await eventService.SubscribeAsync("+380501234567");
Console.WriteLine($"Subscription id: {resp.Id}");
```

### `UnsubscribeAsync`

```csharp
Task<UnsubscribeReceiveResponse> UnsubscribeAsync(
    int subscriptionId,
    CancellationToken cancellationToken = default);
```

Відписується. `subscriptionId` — той, що повернув `SubscribeAsync`.

**Винятки:** `ArgumentException` якщо `subscriptionId` невалідний; `JsonRpcException`.

---

## Поверхні подій (17 × 2)

Усі властивості/методи нижче — спарені: для кожного event-kind'у є `IObservable<TEventArgs> X { get; }` (Rx) і `IAsyncEnumerable<TEventArgs> XAsync(CancellationToken)` (Channels).

| Event-kind | `IObservable<T>` property | `IAsyncEnumerable<T>` method | EventArgs |
|---|---|---|---|
| Текстові повідомлення | `TextMessages` | `TextMessagesAsync` | `TextMessageEventArgs` |
| Реакції | `Reaction` | `ReactionAsync` | `ReactionEventArgs` |
| Вкладення | `Attachments` | `AttachmentsAsync` | `AttachmentEventArgs` |
| Стікери | `Sticker` | `StickerAsync` | `StickerEventArgs` |
| Typing | `TypingNotifications` | `TypingAsync` ⚠ | `TypingEventArgs` |
| Квитанції | `Receipts` | `ReceiptsAsync` | `ReceiptEventArgs` |
| Синхронізація | `Syncs` | `SyncsAsync` | `SyncEventArgs` |
| Цитати | `Quotes` | `QuotesAsync` | `QuoteEventArgs` |
| Редагування | `Edits` | `EditsAsync` | `EditEventArgs` |
| Remote-delete | `RemoteDeletes` | `RemoteDeletesAsync` | `RemoteDeleteEventArgs` |
| Poll create | `PollCreates` | `PollCreatesAsync` | `PollCreateEventArgs` |
| Poll vote | `PollVotes` | `PollVotesAsync` | `PollVoteEventArgs` |
| Poll terminate | `PollTerminates` | `PollTerminatesAsync` | `PollTerminateEventArgs` |
| Payments | `Payments` | `PaymentsAsync` | `PaymentEventArgs` |
| Pin | `PinMessages` | `PinMessagesAsync` | `PinMessageEventArgs` |
| Unpin | `UnpinMessages` | `UnpinMessagesAsync` | `UnpinMessageEventArgs` |
| Admin-delete | `AdminDeletes` | `AdminDeletesAsync` | `AdminDeleteEventArgs` |

⚠ **`TypingAsync`, не `TypingNotificationsAsync`** — historical naming asymmetry, єдина у таблиці.

---

## Коли яку поверхню обирати

| Сценарій | Поверхня | Чому |
|---|---|---|
| Один pipeline що споживає всі повідомлення з back-pressure | `IAsyncEnumerable<T>` | `await foreach` + auto-cancellation; bounded channel = бібліотека сама перестане pull'ати з signal-cli якщо ти повільний |
| Декілька паралельних handler'ів читають **один і той же** event | `IObservable<T>` | Rx fan-out; кожен subscriber отримує копію |
| Стандартний `BackgroundService` worker | `IAsyncEnumerable<T>` | Передай `stoppingToken` у `XAsync(stoppingToken)` — graceful shutdown працює |
| Інтеграція з існуючим Rx-pipeline (Select/Where/Buffer) | `IObservable<T>` | Native Rx-operators |
| Drop-oldest семантика при slow-consumer | `IAsyncEnumerable<T>` | Channel.CreateBounded(1024, DropOldest) — найстаріший elem мовчки drop'ається з лог-counter'ом |

**Не змішуй у одному handler'і.** Один event буде delivered або через Rx subject, або через Channel — не через обидва.

---

## Приклади

### Текстові повідомлення через `IAsyncEnumerable<T>`

```csharp
await eventService.SubscribeAsync("+380501234567");

await foreach (var msg in eventService.TextMessagesAsync(stoppingToken))
{
    Console.WriteLine($"[{msg.SourceNumber ?? msg.SourceUuid}] {msg.DataMessage.Message}");
}
```

`TextMessageEventArgs` поля: `Account` (E.164), `SourceNumber`, `SourceUuid`, `DataMessage` (тіло, mentions, attachments, ...), `EnvelopeTimestamp`.

### Реакції через Rx (broadcast)

```csharp
using var sub1 = eventService.Reaction.Subscribe(args =>
{
    Console.WriteLine($"[stats] reaction {args.Reaction.Emoji} on msg {args.Reaction.TargetSentTimestamp}");
});
using var sub2 = eventService.Reaction.Subscribe(args =>
{
    metrics.Counter("reactions_seen").Inc();
});
// Обидва subscribers отримують кожну reaction.
```

### Polls — підписка на 3 потоки одночасно

```csharp
var creates = eventService.PollCreatesAsync(stoppingToken);
var votes = eventService.PollVotesAsync(stoppingToken);
var terminates = eventService.PollTerminatesAsync(stoppingToken);

_ = Task.Run(async () => { await foreach (var c in creates) Handle(c); }, stoppingToken);
_ = Task.Run(async () => { await foreach (var v in votes) Handle(v); }, stoppingToken);
_ = Task.Run(async () => { await foreach (var t in terminates) Handle(t); }, stoppingToken);
```

---

## DataMessage union-семантика

DataMessage — **presence-based union**: один envelope може одночасно тригерити кілька потоків (наприклад text + attachment + quote). `SignalEventService` емітує **кожен applicable** потік + його парну Channel; не reintroduce'ить early-return між payload-checks (CLAUDE.md rule #4).

Приклад: користувач надсилає фото з caption "Дивись!" + reply на попереднє повідомлення. У ту мить fire'ять:
- `TextMessagesAsync` / `TextMessages` (тому що `DataMessage.Message != null`);
- `AttachmentsAsync` / `Attachments` (тому що `DataMessage.Attachments` непорожній);
- `QuotesAsync` / `Quotes` (тому що `DataMessage.Quote != null`).

---

## Cancellation

Передавай `stoppingToken` у `XAsync(stoppingToken)` — `await foreach` нормально вийде при cancel.

Для Rx — `IDisposable` від `.Subscribe(...)` стандартний disposal pattern, або `host.StoppingToken` через `TakeUntil`/`Take(...)` rx-operator'и.

---

## Реєстрація

`AddSignalEvents()` — окремий розширюючий extension (не входить в `AddSignalCli`):

```csharp
services.AddSignalCli(opts => { /* ... */ });
services.AddSignalEvents();   // ідемпотентно
```

Деталі — [`docs/api/di-options.md`](di-options.md).
