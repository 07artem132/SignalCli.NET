# Бінарні ресурси, стікери, system-level API

Три інтерфейси:
- **`ISignalResources`** (3 методи) — читання бінарних ресурсів з локального кешу signal-cli;
- **`ISignalStickers`** (3 методи) — sticker pack lifecycle;
- **`ISignalCliClient`** (2 методи) — система: `VersionAsync` + raw `InvokeMethodAsync` для розширень.

---

## `ISignalResources`

Усі ресурси читаються з **локального кешу signal-cli** — НЕ тригерять re-download з CDN. Якщо ресурс не у кеші (наприклад, attachment не processed через `receive` попередньо) — `JsonRpcException` (`-1 UserError`) з повідомленням типу `"Could not find attachment with ID:"`.

Резолвиться як `host.Services.GetRequiredService<ISignalResources>()`.

### `GetAttachmentAsync`

```csharp
Task<byte[]> GetAttachmentAsync(
    string account,
    string id,
    CancellationToken cancellationToken = default);
```

Читає attachment за ID (з `DataMessage.Attachments[].Id`). Повертає raw bytes (декодовані з base64 wire-shape).

**signal-cli RPC:** `GetAttachmentCommand.java` @ `bda4e7fc`.

**Винятки:** `JsonRpcException` (`-1`) якщо ID не у кеші; `InvalidOperationException` якщо upstream повернув invalid base64.

```csharp
// З event handler'а:
await foreach (var att in eventService.AttachmentsAsync(stoppingToken))
{
    foreach (var a in att.Attachments)
    {
        byte[] bytes = await signalResources.GetAttachmentAsync(att.Account, a.Id);
        await File.WriteAllBytesAsync($"/downloads/{a.SafeFileName}", bytes);
    }
}
```

### `GetAvatarAsync`

```csharp
Task<byte[]> GetAvatarAsync(
    GetAvatarOptions options,
    CancellationToken cancellationToken = default);
```

Читає avatar — contact, public profile, або group. **§F19 3-way XOR:** `GetAvatarOptions.Builder` enforce'ить вибір рівно одного з `WithContact`/`WithProfile`/`WithGroupId`.

**signal-cli RPC:** `GetAvatarCommand.java` @ `bda4e7fc`.

```csharp
// Contact avatar:
var contactOpts = new GetAvatarOptions.Builder(account: "+380501234567")
    .WithContact("+380509999999")
    .Build();
byte[] avatarBytes = await signalResources.GetAvatarAsync(contactOpts);
// Зазвичай image/jpeg або image/webp.

// Group avatar:
var groupOpts = new GetAvatarOptions.Builder("+380501234567")
    .WithGroupId("base64GroupId")
    .Build();
byte[] groupAvatar = await signalResources.GetAvatarAsync(groupOpts);
```

### `GetStickerAsync`

```csharp
Task<byte[]> GetStickerAsync(
    string account,
    string packId,
    int stickerId,
    CancellationToken cancellationToken = default);
```

Читає sticker з кешу. `packId` — lowercase-hex 32-char `StickerPackId`; `stickerId` — 0-based index у pack'у.

**signal-cli RPC:** `GetStickerCommand.java` @ `bda4e7fc`.

**Винятки:** `ArgumentException` якщо `packId` не valid lowercase-hex 32-char.

```csharp
byte[] webp = await signalResources.GetStickerAsync(
    account: "+380501234567",
    packId: "ab12cd34ef56...",  // 32 hex chars
    stickerId: 0);
```

---

## `ISignalStickers`

Sticker pack lifecycle: upload (network mutating), list (read-only), add/install (network mutating).

> ℹ signal-cli **не** надає `remove`/`uninstall` RPC — для виключення pack'у потрібен ручний edit local-store на upstream-side (out of scope для .NET wrapper'у).

Резолвиться як `host.Services.GetRequiredService<ISignalStickers>()`.

### `UploadStickerPackAsync`

```csharp
Task<UploadStickerPackResponse> UploadStickerPackAsync(
    string account,
    string path,
    CancellationToken cancellationToken = default);
```

Завантажує sticker pack на Signal CDN з `manifest.json` або `.zip` файла на **daemon-side ФС** (signal-cli-процесу, не клієнта).

