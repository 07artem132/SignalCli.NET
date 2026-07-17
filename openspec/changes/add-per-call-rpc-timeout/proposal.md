# add-per-call-rpc-timeout

## Why

Consumer `SignalCliNet.WsRpcServer` викликає `finishLink` для лінкування пристрою
через ручний QR-скан. Ця фаза interactive: primary device мусить показати/відсканувати
QR і підтвердити зв'язування — на практиці це потребує ≥130 с. Глобальний клієнтський
таймаут `SignalCliOptions.RequestTimeoutSeconds` за замовчуванням 30 с, і це коректне
значення для звичайних RPC-викликів. Підняти його до 130 с для ВСЬОГО клієнта заради
одного інтерактивного виклику — регресія: усі інші виклики втратили б швидкий fail-fast
на «signal-cli живий, але мовчить».

`JsonRpcClient` наразі має ЄДИНИЙ `_requestTimeout` на клієнт (обчислений у конструкторі
з `RequestTimeoutSeconds`) і жодного per-call seam'у. `InvokeMethodAsync` створює
`new CancellationTokenSource(_requestTimeout, _timeProvider)` без можливості override.

## What Changes

- **`add-per-call-rpc-timeout`** — API-additive per-call таймаут:
  - `ISignalCliClient.InvokeMethodAsync` + транспортний `IJsonRpcSender.InvokeMethodAsync`
    отримують ОСТАННІМ опціональний параметр `TimeSpan? timeout = null`. Additive,
    call-site-сумісно (існуючі виклики без аргументу компілюються й поводяться незмінно).
  - `JsonRpcClient.InvokeMethodAsync`: коли `timeout` заданий і `> TimeSpan.Zero` — він
    застосовується до timeout-CTS замість `_requestTimeout` (той самий TimeProvider-aware
    overload `new CancellationTokenSource(effectiveTimeout, _timeProvider)`, правило
    patterns.md — не вводимо parameterless CTS). `null`/`TimeSpan.Zero` → `_requestTimeout`
    (поведінка незмінна). Від'ємний `timeout` → `ArgumentOutOfRangeException` на межі.
  - `TimeoutException`-повідомлення показує фактично застосований (`effectiveTimeout`),
    а не завжди глобальний.
  - `SignalService` (реалізує `ISignalCliClient`) прокидає `timeout` у транспорт.

- **`finish-link-timeout`** — точковий consumer-facing surface:
  - `ISignalDevices.FinishLinkAsync` + `SignalDevices.FinishLinkAsync` отримують ОСТАННІМ
    опц. `TimeSpan? timeout = null`, прокидають у `InvokeMethodAsync`. XMLDoc пояснює,
    навіщо (ручний QR-скан довший за глобальний таймаут). `StartLinkAsync` НЕ змінено —
    у нього немає довгої interactive-фази.

## Impact

- Тести: 509 → 514 (4 нові per-call у `Rpc/PerCallTimeoutTests.cs` через `FakeTimeProvider`;
  +1 forwarding у `DeviceManagement/SignalDevicesCrudTests.cs`).
- Версія: 4.10.1 → 4.10.2 (patch); CHANGELOG `## [4.10.2]` у тому ж наборі змін.
- RG03 (`PublicApiSurfaceTests`): baseline оновлено для 3 змінених сигнатур
  (`IJsonRpcSender`/`ISignalCliClient.InvokeMethodAsync`, `ISignalDevices.FinishLinkAsync`) —
  додано `System.Nullable<System.TimeSpan>` останнім параметром. Additive-only.
- Docs: `docs/api/devices.md` `FinishLinkAsync` — оновлено сигнатуру + per-call-timeout нота.

## Out of scope

- Симетричний per-call timeout на `StartLinkAsync` — у нього немає довгої interactive-фази
  (blocking-фаза — секунди server round-trip'у), тож глобального default'у достатньо.
- Per-call timeout на решті facade-методів (`SendText`, `ListDevices`, …) — API-additive
  seam на `ISignalCliClient`/`IJsonRpcSender` вже дозволяє їх додати згодом за потреби, без
  подальших transport-змін; додаємо лише там, де є доведена потреба (`finishLink`).
- Зміна default'у `RequestTimeoutSeconds` чи його семантики.
