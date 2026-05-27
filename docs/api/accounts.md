# `ISignalAccounts` — облікові записи та lifecycle

Керування зареєстрованими акаунтами, синхронізацією, PIN, change-number, unregister. Read-only методи (`ListAccountsAsync`, `GetUserStatusAsync`) безпечні; **8 destructive методів gated through `SignalCliOptions.EnableDestructiveOperations`** (default `false`) — без opt-in кидають `InvalidOperationException` ще ДО RPC.

Резолвиться як `host.Services.GetRequiredService<ISignalAccounts>()`.

> ⚠ **Destructive operations.** `Unregister`, `DeleteLocalAccountData`, `UpdateAccount`, `SetPin/RemovePin`, `StartChangeNumber/FinishChangeNumber`, `UpdateConfiguration` — НЕ МОЖНА скасувати. Вмикай `EnableDestructiveOperations = true` лише після code-review.

---

## `ListAccountsAsync`

```csharp
Task<ListAccountsResponse> ListAccountsAsync(
    CancellationToken cancellationToken = default);
```

Список зареєстрованих акаунтів. `ListAccountsResponse` реалізує `IReadOnlyList<Account>` — можна enumerate напряму.

**signal-cli RPC:** `ListAccountsCommand.java` @ `bda4e7fc`.

```csharp
var accounts = await signalAccounts.ListAccountsAsync();
foreach (var acc in accounts)
    Console.WriteLine(acc.Number);
```

---

## `SyncAccountAsync`

```csharp
Task<SyncAccountsResponse> SyncAccountAsync(
    CancellationToken cancellationToken = default);
```

**Pull-side sync** — надсилає request на primary, який відповість повним списком контактів/груп. Виконується у фоні.

**signal-cli RPC:** `SyncCommand.java` @ `bda4e7fc`.

---

## `GetUserStatusAsync`

```csharp
Task<GetUserStatusResponse> GetUserStatusAsync(
    string account,
    IEnumerable<string>? recipients = null,
    IEnumerable<string>? usernames = null,
    CancellationToken cancellationToken = default);
```

Перевіряє registration-status для phone numbers AND/OR usernames через CDSI lookup. Read-only.

**signal-cli RPC:** `GetUserStatusCommand.java` @ `bda4e7fc`.

**§F5 AND/OR merge:** обидва `recipients` і `usernames` опційні; обидва можуть бути non-null одночасно (response merges entries). Empty + empty → response з порожнім `Items`.

**Винятки:** `RateLimitException` (`-5`, CDSI throttled).

```csharp
var statuses = await signalAccounts.GetUserStatusAsync(
    account: "+380501234567",
    recipients: ["+380501234567", "+380509999999"]);
foreach (var s in statuses)
    Console.WriteLine($"{s.Recipient}: registered={s.IsRegistered}");
```

---

## `SubmitRateLimitChallengeAsync`

```csharp
Task SubmitRateLimitChallengeAsync(
    string account,
    string challenge,
    string captcha,
    CancellationToken cancellationToken = default);
```

Submit'ить CAPTCHA-solution для rate-limit challenge. Після success — наступні send-операції проходять.

**Workflow:**
1. Send-операція кидає `RateLimitException` → з `Error.Data` витягуєш `challenge` token.
2. Користувач розв'язує CAPTCHA на `https://signalcaptchas.org/challenge/generate.html`.
3. Викликаєш цей метод з token + captcha.

**signal-cli RPC:** `SubmitRateLimitChallengeCommand.java` @ `bda4e7fc`.

**Винятки:** `CaptchaRequiredException` (`-6`) — upstream rejects captcha; `ArgumentException` — якщо `challenge` або `captcha` порожні.

```csharp
try
{
    await signalMessage.SendTextMessageAsync(opts);
}
catch (RateLimitException ex)
{
    var challengeToken = ex.Error.Data?.GetProperty("token").GetString();
    // … користувач розв'язує CAPTCHA, отримуєш captchaSolution …
    await signalAccounts.SubmitRateLimitChallengeAsync(account, challengeToken!, captchaSolution);
}
```

---

## `SendContactsAsync`

```csharp
Task SendContactsAsync(string account, CancellationToken cancellationToken = default);
```

**Push-side** — пушить локальний contact list на linked devices через `SyncMessage.Contacts`. Inverse direction для `SyncAccountAsync` (pull).

**signal-cli RPC:** `SendContactsCommand.java` @ `bda4e7fc`. `JsonRpcException` (`-3 IoError`) при send failure (no linked devices / network).

---

## `UpdateAccountAsync` ⚠ DESTRUCTIVE

```csharp
Task<UpdateAccountResponse> UpdateAccountAsync(
    UpdateAccountOptions options,
    CancellationToken cancellationToken = default);
```

