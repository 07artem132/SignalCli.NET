# `ISignalMessage` — відправка повідомлень

Сервіс відправки усіх вихідних повідомлень Signal: текст, вкладення, стікери, реакції, квитанції, typing-індикатори, віддалене видалення, polls, payment-notifications, message-request-response, pin/unpin, admin-delete. Усі методи — `Task<SendMessageResponse>` (одна відповідь з timestamp + per-recipient send-результатами); виключення: `SendMessageRequestResponseAsync` повертає `Task` (response порожній на wire).

Резолвиться з DI як `host.Services.GetRequiredService<ISignalMessage>()`. Без власного стану — facade навколо `ISignalCliClient.InvokeMethodAsync`.

**Спільні винятки** для усіх send-методів (опускаємо у per-метод секціях нижче):

| Виняток | Код signal-cli | Коли |
|---|---|---|
| `JsonRpcException` | будь-який | Базовий тип — інші помилки |
| `RateLimitException` | `-5` | Server-side rate-limit для усіх recipient'ів. `Error.Data` містить challenge token |
| `UntrustedIdentityException` | `-4` | Identity recipient'а неверифікована. Розрізнити first-contact vs re-install — client-side через `ISignalContacts.ListIdentitiesAsync` (upstream signal-cli не розрізняє ці кейси на wire — pinned fact #8 у `.claude/rules/signal-cli-protocol.md`) |
| `CaptchaRequiredException` | `-6` | CAPTCHA challenge — використай `ISignalAccounts.SubmitRateLimitChallengeAsync` |
| `GroupAdminRequiredException` | `-1` + "admin" | Group-only операція, акаунт — не admin |
| `OperationCanceledException` | — | `cancellationToken` cancelled |
| `TimeoutException` | — | signal-cli не відповів за `SignalCliOptions.RequestTimeoutSeconds` |

Усі XML doc'и методів цитують upstream signal-cli source (commit `bda4e7fc`) — per CLAUDE.md §0.5 anti-hallucination protocol.

---

## `SendTextMessageAsync`

```csharp
Task<SendMessageResponse> SendTextMessageAsync(
    TextMessageOptions options,
    CancellationToken cancellationToken = default);
```

Відправляє текстове повідомлення. Опціонально — з формат-стилями (`UseStyle()` → markdown-like syntax всередині `Message`), згадками (`WithMentions`), preview (`WithPreview`).

**signal-cli RPC:** `SendCommand.java` @ `bda4e7fc`.

```csharp
var opts = new TextMessageOptions.Builder(
        account: "+380501234567",
        recipients: [new UserRecipient("+380501234567")],
        message: "**Привіт**, _світ_!")
    .UseStyle()
    .Build();
var response = await signalMessage.SendTextMessageAsync(opts);
Console.WriteLine($"Sent timestamp: {response.Timestamp}");
```

**Стилі (`UseStyle()`):** `*курсив*`, `**жирний**`, `` `моноширинний` ``, `~закреслений~`, `||спойлер||`.

---

## `SendAttachmentAsync`

```csharp
Task<SendMessageResponse> SendAttachmentAsync(
    AttachmentMessageOptions options,
    CancellationToken cancellationToken = default);
```

Відправляє повідомлення з вкладеннями (файли). Caption опціональний (`WithMessage`). Бібліотека автоматично перемикається між inline data-URI (для малих ≤ 12 МБ raw) і temp-file path (для більших) — Jackson `maxStringLength` 20M + 4M margin.

**signal-cli RPC:** `SendCommand.java` (з `attachments`) @ `bda4e7fc`. Max 100 МБ per вкладення.

```csharp
var opts = new AttachmentMessageOptions.Builder(
        account: "+380501234567",
        recipients: [new UserRecipient("+380501234567")],
        attachments: ["/path/to/photo.jpg", "/path/to/doc.pdf"])
    .WithMessage("Документи + фото")
    .Build();
await signalMessage.SendAttachmentAsync(opts);
```

**Безпека:** `FileName` атачмента sanitize'ується через `Path.GetFileName` (CLAUDE.md rule #3) — захист від path-traversal.

---

## `SendStickerAsync`

```csharp
Task<SendMessageResponse> SendStickerAsync(
    StickerMessageOptions options,
    CancellationToken cancellationToken = default);
```

Відправляє стікер. Sticker ідентифікується рядком `packId:stickerIndex`.

**signal-cli RPC:** `SendCommand.java` (з `sticker`) @ `bda4e7fc`.

```csharp
var opts = new StickerMessageOptions.Builder(
        account: "+380501234567",
        recipients: [new UserRecipient("+380501234567")],
        sticker: "abc123def456:5")
    .Build();
await signalMessage.SendStickerAsync(opts);
```

Для отримання `packId` — `ISignalStickers.ListStickerPacksAsync`.

---

## `SendReactionAsync`

```csharp
Task<SendMessageResponse> SendReactionAsync(
    ReactionOptions options,
    CancellationToken cancellationToken = default);
```

