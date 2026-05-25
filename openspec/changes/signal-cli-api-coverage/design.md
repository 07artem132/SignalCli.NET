# Design — signal-cli-api-coverage

## Method

8 release waves, кожен — own PR, own minor-bump. Cluster ordering за value-to-risk: highest-frequency-in-bots first, destructive ops behind explicit opt-in last.

```
4.1.0 messaging-interactive            (4 methods, low risk)
   │
   ▼
4.2.0 groups-crud                       (3 methods, medium risk — complex DTOs)
   │
   ▼
4.3.0 contacts-identity                 (8 methods, low risk — mostly read)
   │
   ▼
4.4.0 sticker-packs + binary-fetch      (6 methods, low risk — base64 helpers)
   │
   ▼
4.5.0 device-management                 (4 methods, medium risk — primary-side ops)
   │
   ▼
4.6.0 account-lifecycle                 (8 methods, HIGH risk — opt-in gated)
   │
   ▼
4.7.0 polls + messaging-power-user      (8 methods, medium risk — group admin)
   │
   ▼
4.8.0 utility-rpc                       (3 methods, low risk)
```

Waves 1-5 + 7-8 — additive, no breaking changes. Wave 6 — additive (new options-property + new methods); destructive-by-design але gated.

## 1. Cross-cutting infrastructure

### 1.1 `SignalCliOptions.EnableDestructiveOperations` (lands в Wave 6, але документується тут для cross-wave awareness)

```csharp
public sealed class SignalCliOptions
{
    // ... existing properties ...

    /// <summary>
    /// Якщо <c>true</c>, дозволяє виклик незворотних операцій: <c>unregister</c>,
    /// <c>deleteLocalAccountData</c>, <c>setPin</c>/<c>removePin</c>, <c>startChangeNumber</c>/
    /// <c>finishChangeNumber</c>, <c>updateAccount</c>, <c>updateConfiguration</c>.
    /// За замовчуванням <c>false</c> — спроба виклику кине
    /// <see cref="InvalidOperationException"/> з підказкою.
    /// </summary>
    /// <remarks>
    /// Захист від accidental misuse у production. Consumer що дійсно хоче destructive
    /// operations (наприклад, account-management dashboard) виставляє флаг явно.
    /// </remarks>
    public bool EnableDestructiveOperations { get; set; } = false;
}
```