**signal-cli RPC:** `UploadStickerPackCommand.java` @ `bda4e7fc`.

Після upload'у pack автоматично НЕ install'иться для uploader'а — треба окремо `AddStickerPackAsync` з повернутим URL.

**Винятки:** `JsonRpcException` (`-1 UserError`) — invalid pack (missing manifest, limit exceeded); (`-3 IoError`) — network / image-size failure.

```csharp
var resp = await signalStickers.UploadStickerPackAsync(
    account: "+380501234567",
    path: "/var/lib/signal/my-stickers/manifest.json");
Console.WriteLine($"Shareable URL: {resp.PackUrl}");
// → https://signal.art/addstickers/#pack_id=...&pack_key=...
```

### `ListStickerPacksAsync`

```csharp
Task<ListStickerPacksResponse> ListStickerPacksAsync(
    string account,
    CancellationToken cancellationToken = default);
```

Список усіх відомих sticker pack'ів акаунту (installed + seen via incoming sync). Read-only.

**signal-cli RPC:** `ListStickerPacksCommand.java` @ `bda4e7fc`.

```csharp
var packs = await signalStickers.ListStickerPacksAsync("+380501234567");
foreach (var p in packs)
    Console.WriteLine($"{p.Title} (packId={p.PackId}, installed={p.Installed})");
```

### `AddStickerPackAsync`

```csharp
Task AddStickerPackAsync(
    string account,
    IEnumerable<string> uris,
    CancellationToken cancellationToken = default);
```

Install'ить один або кілька pack'ів за URL'ами.

**signal-cli RPC:** `AddStickerPackCommand.java` @ `bda4e7fc`.

**Caveat:** upstream обробляє URL'и послідовно — failure на будь-якому aborts БЕЗ rollback'у вже-installed pack'ів (partial-application).

**Винятки:** `ArgumentException` якщо `uris` порожній.

```csharp
await signalStickers.AddStickerPackAsync(
    account: "+380501234567",
    uris: ["https://signal.art/addstickers/#pack_id=abc&pack_key=def"]);
```

---

## `ISignalCliClient` — system + raw RPC

Резолвиться як `host.Services.GetRequiredService<ISignalCliClient>()`.

### `VersionAsync`

```csharp
Task<VersionResponse> VersionAsync(
    CancellationToken cancellationToken = default);
```

Ping signal-cli + повертає версію. Дешевий smoke-test — використовується `SignalCliHealthMonitor` для health-probe'у.

**signal-cli RPC:** `VersionCommand.java` @ `bda4e7fc`.

```csharp
var v = await signalCliClient.VersionAsync();
Console.WriteLine($"signal-cli {v.Version} ready");
```

### `InvokeMethodAsync<TRequest, TResponse>` — raw RPC for extension

```csharp
Task<TResponse> InvokeMethodAsync<TRequest, TResponse>(
    string method,
    TRequest parameters,
    JsonTypeInfo<TRequest> requestTypeInfo,
    JsonTypeInfo<TResponse> responseTypeInfo,
    CancellationToken cancellationToken = default
) where TResponse : notnull;
```

Викликає **будь-який** signal-cli JSON-RPC метод, навіть той, що ще не обгорнутий у typed facade. AOT-сумісна signature: викликач **обов'язково** передає source-generated `JsonTypeInfo<T>` для запиту й відповіді (CLAUDE.md rule #15: production code не використовує reflection-based generic overload'и).

**Шаги для додавання нового RPC:**

1. Створи `record TYourParameters(...)` і `record TYourResponse(...)` з `[JsonPropertyName]`.
2. Створи власний `[JsonSerializerContext]` що реєструє обидва типи:

```csharp
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(YourParameters))]
[JsonSerializable(typeof(YourResponse))]
public partial class YourJsonContext : JsonSerializerContext { }
```

3. Виклик:

```csharp
var result = await signalCliClient.InvokeMethodAsync(
    method: "yourSignalCliMethodName",
    parameters: new YourParameters(...),
    requestTypeInfo: YourJsonContext.Default.YourParameters,
    responseTypeInfo: YourJsonContext.Default.YourResponse,
    cancellationToken);
```

Для тестового шаблону — див. `Tests/SignalCli.Tests/TestSerializationContext.cs`.