Відправляє emoji-реакцію на існуюче повідомлення. `targetTimestamp` — sent-timestamp ОРИГІНАЛЬНОГО повідомлення; `targetAuthor` — автор оригінального (E.164 або UUID). Для зняття — `AsRemove()`.

**signal-cli RPC:** `SendReactionCommand.java` @ `bda4e7fc`.

**§F20 quirk:** єдиний send-метод з `NotifySelf`. Recipient'и можуть бути в будь-якій комбінації: phone-numbers (`WithRecipients`), group IDs (`WithGroupIds`), usernames (`WithUsernames`), або note-to-self (`WithNoteToSelf`).

```csharp
var opts = new ReactionOptions.Builder(
        account: "+380501234567",
        emoji: "👍",
        targetTimestamp: 1716732456000)
    .WithRecipients(["+380501234567"])
    .WithTargetAuthor("+380501234567")
    .Build();
await signalMessage.SendReactionAsync(opts);
```

---

## `SendReceiptAsync`

```csharp
Task<SendMessageResponse> SendReceiptAsync(
    ReceiptOptions options,
    CancellationToken cancellationToken = default);
```

Відправляє read/viewed receipt автору повідомлення. `targetTimestamps` — масив (один receipt може квитувати кілька повідомлень).

**signal-cli RPC:** `SendReceiptCommand.java` @ `bda4e7fc`.

**§F7 quirk:** `Recipient` — singular string, не масив (унікально для receipt). **Не** марк'ить повідомлення прочитаним локально — це notification до remote sender'а.

```csharp
var opts = new ReceiptOptions.Builder(
        account: "+380501234567",
        recipient: "+380501234567",
        targetTimestamps: [1716732456000, 1716732470000])
    .WithType(ReceiptType.Read)
    .Build();
await signalMessage.SendReceiptAsync(opts);
```

`ReceiptType`: `Read` | `Viewed`.

---

## `SendTypingAsync`

```csharp
Task<SendMessageResponse> SendTypingAsync(
    TypingOptions options,
    CancellationToken cancellationToken = default);
```

Відправляє typing-індикатор (START або STOP-через-`AsStop()`). START auto-expires через ~15 секунд у receiver'а — explicit STOP опційний.

**signal-cli RPC:** `SendTypingCommand.java` @ `bda4e7fc`.

```csharp
var opts = new TypingOptions.Builder(account: "+380501234567")
    .WithRecipients(["+380501234567"])
    .Build();   // START
await signalMessage.SendTypingAsync(opts);

// Через 5 секунд — STOP:
var stop = new TypingOptions.Builder(account: "+380501234567")
    .WithRecipients(["+380501234567"])
    .AsStop()
    .Build();
await signalMessage.SendTypingAsync(stop);
```

---

## `SendRemoteDeleteAsync`

```csharp
Task<SendMessageResponse> SendRemoteDeleteAsync(
    RemoteDeleteOptions options,
    CancellationToken cancellationToken = default);
```

Попросити recipient'ів видалити власну копію повідомлення (`targetTimestamp` — sent-timestamp оригіналу). Best-effort: Signal-клієнти можуть відмовити (поза часовим вікном). **Локальна копія у signal-cli не видаляється** — consumer'и трекають sent-messages самі.

**signal-cli RPC:** `RemoteDeleteCommand.java` @ `bda4e7fc`.

```csharp
var opts = new RemoteDeleteOptions.Builder(
        account: "+380501234567",
        targetTimestamp: 1716732456000)
    .WithRecipients(["+380501234567"])
    .Build();
await signalMessage.SendRemoteDeleteAsync(opts);
```

---

## `SendPollCreateAsync`

```csharp
Task<SendMessageResponse> SendPollCreateAsync(
    SendPollCreateOptions options,
    CancellationToken cancellationToken = default);
```

Створює новий poll. **§F15 валідація client-side:** 2-10 варіантів, ≤ 100 chars кожен.

**signal-cli RPC:** `SendPollCreateCommand.java` @ `bda4e7fc`.

```csharp
var opts = new SendPollCreateOptions.Builder(
        account: "+380501234567",
        question: "Який день обираємо для дзвінка?",
        pollOptions: ["Понеділок", "Вівторок", "Середа"])
    .WithGroupIds(["base64GroupId"])
    .WithAllowMultipleVotes(false)
    .Build();
var resp = await signalMessage.SendPollCreateAsync(opts);
long pollTimestamp = resp.Timestamp; // знадобиться для Vote/Terminate
```

---

## `SendPollVoteAsync`

```csharp
Task<SendMessageResponse> SendPollVoteAsync(
    SendPollVoteOptions options,
    CancellationToken cancellationToken = default);
```

Голосує у polls. **§F22 zero-based indexes** — позиції варіантів з 0. `VoteCount` monotonic per-voter (re-vote = новий vote з вищим counter).

**signal-cli RPC:** `SendPollVoteCommand.java` @ `bda4e7fc`.

```csharp
var opts = new SendPollVoteOptions.Builder(
        account: "+380501234567",
        pollAuthor: "+380501234567",
        pollTimestamp: pollTimestamp,
        optionIndexes: [0])   // "Понеділок"
    .WithGroupIds(["base64GroupId"])
    .Build();
await signalMessage.SendPollVoteAsync(opts);
```

