# `ISignalContacts` — контакти та identity-keys

9 .NET-методів (8 upstream signal-cli RPC; `trust` розщеплено на 2 type-safe методи): read-only (`ListContacts`, `ListIdentities`) + mutating (`TrustAllKnownKeys`/`TrustVerified`, `UpdateContact`, `UpdateProfile`, `RemoveContact`, `Block`, `Unblock`).

Mutating методи тригерять contacts-sync на linked devices. Резолвиться як `host.Services.GetRequiredService<ISignalContacts>()`.

---

## `ListContactsAsync`

```csharp
Task<ListContactsResponse> ListContactsAsync(
    string account,
    IEnumerable<string>? recipients = null,
    bool allRecipients = false,
    bool? blocked = null,
    string? name = null,
    bool includeInternal = false,
    CancellationToken cancellationToken = default);
```

Список усіх відомих контактів акаунту з опційними фільтрами. Read-only.

**signal-cli RPC:** `ListContactsCommand.java` @ `bda4e7fc`.

| Параметр | Значення |
|---|---|
| `recipients` | Фільтр — лише ці recipient'и |
| `allRecipients` | `true` → returnн усі known recipients (не лише contacts) |
| `blocked` | `true`/`false` фільтр; `null` → no-filter |
| `name` | Substring-фільтр по contact-name або profile-name |
| `includeInternal` | Додає `internal` sub-object у кожен результат |

```csharp
var contacts = await signalContacts.ListContactsAsync(
    account: "+380501234567",
    blocked: false);
foreach (var c in contacts)
    Console.WriteLine($"{c.Number}: {c.GivenName} {c.FamilyName}");
```

---

## `ListIdentitiesAsync`

```csharp
Task<ListIdentitiesResponse> ListIdentitiesAsync(
    string account,
    string? recipient = null,
    CancellationToken cancellationToken = default);
```

Список identity-keys (safety-numbers). `recipient` — опційний (phone/UUID/PNI/username); `null` → усі. Read-only.

**signal-cli RPC:** `ListIdentitiesCommand.java` @ `bda4e7fc`.

```csharp
var identities = await signalContacts.ListIdentitiesAsync("+380501234567");
foreach (var id in identities)
    Console.WriteLine($"{id.Number}: trust={id.TrustLevel}, safety={id.SafetyNumber}");
```

`TrustLevel` enum: `Untrusted` | `TrustedUnverified` | `TrustedVerified`.

---

## `TrustAllKnownKeysAsync`

```csharp
Task TrustAllKnownKeysAsync(
    string account,
    string recipient,
    CancellationToken cancellationToken = default);
```

Trust усіх known identity-keys recipient'а — **testing-only path** (`--trust-all-known-keys`). Для production — `TrustVerifiedAsync`.

**signal-cli RPC:** `TrustCommand.java` @ `bda4e7fc`. Тригерить verified-message sync на linked devices.

**Винятки:** `JsonRpcException` (`-1`) якщо recipient не зареєстрований або не має identity.

```csharp
// E2E-тест-setup:
await signalContacts.TrustAllKnownKeysAsync("+380501234567", "+380509999999");
```

---

## `TrustVerifiedAsync`

```csharp
Task TrustVerifiedAsync(
    string account,
    string recipient,
    string verifiedSafetyNumber,
    CancellationToken cancellationToken = default);
```

Trust identity-key за verified safety-number (production path). `verifiedSafetyNumber` може бути:
- 60-digit safety-number (з пробілами або без),
- 66-hex-fingerprint,
- base64 scannable-safety-number — upstream disambiguate'ить за довжиною.

**signal-cli RPC:** `TrustCommand.java` @ `bda4e7fc`. **XOR-mutex** з `TrustAllKnownKeysAsync` enforce'иться через окремі методи.

**Винятки:** `JsonRpcException` (`-1`) якщо safety-number не співпадає або має invalid format.

```csharp
await signalContacts.TrustVerifiedAsync(
    account: "+380501234567",
    recipient: "+380509999999",
    verifiedSafetyNumber: "12345 67890 12345 67890 12345 67890 12345 67890 12345 67890 12345 67890");
```

---

## `UpdateContactAsync`