`SignalAccounts` constructor reads `_options.Value.EnableDestructiveOperations` once at construction (per critical rule #10 — fail-fast at constructor). Кожен destructive метод робить guard:

```csharp
public async Task<UnregisterResponse> UnregisterAsync(...)
{
    if (!_destructiveOpsEnabled)
        throw new InvalidOperationException(
            "Destructive operation 'unregister' is disabled. " +
            "Set SignalCliOptions.EnableDestructiveOperations = true to enable.");
    // ... RPC ...
}
```

### 1.2 Нові typed exceptions

Land у Wave 1 (messaging-interactive), бо `IdentityChangedException` потрібний для `sendReaction`/`sendReceipt` (signal-cli повертає `-4` коли recipient'ова identity змінилася між send-ами).

```csharp
// src/SignalCli/Exceptions/IdentityChangedException.cs
public sealed class IdentityChangedException(JsonRpcError error) : UntrustedIdentityException(error)
{
    // Code is always -4 by construction. Differs from UntrustedIdentityException only
    // in context: this fires on SEND-time identity-mismatch (recipient re-installed),
    // while base UntrustedIdentityException fires on any -4 (including initial contact).
}

// src/SignalCli/Exceptions/GroupAdminRequiredException.cs
public sealed class GroupAdminRequiredException(JsonRpcError error) : JsonRpcException(error);

// src/SignalCli/Exceptions/CaptchaRequiredException.cs
public sealed class CaptchaRequiredException(JsonRpcError error) : JsonRpcException(error);
```

Dispatch logic у `JsonRpcClient.InvokeMethodAsync` (existing switch — extended):

```csharp
throw response.Error.Code switch
{
    (int)JsonRpcErrorCode.RateLimit => new RateLimitException(response.Error),
    (int)JsonRpcErrorCode.UntrustedIdentity => new UntrustedIdentityException(response.Error),
    (int)JsonRpcErrorCode.CaptchaRejected => new CaptchaRequiredException(response.Error),
    (int)JsonRpcErrorCode.UserError when response.Error.Message?.Contains("admin", StringComparison.OrdinalIgnoreCase) == true
        => new GroupAdminRequiredException(response.Error),
    _ => new JsonRpcException(response.Error),
};
```

`IdentityChangedException` НЕ диспатчиться окремо — це seам same `-4`, але caller може catch'нути її spec'но коли він спеціально хоче handle re-install case (vs first-time-trust).

### 1.3 `SignalJsonContext` додавання — ~88 entries

Кожна нова `*Parameters`/`*Response` пара реєструється:

```csharp
// Wave 1 (messaging-interactive)
[JsonSerializable(typeof(SendReactionParameters))]
[JsonSerializable(typeof(SendReactionResponse))]
[JsonSerializable(typeof(SendReceiptParameters))]
[JsonSerializable(typeof(SendReceiptResponse))]
[JsonSerializable(typeof(SendTypingParameters))]
[JsonSerializable(typeof(SendTypingResponse))]
[JsonSerializable(typeof(RemoteDeleteParameters))]
[JsonSerializable(typeof(RemoteDeleteResponse))]
// ... continues per wave ...
```

`R01` (`JsonContextRegistrationTests`) reflectively enumerates `InvokeMethodAsync<TRequest, TResponse>` call-sites and fails build if any `T` is not in context. Це наш forcing function — забув зареєструвати = test failure.

### 1.4 Namespace + folder layout

Existing pattern зберігається:

```
src/SignalCli/Models/Signal/
├── Accounts/         (existing + 8 new for account-lifecycle)
├── Contacts/         (NEW — for contacts-identity)
├── Devices/          (existing + 4 new for device-management)
├── Groups/           (existing + 3 new for groups-crud)
├── Message/          (existing + 4 + 3 + 5 = 12 new for messaging-*)
├── Resources/        (NEW — for binary-resource-fetch)
├── Stickers/         (NEW — for sticker-packs)
└── Utility/          (NEW — for utility-rpc)

src/SignalCli/Interfaces/Signal/
├── ISignalAccounts.cs  (existing + 8 + 3 new methods)
├── ISignalContacts.cs  (NEW)
├── ISignalDevices.cs   (existing + 4 new methods)
├── ISignalGroups.cs    (existing + 3 new methods)
├── ISignalMessage.cs   (existing + 4 + 3 + 5 = 12 new methods)
├── ISignalResources.cs (NEW)
└── ISignalStickers.cs  (NEW)

src/SignalCli/Services/Signal/
├── SignalAccounts.cs
├── SignalContacts.cs   (NEW)
├── SignalDevices.cs
├── SignalGroups.cs
├── SignalMessage.cs
├── SignalResources.cs  (NEW)
└── SignalStickers.cs   (NEW)
```

Three нові services реєструються у `ServiceCollectionExtensions.AddSignalCli`:

```csharp
services.TryAddSingleton<ISignalContacts, SignalContacts>();
services.TryAddSingleton<ISignalResources, SignalResources>();
services.TryAddSingleton<ISignalStickers, SignalStickers>();
```

### 1.5 DTO conventions

Усі нові DTO — `sealed record` з `[JsonPropertyName]` per critical rule #6, `[PublicAPI]` per existing pattern, XMLDoc на українській (`.claude/rules/conventions.md`).

```csharp
[PublicAPI]
public sealed record SendReactionParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("recipient")] IReadOnlyList<string>? Recipient,
    [property: JsonPropertyName("groupId")] IReadOnlyList<string>? GroupId,
    [property: JsonPropertyName("emoji")] string Emoji,
    [property: JsonPropertyName("targetAuthor")] string TargetAuthor,
    [property: JsonPropertyName("targetTimestamp")] long TargetTimestamp,
    [property: JsonPropertyName("remove")] bool? Remove);
```

Параметри з default-значеннями (`bool? remove = false`) використовують nullable + `JsonSerializerOptions.DefaultIgnoreCondition = WhenWritingNull` (existing у `SignalJson.Options`) — щоб signal-cli не отримував зайвих `null`-полів, які він треактує строго.

Response DTOs дзеркалять `SendMessageResponse` pattern для send-методів (з `Timestamp` + `Results`):

```csharp
[PublicAPI]
public sealed record SendReactionResponse(
    [property: JsonPropertyName("timestamp")] long Timestamp,
    [property: JsonPropertyName("results")] IReadOnlyList<SendResult>? Results);
```

де `SendResult` — спільний DTO для всіх send-методів (existing у `Message/`).

### 1.6 Service-method conventions

Всі service-методи дотримуються existing pattern (з `SignalGroups.ListGroupsAsync`):

```csharp
public async Task<TResponse> XxxAsync(TOptions options, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(options);
    options.Validate();  // for compound options-records
    SignalXxxLog.XxxRequested(_logger);  // method-name only, NO PII

    var response = await _signalCliClient
        .InvokeMethodAsync(
            "xxxRpcMethod",
            options.ToParameters(),  // mapping helper on options-record
            SignalJsonContext.Default.XxxParameters,
            SignalJsonContext.Default.XxxResponse,
            cancellationToken).ConfigureAwait(false);

    if (response is null)
    {
        SignalXxxLog.XxxNullResponse(_logger);
        throw new InvalidOperationException("Отримано нульову відповідь від сервера");
    }
    return response;
}
```

Validation — boundary-only (critical rule from `agent-friendly-api`); жодного internal-state caching у service'ах (вони stateless façades).

### 1.7 Logging — `[LoggerMessage]` EventId blocks

Existing blocks:
- 100-199: `SignalCliHostedServiceLog`
- 200-299: `JsonRpcClientLog`
- 300-399: `SignalEventServiceLog`
- 400-449: `SignalMessageLog`
- 450-499: `SignalAccountsLog`
- 500-549: `SignalDevicesLog`
- 550-599: `SignalGroupsLog`
- ... reserved per `.claude/rules/patterns.md` § Logging

Нові EventId blocks (виділяються при імплементації):
- 600-649: `SignalContactsLog` (Wave 3)
- 650-679: `SignalStickersLog` (Wave 4)
- 680-699: `SignalResourcesLog` (Wave 4)

Per service нових методів ~3-5 messages (Requested, NullResponse, ValidationFailed). Total ~50 нових `[LoggerMessage]` entries. Жодного `Information+` сайту не повинно reference'ити PII поля (recipient phone, message body, file path) — critical rule #1.

`R02` (`EventIdBlockTests`) автоматично pin'ає EventId blocks per service-class.

### 1.8 Тестова стратегія

**Per method** (mandatory):
1. **Serialization roundtrip test** у `Tests/SignalCli.Tests/Serialization/<Wave>SerializationTests.cs` — `JsonSerializer.Serialize(params, ctx)` + `Deserialize(json, ctx)` повертає eq-equivalent об'єкт; JSON shape матчить snapshot з реального signal-cli payload'у (capture через manual investigation, embed як inline-literal у тесті).
2. **InvokeMethodAsync wiring test** у `Tests/SignalCli.Tests/Services/<Service>Tests.cs` — mock'ит `ISignalCliClient`, asserts (a) method-name string ("sendReaction" тощо), (b) `params` JSON shape, (c) повернений `response` пробрасується.
3. **Validation test** для compound options-records — пустий account, empty recipients, конфліктні поля (recipient AND groupId) — кидає `ArgumentException` з правильним `paramName`.

**Per service** (mandatory):
4. **Constructor null-arg test** — кожен `ArgumentNullException.ThrowIfNull` у constructor'і.

**Per wave** (mandatory):
5. **PublicApiSurfaceTests** baseline update — `SignalCli.public-api.txt` оновлюється з новим API.

**E2E** (per matrix table в proposal — тільки для безпечних read-only методів, env-gated):
6. `Tests/SignalCli.Tests.Integration/SignalCliE2E<Method>Tests.cs` — запускає реальний signal-cli (bundled-JRE), викликає метод, assertion'ить shape. НЕ мутує persistent state.

**Env-gating helper** (нова утиліта, lands у Wave 3 разом з першим account-залежним E2E):

```csharp
// Tests/SignalCli.Tests.Integration/TestAccountFixture.cs
internal static class TestAccountFixture
{
    public const string EnvVar = "SIGNALCLI_TEST_ACCOUNT";

    public static string? TryGet() => Environment.GetEnvironmentVariable(EnvVar);

    public static string GetOrSkip()
    {
        var account = TryGet();
        if (string.IsNullOrEmpty(account))
            throw SkipException.ForReason(
                $"No {EnvVar} env var; integration test requires registered test account. " +
                "Set e.g. SIGNALCLI_TEST_ACCOUNT=+12025550100 to run.");
        return account;
    }
}
```

(`SkipException` — xunit-skip-ext або власний `DynamicSkipException : Exception` що xunit-runner трактує як skip; existing `TryBuildHost` уже використовує `out string skipReason` + `Assert.Skip` — той самий механізм.)

Кожен account-залежний E2E тест починається з:
```csharp
[Fact]
public async Task ListContacts_Returns_Empty_Or_Populated()
{
    var account = TestAccountFixture.GetOrSkip();   // ← skip if env var missing
    var host = TryBuildHost(out var skipReason);
    if (host is null) { Assert.Skip(skipReason); return; }
    await using var _ = host;
    // ... test body uses `account` ...
}
```

CI на public runners (без registered тестового номера) пропускає ці тести з clear reason; local developer setup з env-var — запускає реально. Жоден тест НЕ мутує persistent state account'а — лише read.

**Очікувана тестова дельта:**
- Wave 1: +12 unit + 0 E2E (всі mutating)
- Wave 2: +9 unit + 0 E2E
- Wave 3: +24 unit + 2 E2E (listContacts, listIdentities)
- Wave 4: +15 unit + 0 E2E *(was +18 before `sticker-pack-install` event decoder removal — see `research/SUMMARY.md` §F1)*
- Wave 5: +12 unit + 1 E2E (listDevices)
- Wave 6: +24 unit + 0 E2E + opt-in-flag tests
- Wave 7: +24 unit + 0 E2E
- Wave 8: +9 unit + 1 E2E (getUserStatus)

**Total: +129 unit + 4 E2E** *(was +132 — Wave 4 lost 3 tests after `sticker-pack-install` event decoder drop, see §F1)*. Поточна планка 290 → ~419 unit; E2E 2 → 6.

### 1.9 Receive-side event decoding — sourced from signal-cli's Java records

signal-cli `master` branch (verified at 2026-05-25) ships stable Jackson Java records у `src/main/java/org/asamk/signal/json/` що описують точний JSON envelope shape emitted на `subscribeReceive` notifications. Ці records — наш authoritative spec для decoder DTOs (не треба E2E capture).

Файли upstream'у з яких re-engineer'имо:

| signal-cli source | .NET decoder DTO | Used by event |
|---|---|---|
| `JsonDataMessage.java` (existing — extended with new fields) | `JsonDataMessage` (existing — `Envelope.cs`) | base envelope для нижче |
| `JsonPollCreate.java` (`question, allowMultiple, options`) | `JsonPollCreate` (new) | poll-create event |
| `JsonPollVote.java` | `JsonPollVote` (new) | poll-vote event |
| `JsonPollTerminate.java` | `JsonPollTerminate` (new) | poll-terminate event |
| `JsonPayment.java` | `JsonPayment` (new) | payment-notification event |
| `JsonPinMessage.java` | `JsonPinMessage` (new) | pin-message event |
| `JsonUnpinMessage.java` | `JsonUnpinMessage` (new) | unpin-message event |
| `JsonAdminDelete.java` | `JsonAdminDelete` (new) | admin-delete event |

> **2026-05-25 research correction** (see `research/SUMMARY.md` §F1, §F2): `JsonSyncMessage.java @ bda4e7fc` has no `stickerPackOperations` field, and `messageRequestResponse` sync-event uses an 8-value enum while the send-side Wave-7 enum has 2. Both sync-side rows previously listed here are **moved out of scope** — see `proposal.md` "Out of scope". Below table currently reflects 7 data-message decoders only; Wave 4 ships zero receive-side decoders.

**`Envelope.cs` extension shape** (Wave 7 — додає 7 nullable record fields у `JsonDataMessage`):

```csharp
public record JsonDataMessage(
    // ... existing fields ...
    [property: JsonPropertyName("pollCreate")] JsonPollCreate? PollCreate,
    [property: JsonPropertyName("pollVote")] JsonPollVote? PollVote,
    [property: JsonPropertyName("pollTerminate")] JsonPollTerminate? PollTerminate,
    [property: JsonPropertyName("payment")] JsonPayment? Payment,
    [property: JsonPropertyName("pinMessage")] JsonPinMessage? PinMessage,
    [property: JsonPropertyName("unpinMessage")] JsonUnpinMessage? UnpinMessage,
    [property: JsonPropertyName("adminDelete")] JsonAdminDelete? AdminDelete);
```

**`ISignalEventService` extension shape** (Wave 7 — додає 7 пар IObservable + IAsyncEnumerable; RG06 `EventApiSymmetryTests` enforce'ить парність):

```csharp
// IObservable side
IObservable<PollCreateEventArgs> PollCreates { get; }
IObservable<PollVoteEventArgs> PollVotes { get; }
IObservable<PollTerminateEventArgs> PollTerminates { get; }
IObservable<PaymentNotificationEventArgs> PaymentNotifications { get; }
IObservable<PinMessageEventArgs> PinMessages { get; }
IObservable<UnpinMessageEventArgs> UnpinMessages { get; }
IObservable<AdminDeleteEventArgs> AdminDeletes { get; }

// IAsyncEnumerable side (RG06 — кожен IObservable МАЄ паир)
IAsyncEnumerable<PollCreateEventArgs> PollCreatesAsync(CancellationToken ct = default);
IAsyncEnumerable<PollVoteEventArgs> PollVotesAsync(CancellationToken ct = default);
// ... etc ...
```

~~**Wave 4 окремо** — додає `IObservable<StickerPackInstallEventArgs> StickerPackInstalls` + `StickerPackInstallsAsync` через extension `JsonSyncMessage.StickerPackOperations: IReadOnlyList<JsonStickerPackOperation>?`.~~ **Removed 2026-05-25** — `JsonSyncMessage.java @ bda4e7fc` has no such field; upstream silently auto-installs without bubbling to JSON-RPC layer. See `proposal.md` "Out of scope" + `research/SUMMARY.md` §F1. Wave 4 ships ONLY 6 send-side methods (sticker/binary-fetch), zero receive-side decoders.

**`SignalEventService.DispatchDataMessage` extension** — додає 7 нових `if`-emissions у same presence-based union pattern (critical rule #4: `DataMessage` — presence-based union; жодного early `return` між payload checks; кожен payload emit'ить і IObservable і paired Channel):

```csharp
if (dm.PollCreate is not null)
{
    var args = new PollCreateEventArgs(envelope, dm.PollCreate);
    _pollCreateSubject.OnNext(args);
    _pollCreateChannel.Writer.TryWrite(args);  // bounded — back-pressure per existing pattern
}
// ... + 6 more ...
```

**SignalJsonContext** додатки: 7 нових `[JsonSerializable(typeof(Json*))]` entries + 7 `[JsonSerializable(typeof(*EventArgs))]` для `JsonNotificationRaw` deserialization.

**Тестова стратегія для decoders** — НЕ потребує live signal-cli (бо ми re-engineer'имо з source):
- Serialization roundtrip test з inline-literal JSON envelope crafted to match signal-cli's `JsonDataMessage` Java-record output (`Jackson default mapping` = camelCase fields, null-skip on write).
- DataMessage union-test: створити envelope з кількома payloads одночасно (e.g., `text + reaction + pollVote`) і assert що ВСІ три emit'ять у відповідні streams (critical rule #4 regression).
- Channel back-pressure test: 1001 events на capacity-1000 channel → 1 drop + counter increment (mirror existing pattern from `post-modernize-tuning §8c`).

**Реальний live E2E capture робиться лише ОДИН раз перед merge'ом Wave 7**, як sanity check: developer з env-var-account викликає `sendPollCreate` із другого пристрою свого ж акаунта, ловить notification, dump'ить raw JSON через `_logger.LogTrace`, порівнює з нашим inline-literal-snapshot. Якщо drift — оновлюємо snapshot ДО merge. Це 5-хвилинна manual procedure documented у `tasks.md §7.X`, НЕ automated CI test (бо потребує 2 пристрої).

## 2. Per-wave design summary

### Wave 1 — `messaging-interactive` (4.1.0)

Методи: `sendReaction`, `sendReceipt`, `sendTyping`, `remoteDelete`.

Shape — всі чотири шерять `[recipient]` OR `[groupId]` discriminated-union pattern (як existing `SendMessageFullParameters`). New options-records:
- `ReactionOptions` (builder pattern, як existing `TextMessageOptions`) — target message identifier (`targetAuthor`+`targetTimestamp`), emoji, remove-flag.
- `ReceiptOptions` — `type: "read"|"viewed"`, target timestamps.
- `TypingOptions` — `stop: bool`.
- `RemoteDeleteOptions` — `targetTimestamp`.

Кожна — extension method на `ISignalMessage`:
```csharp
public interface ISignalMessage
{
    // existing
    Task<SendMessageResponse> SendTextMessageAsync(TextMessageOptions options, CancellationToken ct = default);
    Task<SendMessageResponse> SendAttachmentMessageAsync(AttachmentMessageOptions options, CancellationToken ct = default);
    Task<SendMessageResponse> SendStickerMessageAsync(StickerMessageOptions options, CancellationToken ct = default);

    // NEW
    Task<SendReactionResponse> SendReactionAsync(ReactionOptions options, CancellationToken ct = default);
    Task<SendReceiptResponse> SendReceiptAsync(ReceiptOptions options, CancellationToken ct = default);
    Task<SendTypingResponse> SendTypingAsync(TypingOptions options, CancellationToken ct = default);
    Task<RemoteDeleteResponse> RemoteDeleteAsync(RemoteDeleteOptions options, CancellationToken ct = default);
}
```

Cross-cutting: lands `IdentityChangedException`, `GroupAdminRequiredException`, `CaptchaRequiredException`.

### Wave 2 — `groups-crud` (4.2.0)

Методи: `joinGroup`, `updateGroup`, `quitGroup`.

`updateGroup` — найскладніший: 12+ optional params (name, description, avatar, members add/remove, admins add/remove, message-expiration-time, link-state, permissions). Builder pattern обов'язковий.

```csharp
public interface ISignalGroups
{
    // existing
    Task<ListGroupsResponse> ListGroupsAsync(string account, CancellationToken ct = default);

    // NEW
    Task<JoinGroupResponse> JoinGroupAsync(string account, string uri, CancellationToken ct = default);
    Task<UpdateGroupResponse> UpdateGroupAsync(UpdateGroupOptions options, CancellationToken ct = default);
    Task QuitGroupAsync(string account, string groupId, QuitGroupBehavior behavior = QuitGroupBehavior.LeaveOnly, CancellationToken ct = default);
}

public enum QuitGroupBehavior
{
    LeaveOnly,
    Delete,         // --delete flag — remove local group data
}
```

### Wave 3 — `contacts-identity` (4.3.0)

Методи: `listContacts`, `listIdentities`, `trust`, `updateContact`, `removeContact`, `updateProfile`, `block`, `unblock`.

```csharp
public interface ISignalContacts
{
    Task<ListContactsResponse> ListContactsAsync(string account, ListContactsFilter? filter = null, CancellationToken ct = default);
    Task<ListIdentitiesResponse> ListIdentitiesAsync(string account, string? recipientFilter = null, CancellationToken ct = default);
    Task TrustAsync(TrustOptions options, CancellationToken ct = default);  // --trust-all-known | --verified-safety-number
    Task UpdateContactAsync(UpdateContactOptions options, CancellationToken ct = default);  // name, expiration
    Task RemoveContactAsync(string account, string recipient, RemoveContactBehavior behavior = RemoveContactBehavior.Hide, CancellationToken ct = default);
    Task UpdateProfileAsync(UpdateProfileOptions options, CancellationToken ct = default);  // givenName, familyName, about, mobileCoinAddress, avatar
    Task BlockAsync(string account, IReadOnlyList<string> recipients, IReadOnlyList<string>? groupIds = null, CancellationToken ct = default);
    Task UnblockAsync(string account, IReadOnlyList<string> recipients, IReadOnlyList<string>? groupIds = null, CancellationToken ct = default);
}
```

`TrustAsync` має enum для trust-mode (`TrustMode.TrustAllKnown` | `TrustMode.VerifiedSafetyNumber`) + opt'ний safety-number string. Mutually-exclusive — validate у options-record.

E2E: `listContacts`, `listIdentities` — read-only, safe.

### Wave 4 — `sticker-packs` + `binary-resource-fetch` (4.4.0)

```csharp
public interface ISignalStickers
{
    Task<ListStickerPacksResponse> ListStickerPacksAsync(string account, CancellationToken ct = default);
    Task<UploadStickerPackResponse> UploadStickerPackAsync(string account, string path, CancellationToken ct = default);  // path to manifest dir or .zip
    Task AddStickerPackAsync(string account, string packId, string packKey, CancellationToken ct = default);
}

public interface ISignalResources
{
    Task<byte[]> GetAttachmentAsync(string account, string id, string? recipient = null, string? groupId = null, CancellationToken ct = default);
    Task<byte[]> GetAvatarAsync(string account, string? contact = null, string? groupId = null, string? profile = null, CancellationToken ct = default);
    Task<byte[]> GetStickerAsync(string account, string packId, int stickerId, CancellationToken ct = default);
}
```

signal-cli returns base64-encoded payload у `result.data`. Service декодує через `Convert.FromBase64String` і повертає `byte[]`. Помилковий base64 = `InvalidOperationException`.

### Wave 5 — `device-management` (4.5.0)

```csharp
public interface ISignalDevices
{
    // existing (linking THIS as secondary device to existing primary)
    Task<StartLinkResponse> StartLinkAsync(StartLinkParameters parameters, CancellationToken ct = default);
    Task<FinishLinkResponse> FinishLinkAsync(FinishLinkParameters parameters, CancellationToken ct = default);

    // NEW (managing devices FROM the primary side)
    Task AddDeviceAsync(string account, string uri, CancellationToken ct = default);  // counterpart of startLink — primary side
    Task<ListDevicesResponse> ListDevicesAsync(string account, CancellationToken ct = default);
    Task RemoveDeviceAsync(string account, long deviceId, CancellationToken ct = default);
    Task UpdateDeviceAsync(string account, long deviceId, string deviceName, CancellationToken ct = default);
}
```

`AddDevice` приймає `sgnl://linkdevice?...` URI отриманий від secondary. `ListDevices` — read-only (safe for E2E).

### Wave 6 — `account-lifecycle` (4.6.0) — **opt-in gated**

```csharp
public interface ISignalAccounts
{
    // existing
    Task<ListAccountsResponse> ListAccountsAsync(CancellationToken ct = default);
    Task<SyncAccountsResponse> SendSyncRequestAsync(string account, CancellationToken ct = default);

    // NEW — utility (non-destructive — no gating)
    Task<GetUserStatusResponse> GetUserStatusAsync(GetUserStatusOptions options, CancellationToken ct = default);  // → Wave 8 actually
    Task SendContactsAsync(string account, CancellationToken ct = default);                                       // → Wave 8 actually
    Task SubmitRateLimitChallengeAsync(string account, string challenge, string captcha, CancellationToken ct = default); // → Wave 8

    // NEW — destructive (opt-in via SignalCliOptions.EnableDestructiveOperations)
    Task UpdateAccountAsync(UpdateAccountOptions options, CancellationToken ct = default);  // device name, discoverable, unrestricted-unidentified
    Task UpdateConfigurationAsync(UpdateConfigurationOptions options, CancellationToken ct = default);  // read-receipts, typing-indicators, link-previews, unidentified-delivery
    Task SetPinAsync(string account, string pin, CancellationToken ct = default);
    Task RemovePinAsync(string account, CancellationToken ct = default);
    Task UnregisterAsync(string account, bool deleteAccount = false, CancellationToken ct = default);
    Task DeleteLocalAccountDataAsync(string account, bool ignoreRegistered = false, CancellationToken ct = default);
    Task<StartChangeNumberResponse> StartChangeNumberAsync(StartChangeNumberOptions options, CancellationToken ct = default);
    Task FinishChangeNumberAsync(FinishChangeNumberOptions options, CancellationToken ct = default);
}
```

Constructor:
```csharp
internal sealed class SignalAccounts(
    ISignalCliClient signalCliClient,
    IOptions<SignalCliOptions> options,
    ILogger<SignalAccounts> logger) : ISignalAccounts
{
    private readonly bool _destructiveOpsEnabled = options.Value.EnableDestructiveOperations;
    // ...
}
```

Guard helper:
```csharp
private void EnsureDestructiveAllowed([CallerMemberName] string? method = null)
{
    if (!_destructiveOpsEnabled)
        throw new InvalidOperationException(
            $"Destructive operation '{method}' is disabled. " +
            $"Set SignalCliOptions.EnableDestructiveOperations = true to enable.");
}
```

### Wave 7 — `polls` + `messaging-power-user` (4.7.0)

```csharp
public interface ISignalMessage
{
    // ... existing + Wave 1 ...

    // Polls
    Task<SendPollCreateResponse> SendPollCreateAsync(PollCreateOptions options, CancellationToken ct = default);
    Task<SendPollVoteResponse> SendPollVoteAsync(PollVoteOptions options, CancellationToken ct = default);
    Task<SendPollTerminateResponse> SendPollTerminateAsync(PollTerminateOptions options, CancellationToken ct = default);

    // Power-user
    Task<SendAdminDeleteResponse> SendAdminDeleteAsync(AdminDeleteOptions options, CancellationToken ct = default);
    Task<SendPinMessageResponse> SendPinMessageAsync(PinMessageOptions options, CancellationToken ct = default);
    Task<SendUnpinMessageResponse> SendUnpinMessageAsync(UnpinMessageOptions options, CancellationToken ct = default);
    Task SendMessageRequestResponseAsync(MessageRequestResponseOptions options, CancellationToken ct = default);
    Task<SendPaymentNotificationResponse> SendPaymentNotificationAsync(PaymentNotificationOptions options, CancellationToken ct = default);
}
```

Builder pattern для `PollCreateOptions` (question + 2-10 options + allow-multiple-votes). `MessageRequestResponseOptions` має enum `MessageRequestResponseType { Accept, Delete }` — **тільки 2 values** на send-side per upstream `src/main/java/org/asamk/signal/commands/MessageRequestResponseType.java @ bda4e7fc`. Receive-side sync-message decoding використовує окремий 8-value enum (`MessageEnvelope.Sync.MessageRequestResponse.Type`), але receive-side `messageRequestResponse` event декодер винесено у "Out of scope" (`proposal.md` + `research/SUMMARY.md` §F2). Якщо знадобиться — окремий OpenSpec change з 8-value `MessageRequestResponseSyncType`.

### Wave 8 — `utility-rpc` (4.8.0)

Tail-end ванiльних утиліт:

```csharp
public interface ISignalAccounts
{
    // ... + Wave 6 ...

    Task<GetUserStatusResponse> GetUserStatusAsync(GetUserStatusOptions options, CancellationToken ct = default);
    Task SendContactsAsync(string account, CancellationToken ct = default);
    Task SubmitRateLimitChallengeAsync(string account, string challenge, string captcha, CancellationToken ct = default);
}
```

`GetUserStatusOptions` — list of phone numbers AND/OR usernames (NOT mutually exclusive — upstream `GetUserStatusCommand.java:66-81 @ bda4e7fc` merges both arrays via `Stream.concat` and returns one `JsonUserStatus` row per input, tagged by `recipient`/`username` field). Response is `IReadOnlyList<UserStatusEntry>` where each entry has `Recipient`, `Number?`, `Username?`, `Uuid?`, `IsRegistered: bool` (derived as `Uuid != null`). Safe для E2E (read-only). See `research/wave-8-utility-rpc.md` for full wire shape + `research/SUMMARY.md` §F5.

## 3. Edge cases + open questions

### 3.1 Field-name capitalization

signal-cli's CLI parses kebab-case (`--read-receipts`); its JSON-RPC params use camelCase (`readReceipts`). Перевірити кожен method'у через `JsonRpcCommandHandler.java` (existing reference у `signal-cli-protocol-alignment` design.md). При сумніві — capture реальний request через signal-cli's `--verbose` mode і pin'ити inline у serialization-test'і.

### 3.2 Discriminated unions (recipient vs groupId)

Більшість send-методів приймає АБО `recipient: [...]` АБО `groupId: [...]`, не обидва. Існуючий `SendMessageFullParameters` дозволяє обидва поля nullable; validation на options-record. Зберігаємо той самий pattern для всіх нових send-методів (sendReaction, sendReceipt, etc.).

### 3.3 Base64 binary payload size limit

`getAttachment` може повернути файл до 100 MB. signal-cli повертає його base64-inline у JSON-RPC response (немає альтернативного binary streaming protocol). Read-side: existing `JsonRpcReader.ReadLineAsync` уже працює з великими лініями (Jackson `maxStringLength = 20_000_000` symmetric на read). Якщо файл >15 MB raw (>20M base64), signal-cli кине `IoError -3` ще до response — ми catch'немо як `JsonRpcException`. **Не потрібна спеціальна обробка** — existing constraint вже handle'ить.

### 3.4 `quitGroup` + behavior enum

signal-cli CLI має `--delete` flag для quitGroup (remove local group data після leave). JSON-RPC — окремий param `delete: bool`. Wrapper'имо у `QuitGroupBehavior` enum для type-safety:

```csharp
public enum QuitGroupBehavior
{
    LeaveOnly = 0,  // signal-cli default
    Delete = 1,     // --delete
}
```

### 3.5 `addDevice` vs `startLink` — naming clash з consumer POV

`startLink`/`finishLink` — primary perspective: "цей пристрій (вторинний) лінкується до existing primary".
`addDevice` — secondary perspective: "цей primary додає новий вторинний пристрій через його URI".

Семантично різні. XMLDoc на обох МУСИТЬ чітко казати perspective ("Call from secondary device to ..." / "Call from primary to register a secondary ...") щоб consumer не сплутав.

### 3.6 Avatar/sticker upload — `byte[]` vs file path?

signal-cli приймає **file path** (string) для avatar/sticker upload, не raw bytes. Це обмеження upstream'у. Wrapper:
- `UpdateProfileOptions.AvatarPath: string?` — приймає path
- `UploadStickerPackAsync(account, path)` — приймає path до manifest directory або .zip
- Helper extension `WithAvatarBytes(byte[] bytes)` пише у temp file (через existing `ITemporaryFileService`, як у `AttachmentEntry.SaveToTempFile`), повертає path — но це **convenience-on-top**, основний API — file-path.

### 3.7 `updateConfiguration` field names

signal-cli's --read-receipts / --typing-indicators / --link-previews / --unidentified-delivery — booleans tristate (set true / set false / leave unchanged). JSON-RPC — `bool?` nullable; `null` = leave unchanged. Wrapper зберігає той самий tristate via `bool?` properties у `UpdateConfigurationOptions`.

### 3.8 `submitRateLimitChallenge` — coupling з `CaptchaRequiredException`

Recommended flow для consumer'а:
```csharp
try
{
    await accounts.RegisterAsync(...);
}
catch (CaptchaRequiredException ex)
{
    // 1. surface ex.Message (contains challenge ID) до user
    // 2. user solves https://signalcaptchas.org/registration/generate
    // 3. caller invokes:
    await accounts.SubmitRateLimitChallengeAsync(account, challengeId, captchaToken, ct);
    // 4. retry RegisterAsync
}
```

Документується у XMLDoc на `CaptchaRequiredException` + у `SubmitRateLimitChallengeAsync`.

## 4. Migration impact для consumers

Усі зміни — **additive**. Жодного removal, жодного rename. Існуючі consumers (4.0.x) працюють без змін.

Винятки:
- `JsonRpcClient.InvokeMethodAsync` extended switch (Wave 1) — додає 3 нових derived exception types для codes -4 (вже був), -6, та user-error-with-"admin"-substring. Consumer що catch'ить `catch (JsonRpcException)` — продовжує ловити все. Consumer що хоче type-specific handling — opt-in `catch (CaptchaRequiredException)` тощо.
- `SignalCliOptions.EnableDestructiveOperations` — нова property з default `false`. Жодного впливу на existing consumers (поки вони не invoке destructive methods, що неможливо до Wave 6).

## 5. Альтернативи що відкинули

### A1. Один `SignalApi` god-class замість 7 sub-services

**Pro:** простіше для consumer'а — `_signal.SendReaction(...)`, `_signal.ListContacts(...)`, all on one object.
**Con:** ламає 5 років конвенцій `agent-friendly-api`; existing `ISignalAccounts`/`ISignalDevices`/`ISignalGroups`/`ISignalMessage` — splited за концерном. Confused consumers'и можуть інжектити лише потрібне. God-class заставить інжектити все.
**Verdict:** keep sub-services pattern.

### A2. Codegen DTOs з signal-cli's JSON schema

signal-cli має `--receive-mode=manual` що дозволяє capture real RPC payloads, плюс OpenAPI-like schema generation в дискусії upstream'у (на 2026-05 не landed). Поки не landed — manually writing DTOs (як сьогодні).
**Verdict:** revisit коли upstream ship'не schema; зараз — manual.

### A3. Source-gen для service methods (one method per `[RpcMethod("sendReaction")]` attribute)

Привабливо, але:
- Existing 9 methods написані manually; consistency value > codegen savings для 44 нових.
- AOT-compat: source-gen для service'ів додав би 4-th source-gen в repo (existing: logging, JSON, options-validation). Mental overhead.
- Debug-experience: stepping into generated method — гірше за stepping into hand-written.
**Verdict:** manual write — мінімум 50 LOC × 44 method = ~2200 LOC, але кожна одиниця human-grok'абельна.

### A4. ~~Reactive-replay для new event-types як окремий follow-up~~

**Skipped (initially planned as out-of-scope, then pulled INTO scope after review).** Original concern was that wire-shape capture would require live E2E sessions; investigation проти signal-cli `master` source (2026-05-25 fetch) showed all 7 new receive-side records (`JsonPollCreate`/`JsonPollVote`/`JsonPollTerminate`/`JsonPayment`/`JsonPinMessage`/`JsonUnpinMessage`/`JsonAdminDelete`) are stable Java records у `org.asamk.signal.json` package, identical Jackson-default-mapping (camelCase). Re-engineer'имо DTOs з source — це detereministic, не speculative. Manual live-capture sanity check (single dev, 5 min) before Wave 7 merge confirms drift-free; documented у `tasks.md §7.X.5`. See §1.9 для повного дизайну.

## 6. Validation checklist

Перед merge кожного wave-PR:
- [ ] `dotnet build -p:TreatWarningsAsErrors=true` clean (both src + tests).
- [ ] `dotnet test SignalCli.sln` — usual count + wave-specific delta (per §1.8 матриці).
- [ ] `R01` (`JsonContextRegistrationTests`) — every new `*Parameters`/`*Response` зареєстрований.
- [ ] `R02` (`EventIdBlockTests`) — нові `[LoggerMessage]` методи в правильних EventId блоках.
- [ ] `R03` (`PublicApiSurfaceTests`) — `SignalCli.public-api.txt` updated.
- [ ] `RG08` (`ClaudeMdSplitConsistencyTests`) — root CLAUDE.md ≤ 200 lines stays.
- [ ] CHANGELOG entry у consumer-first voice (`.claude/rules/openspec-workflow.md`).
- [ ] `<SignalCliPackageVersion>` bumped у `Directory.Build.props` у тому ж commit'і що CHANGELOG.
- [ ] `npx -y @fission-ai/openspec@latest validate signal-cli-api-coverage --strict` — green.