---

## `SendPollTerminateAsync`

```csharp
Task<SendMessageResponse> SendPollTerminateAsync(
    SendPollTerminateOptions options,
    CancellationToken cancellationToken = default);
```

Завершує poll. **Terminator MUST be original author** (немає admin-override).

**signal-cli RPC:** `SendPollTerminateCommand.java` @ `bda4e7fc`.

```csharp
var opts = new SendPollTerminateOptions.Builder(
        account: "+380501234567",
        pollAuthor: "+380501234567",
        pollTimestamp: pollTimestamp)
    .WithGroupIds(["base64GroupId"])
    .Build();
await signalMessage.SendPollTerminateAsync(opts);
```

---

## `SendAdminDeleteAsync`

```csharp
Task<SendMessageResponse> SendAdminDeleteAsync(
    SendAdminDeleteOptions options,
    CancellationToken cancellationToken = default);
```

Admin-delete повідомлення у групі. **Group-only**: ctor вимагає `groupIds`. Server-side rejects якщо не admin.

**signal-cli RPC:** `SendAdminDeleteCommand.java` @ `bda4e7fc`.

```csharp
var opts = new SendAdminDeleteOptions.Builder(
        account: "+380501234567",
        targetAuthor: "+380501234567",  // автор повідомлення яке видаляємо
        targetTimestamp: 1716732456000,
        groupIds: ["base64GroupId"])
    .Build();
await signalMessage.SendAdminDeleteAsync(opts);
```

---

## `SendPinMessageAsync`

```csharp
Task<SendMessageResponse> SendPinMessageAsync(
    SendPinMessageOptions options,
    CancellationToken cancellationToken = default);
```

Pin повідомлення. **§F23 sentinel:** `WithPinDurationSeconds(-1)` = forever.

**signal-cli RPC:** `SendPinMessageCommand.java` @ `bda4e7fc`.

```csharp
var opts = new SendPinMessageOptions.Builder(
        account: "+380501234567",
        targetAuthor: "+380501234567",
        targetTimestamp: 1716732456000)
    .WithGroupIds(["base64GroupId"])
    .WithPinDurationSeconds(-1)   // forever
    .Build();
await signalMessage.SendPinMessageAsync(opts);
```

---

## `SendUnpinMessageAsync`

```csharp
Task<SendMessageResponse> SendUnpinMessageAsync(
    SendUnpinMessageOptions options,
    CancellationToken cancellationToken = default);
```

Unpin повідомлення. Symmetric до Pin без duration.

**signal-cli RPC:** `SendUnpinMessageCommand.java` @ `bda4e7fc`.

---

## `SendMessageRequestResponseAsync`

```csharp
Task SendMessageRequestResponseAsync(
    string account,
    MessageRequestResponseType type,
    IEnumerable<string>? recipients = null,
    IEnumerable<string>? groupIds = null,
    IEnumerable<string>? usernames = null,
    CancellationToken cancellationToken = default);
```

Sync message що ділиться з linked devices ACK'ом message-request'у (новий контакт → accept/delete). **§F2:** на send-side підтримуються тільки `Accept` і `Delete`. Block-стиль — через `ISignalContacts.BlockAsync`.

**signal-cli RPC:** `SendMessageRequestResponseCommand.java` @ `bda4e7fc`. Wire response порожній — повертає `Task`, не `Task<SendMessageResponse>`.

```csharp
await signalMessage.SendMessageRequestResponseAsync(
    account: "+380501234567",
    type: MessageRequestResponseType.Accept,
    recipients: ["+380509999999"]);
```

`MessageRequestResponseType`: `Accept` | `Delete`.

---

## `SendPaymentNotificationAsync`

```csharp
Task<SendMessageResponse> SendPaymentNotificationAsync(
    string account,
    string recipient,
    string receiptBase64,
    string? note = null,
    CancellationToken cancellationToken = default);
```

Sends payment notification. **Single recipient only.** `receiptBase64` — base64 MobileCoin receipt blob (отриманий з off-band payment flow; .NET wrapper не виконує MobileCoin-операції).

**signal-cli RPC:** `SendPaymentNotificationCommand.java` @ `bda4e7fc`.

```csharp
await signalMessage.SendPaymentNotificationAsync(
    account: "+380501234567",
    recipient: "+380509999999",
    receiptBase64: "ASkRGGE6...",
    note: "За каву");
```

> ⚠ **Wire shape evolution.** У 4.9.0 `JsonPayment` shape виправлено з гіпотетичного `(Amount: decimal, Currency: string?)` на реальний upstream `(Note: string?, Receipt: byte[])`. У 4.10.0 `Receipt` зроблено nullable (`byte[]?`) — upstream Java has no NRT, `"receipt": null` AND missing-field cases deliver `null` до consumer'а. Використовуй `payment.Receipt?.Length`. Деталі — `CHANGELOG.md [4.9.0]` + `[4.10.0]`.