```csharp
Task UpdateContactAsync(
    UpdateContactOptions options,
    CancellationToken cancellationToken = default);
```

Оновлює метадані контакту (імена/note/expiration). **Не** змінює profile-side — для цього `UpdateProfileAsync`.

**signal-cli RPC:** `UpdateContactCommand.java` @ `bda4e7fc`.

```csharp
var opts = new UpdateContactOptions.Builder(account: "+380501234567", recipient: "+380509999999")
    .WithName("Артем — Робота")
    .WithGivenName("Артем")
    .WithFamilyName("Іванов")
    .WithNote("Колега")
    .WithExpiration(86400)   // 24h disappearing messages
    .Build();
await signalContacts.UpdateContactAsync(opts);
```

**Builder методи:** `WithName`, `WithGivenName`/`WithFamilyName`, `WithNickGivenName`/`WithNickFamilyName`, `WithNote`, `WithExpiration(seconds)`.

---

## `UpdateProfileAsync`

```csharp
Task UpdateProfileAsync(
    UpdateProfileOptions options,
    CancellationToken cancellationToken = default);
```

Оновлює **власний** profile: `givenName`, `familyName`, `about`, `aboutEmoji`, `mobileCoinAddress`, avatar.

**signal-cli RPC:** `UpdateProfileCommand.java` @ `bda4e7fc`.

**§F18 mutex:** `AvatarPath` та `RemoveAvatar` — XOR; Builder enforce'ить client-side. **Daemon-side filesystem:** avatar-шлях читається з ФС signal-cli-процесу, не клієнта.

```csharp
var opts = new UpdateProfileOptions.Builder(account: "+380501234567")
    .WithGivenName("Артем")
    .WithAbout("Software engineer")
    .WithAboutEmoji("🚀")
    .WithAvatarPath("/var/lib/signal/avatar.jpg")
    .Build();
await signalContacts.UpdateProfileAsync(opts);

// Або видалити avatar:
var rm = new UpdateProfileOptions.Builder("+380501234567")
    .WithRemoveAvatar()
    .Build();
await signalContacts.UpdateProfileAsync(rm);
```

---

## `RemoveContactAsync`

```csharp
Task RemoveContactAsync(
    string account,
    string recipient,
    RemoveContactMode mode = RemoveContactMode.DeleteContact,
    CancellationToken cancellationToken = default);
```

Видаляє контакт у одному з трьох режимів. **§F9** — `RemoveContactMode` enum гарантує валідний XOR на wire (upstream argparse mutex group НЕ enforce'иться у JSON-RPC шарі).

**signal-cli RPC:** `RemoveContactCommand.java` @ `bda4e7fc`.

`RemoveContactMode`:
- `DeleteContact` — повне видалення (default);
- `Hide` — приховати з contact-list, зберегти session;
- `Forget` — wipe identity + session.

```csharp
await signalContacts.RemoveContactAsync(
    account: "+380501234567",
    recipient: "+380509999999",
    mode: RemoveContactMode.Hide);
```

---

## `BlockAsync`

```csharp
Task BlockAsync(
    string account,
    IEnumerable<string>? recipients = null,
    IEnumerable<string>? groupIds = null,
    CancellationToken cancellationToken = default);
```

Блокує recipient'ів та/або group-id'и.

**signal-cli RPC:** `BlockCommand.java` @ `bda4e7fc`.

**Caveat:** recipient-block і group-block — 2 окремі Manager calls. Якщо перший fails, другий **не виконується** (partial-application). Unknown group-id — лише warn-log, інші group-id'и продовжують застосуватись.

**Linked devices:** throw'ить UserError (`NotPrimaryDeviceException`).

```csharp
await signalContacts.BlockAsync(
    account: "+380501234567",
    recipients: ["+380509999999"],
    groupIds: ["base64GroupId"]);
```

---

## `UnblockAsync`

```csharp
Task UnblockAsync(
    string account,
    IEnumerable<string>? recipients = null,
    IEnumerable<string>? groupIds = null,
    CancellationToken cancellationToken = default);
```

Розблоковує recipient'ів та/або group-id'и. Same caveats як у `BlockAsync` (partial-application, primary-only).

**signal-cli RPC:** `UnblockCommand.java` @ `bda4e7fc`.
