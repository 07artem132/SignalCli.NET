# `ISignalDevices` — linked devices та linking flow

Linking-flow (як secondary device — `StartLink`/`FinishLink`) і primary-side управління secondary devices (`AddDevice`/`ListDevices`/`RemoveDevice`/`UpdateDevice`).

Резолвиться як `host.Services.GetRequiredService<ISignalDevices>()`.

**Mental model.** signal-cli може діяти або як **primary device** (registers новий акаунт через `register` flow), або як **linked secondary** (приєднується до існуючого через QR-code linking). У бібліотеці `Start/FinishLink` — *цей* signal-cli як secondary; `Add/List/Remove/UpdateDevice` — *цей* signal-cli як primary керує своїми secondaries.

---

## `StartLinkAsync`

```csharp
Task<StartLinkResponse> StartLinkAsync(
    CancellationToken cancellationToken = default);
```

Починає процес лінкування — повертає `DeviceLinkUri` (`sgnl://linkdevice?uuid=...&pub_key=...`). Згенеруй QR з цього URI (QRCoder, ZXing.Net) і скануй у Signal mobile app.

**signal-cli RPC:** `StartLinkCommand.java` @ `bda4e7fc` — wire-поле camelCase `deviceLinkUri` (inner `record JsonLink(String deviceLinkUri)`); `StartLinkResponse.DeviceLinkUri` мапиться через явний `[JsonPropertyName]`.

```csharp
var link = await signalDevices.StartLinkAsync();
Console.WriteLine($"Скануйте QR з URI: {link.DeviceLinkUri}");
// → QRCoder.QRCodeGenerator → відображення для користувача
```

---

## `FinishLinkAsync`

```csharp
Task<FinishLinkResponse> FinishLinkAsync(
    string deviceLinkUri,
    string deviceName,
    CancellationToken cancellationToken = default);
```

Завершує лінкування після того, як primary device відсканував QR. Blocking — чекає response від Signal server'а.

`FinishLinkResponse.Number` — E.164 номер primary акаунту, до якого зв'язалися (PascalCase property — wire-name `"number"`).

**signal-cli RPC:** `FinishLinkCommand.java` @ `bda4e7fc`.

```csharp
var start = await signalDevices.StartLinkAsync();
ShowQrCode(start.DeviceLinkUri);

var result = await signalDevices.FinishLinkAsync(
    deviceLinkUri: start.DeviceLinkUri,
    deviceName: "Worker bot");
Console.WriteLine($"Linked до акаунту: {result.Number}");
```

**Винятки:** `ArgumentNullException` якщо `deviceLinkUri` чи `deviceName` — `null`.

---

## `AddDeviceAsync`

```csharp
Task AddDeviceAsync(
    string account,
    string uri,
    CancellationToken cancellationToken = default);
```

**Primary-перспектива.** Інша secondary device (наприклад, signal-cli на іншій машині) запустила `StartLinkAsync` і показала QR з `sgnl://linkdevice?uuid=...&pub_key=...` URL. Передаємо цей URL у `AddDeviceAsync` і виконуємо provisioning handshake.

**signal-cli RPC:** `AddDeviceCommand.java` @ `bda4e7fc`.

**Blocking:** key-exchange round-trip з secondary через Signal server — секунди.

**Linked-device callers:** якщо цей signal-cli — secondary, throw'ить `-1 UserError`.

```csharp
await signalDevices.AddDeviceAsync(
    account: "+380501234567",
    uri: "sgnl://linkdevice?uuid=abc&pub_key=...");
```

---

## `ListDevicesAsync`

```csharp
Task<ListDevicesResponse> ListDevicesAsync(
    string account,
    CancellationToken cancellationToken = default);
```

Server-side fetch усіх linked devices (НЕ local-cache). `ListDevicesResponse` реалізує `IReadOnlyList<Device>`.

**signal-cli RPC:** `ListDevicesCommand.java` @ `bda4e7fc`.

**§F6 quirk:** `Device` має 4 поля — wire **не** містить `isThisDevice`. Self-identification — за `Id == 1` (primary).

```csharp
var devices = await signalDevices.ListDevicesAsync("+380501234567");
foreach (var d in devices)
{
    var role = d.Id == 1 ? "primary" : "secondary";
    Console.WriteLine($"#{d.Id} [{role}] {d.Name} — created {d.Created}");
}
```

---

## `RemoveDeviceAsync`

```csharp
Task RemoveDeviceAsync(
    string account,
    int deviceId,
    CancellationToken cancellationToken = default);
```

Видаляє linked secondary device. **Destructive** — secondary одразу втрачає capability; немає undo path; secondary мусить re-link через `AddDeviceAsync`.

**signal-cli RPC:** `RemoveDeviceCommand.java` @ `bda4e7fc`.

```csharp
await signalDevices.RemoveDeviceAsync("+380501234567", deviceId: 3);
```

---

## `UpdateDeviceAsync`

```csharp
Task UpdateDeviceAsync(
    string account,
    int deviceId,
    string deviceName,
    CancellationToken cancellationToken = default);
```

Оновлює назву linked device'а. **§F12:** `deviceName` шифрується identity-key'ом перед transmission; .NET сервіс **не** логує `deviceName` вище `Trace` (CLAUDE.md rule #1).

**signal-cli RPC:** `UpdateDeviceCommand.java` @ `bda4e7fc`.

```csharp
await signalDevices.UpdateDeviceAsync(
    account: "+380501234567",
    deviceId: 1,
    deviceName: "Primary phone");
```