Оновлює server-side attribute'и: deviceName, unidentified-sender policy, discoverability, number-sharing; опційно set/delete username.

**signal-cli RPC:** `UpdateAccountCommand.java` @ `bda4e7fc`. **§F3 NumberSharing** — `bool`, не enum (upstream argparse `type(Boolean.class)`).

**Винятки:** `InvalidOperationException` якщо `EnableDestructiveOperations = false`.

```csharp
var opts = new UpdateAccountOptions.Builder(account: "+380501234567")
    .WithDeviceName("Worker bot")
    .WithDiscoverableByNumber(false)
    .WithUsername("artem.42")
    .Build();
var resp = await signalAccounts.UpdateAccountAsync(opts);
Console.WriteLine($"Username link: {resp.UsernameLink}");
```

---

## `UpdateConfigurationAsync` ⚠ DESTRUCTIVE

```csharp
Task UpdateConfigurationAsync(
    UpdateConfigurationOptions options,
    CancellationToken cancellationToken = default);
```

Оновлює per-account configuration (4 nullable bool flags: `readReceipts`, `unidentifiedDeliveryIndicators`, `typingIndicators`, `linkPreviews`); syncs до linked devices.

**signal-cli RPC:** `UpdateConfigurationCommand.java` @ `bda4e7fc`.

---

## `SetPinAsync` ⚠ DESTRUCTIVE

```csharp
Task SetPinAsync(string account, string pin, CancellationToken cancellationToken = default);
```

Sets Signal registration-lock PIN через Secure Value Recovery. **Client-side enforce: pin ≥ 4 chars** → `ArgumentException`.

**signal-cli RPC:** `SetPinCommand.java` @ `bda4e7fc`.

```csharp
await signalAccounts.SetPinAsync("+380501234567", "12345");
```

---

## `RemovePinAsync` ⚠ DESTRUCTIVE

```csharp
Task RemovePinAsync(string account, CancellationToken cancellationToken = default);
```

Removes registration-lock PIN. Idempotent server-side.

**signal-cli RPC:** `RemovePinCommand.java` @ `bda4e7fc`.

---

## `UnregisterAsync` ⚠ DESTRUCTIVE — IRREVERSIBLE

```csharp
Task UnregisterAsync(
    string account,
    bool deleteAccount = false,
    CancellationToken cancellationToken = default);
```

Unregister акаунт. З `deleteAccount=true` — **irreversibly** видаляє акаунт з Signal серверів.

**signal-cli RPC:** `UnregisterCommand.java` @ `bda4e7fc`.

```csharp
// Soft unregister — local data зберігається:
await signalAccounts.UnregisterAsync("+380501234567");

// Hard delete — IRREVERSIBLE:
await signalAccounts.UnregisterAsync("+380501234567", deleteAccount: true);
```

---

## `DeleteLocalAccountDataAsync` ⚠ DESTRUCTIVE — CANNOT BE UNDONE

```csharp
Task DeleteLocalAccountDataAsync(
    string account,
    bool ignoreRegistered = false,
    CancellationToken cancellationToken = default);
```

Wipe'ає local account directory: identity keys, sessions, contacts. Re-registration створить новий identity — всі контакти побачать "safety-number-changed".

**signal-cli RPC:** `DeleteLocalAccountDataCommand.java` @ `bda4e7fc`.

---

## `StartChangeNumberAsync` ⚠ DESTRUCTIVE

```csharp
Task StartChangeNumberAsync(
    StartChangeNumberOptions options,
    CancellationToken cancellationToken = default);
```

Розпочинає phone-number-change flow (server-side challenge → SMS/voice verification code до нового номера).

**signal-cli RPC:** `StartChangeNumberCommand.java` @ `bda4e7fc`. **§F4 `Voice`** — `bool` (default `false` = SMS).

---

## `FinishChangeNumberAsync` ⚠ DESTRUCTIVE

```csharp
Task FinishChangeNumberAsync(
    FinishChangeNumberOptions options,
    CancellationToken cancellationToken = default);
```

Завершує phone-number change. **OLD number більше не associated** з акаунтом.

**signal-cli RPC:** `FinishChangeNumberCommand.java` @ `bda4e7fc`.

```csharp
// 1. Start:
var start = new StartChangeNumberOptions { Account = "+380501234567", NewNumber = "+380501111111", Voice = false };
await signalAccounts.StartChangeNumberAsync(start);

// 2. Користувач отримує SMS-код на новий номер.

// 3. Finish:
var finish = new FinishChangeNumberOptions
{
    Account = "+380501234567",
    NewNumber = "+380501111111",
    VerificationCode = "123-456",
    Pin = "12345"  // якщо PIN був встановлений
};
await signalAccounts.FinishChangeNumberAsync(finish);
```
