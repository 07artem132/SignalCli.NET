# SignalCli.NET
![Lines](https://raw.githubusercontent.com/07artem132/SignalCli.NET/main/.github/badges/lines.svg) ![Methods](https://raw.githubusercontent.com/07artem132/SignalCli.NET/main/.github/badges/methods.svg) ![Branches](https://raw.githubusercontent.com/07artem132/SignalCli.NET/main/.github/badges/branches.svg)

[![License](https://img.shields.io/badge/license-GPLv3-blue.svg)](http://www.gnu.org/licenses/gpl-3.0.html)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Java](https://img.shields.io/badge/JDK-25+-007396)](https://www.oracle.com/java/technologies/javase-downloads.html)
[![Build Status](https://github.com/07artem132/SignalCli.NET/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/07artem132/SignalCli.NET/actions/workflows/dotnet-desktop.yml)

**Інтеграція Signal-месенджера у .NET-застосунки через типізовану обгортку над [signal-cli](https://github.com/AsamK/signal-cli).** Бібліотека сама запускає signal-cli, моніторить його, перезапускає при збоях, і дає вам типізований async-API + Rx/IAsyncEnumerable-стріми подій. Ви пишете бізнес-логіку, не procces-management.

## 📖 Зміст

- [Чому SignalCli.NET](#-чому-signalclinet)
- [Встановлення](#-встановлення)
- [Швидкий старт за 30 рядків](#-швидкий-старт-за-30-рядків)
- [Конфігурація — три шляхи](#%EF%B8%8F-конфігурація--три-шляхи)
- [API-можливості](#%EF%B8%8F-api-можливості)
- [Документація API](#-документація-api)
- [Події — `IObservable<T>` vs `IAsyncEnumerable<T>`](#-події--iobservablet-vs-iasyncenumerablet)
- [Health-checks (опціональний пакет)](#-health-checks-опціональний-пакет)
- [OpenTelemetry observability](#-opentelemetry-observability)
- [FAQ](#-faq)
- [Залежності](#-залежності)
- [Участь у розробці · Ліцензія · Подяки](#-участь-у-розробці)

---

## 🚀 Чому SignalCli.NET

- **Zero process-management code.** Ми самі стартуємо signal-cli, тримаємо stdout/stdin pipe, моніторимо health, рестартуємо при crash'і. Ви отримуєте `ISignalMessage` / `ISignalEventService` у DI і викликаєте методи.
- **Типізований async-API.** `SendTextMessageAsync(opts, ct)`, `ListAccountsAsync()`, `SubscribeAsync(account)` — все `Task`/`ValueTask`, з `CancellationToken`, з типізованими винятками (`RateLimitException`, `UntrustedIdentityException`).
- **Дві поверхні подій — обирай під свій сценарій.** `IObservable<T>` (Rx, fan-out broadcast) **AND** `IAsyncEnumerable<T>` (Channels, `await foreach` з back-pressure). Кожен event-kind має обидві поверхні; парність enforced reflection-тестом.
- **`Microsoft.Extensions.Hosting` first-class.** Бібліотека — це набір `IHostedService`-ів; всі патерни (`IOptions<T>`, `ILogger<T>`, `IHealthCheck`, `TimeProvider`, `ActivitySource`, `Meter`) — нативні.
- **AOT-ready** (`<IsAotCompatible>true</IsAotCompatible>` на core lib). Можна `dotnet publish /p:PublishAot=true` без warning'ів — JSON-серіалізація source-gen-only.
- **Кросплатформність.** Linux 🐧, Windows 🪟, macOS 🍎. На Linux можна без Java (native GraalVM білд signal-cli), на Windows/macOS — bundled-JRE пакет без системної Java.

---

## 📦 Встановлення

> Пакети публікуються в [GitHub Packages](https://github.com/07artem132/SignalCli.NET/pkgs/nuget). Спершу додайте джерело:

```bash
dotnet nuget add source "https://nuget.pkg.github.com/07artem132/index.json" \
    --name github \
    --username USERNAME \
    --password GITHUB_TOKEN \
    --store-password-in-clear-text
```

Тоді обирайте **один** з трьох рантайм-варіантів:

| Сценарій | Команда | Розмір | Java потрібна? |
|---|---|---|---|
| **Bundled JRE** *(рекомендовано для Win/macOS)* | `dotnet add package SignalCli.NET && dotnet add package SignalCli.Runtime.Jre.win-x64` *(або `.osx-arm64`)* | ~150 МБ | ❌ ні |
| **Native binary** *(Linux x64, GraalVM)* | `dotnet add package SignalCli.NET && dotnet add package SignalCli.Runtime.Native` | ~30 МБ | ❌ ні |
| **Системна Java** *(legacy / custom JVM)* | `dotnet add package SignalCli.NET && dotnet add package SignalCli.Runtime` | ~30 МБ | ✅ JDK 25+ |

Опціонально:

```bash
dotnet add package SignalCli.NET.HealthChecks   # IHealthCheck-адаптер; див. секцію нижче
```

---

## 🚦 Швидкий старт за 30 рядків

Робочий приклад. Скопіюй, встав, запусти — отримаєш version-handshake з реальним signal-cli через bundled JRE:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SignalCli.Extensions;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Bundled-runtime defaults — auto-resolve JRE та jar-files.
        // Override через delegate: AppHome, StoragePathCli, timeouts, тощо.
        services.AddSignalCliWithBundledRuntimeDefaults(o =>
        {
            o.StoragePathCli = Path.Combine(AppContext.BaseDirectory, "SignalCliStorageData");
            o.MaxRestartAttempts = 3;
        });
        services.AddSignalEvents();   // потрібен лише якщо плануєш TextMessages/Reaction/...
    })
    .Build();

await host.StartAsync();

// Простий ping signal-cli — підтверджує що процес стартував і JSON-RPC handshake пройшов.
var signalService = host.Services.GetRequiredService<ISignalCliClient>();
var version = await (host.Services.GetRequiredService<ISignalService>()).VersionAsync();
Console.WriteLine($"signal-cli {version.Version} ready");

await host.StopAsync();
```

> **Перший запуск** триває 2-5 секунд — bundled JVM має зінитілізуватись. Наступні виклики через цей же `host` — мілісекунди.

---

## ⚙️ Конфігурація — три шляхи

Усі три overload'и `AddSignalCli` idempotent — повторні виклики тихо no-op'ують замість дублювати hosted-сервіси.

### 1. `AddSignalCliWithBundledRuntimeDefaults` — найпростіший

Для consumer'ів пакетів `SignalCli.Runtime.Jre.*` або `SignalCli.Runtime.Native`. Auto-resolve'ить `AppHome`, `LibDirectory`, `JavaExecutable` (через bundled JRE → `JAVA_HOME` → Windows Oracle → `PATH`). Delegate override опціональний.

```csharp
services.AddSignalCliWithBundledRuntimeDefaults(o =>
{
    o.MaxRestartAttempts = 5;
    o.RequestTimeoutSeconds = 60;
});
```

### 2. `AddSignalCli(Action<SignalCliOptions>)` — повний контроль

```csharp
services.AddSignalCli(o =>
{
    o.AppHome = "/opt/signal";
    o.LibDirectory = "lib";
    o.JavaExecutable = "/usr/bin/java";
    o.MaxRestartAttempts = 3;
    o.HealthCheckIntervalSeconds = 40;
    o.HealthCheckTimeoutSeconds = 10;
    o.RequestTimeoutSeconds = 30;
});
```

Усі властивості валідуються через `[Required]`/`[Range]`-атрибути + cross-field XOR (`JavaExecutable` або `SignalCliExecutable` — один з двох обов'язковий) на `host.StartAsync()`. Помилки конфігу видно одразу, не на першому RPC.

### 3. `AddSignalCli(IConfiguration)` — bind з `appsettings.json`

**AOT-safe** з 4.0.1 (`<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>` робить bind reflection-free).

```json
{
  "SignalCli": {
    "AppHome": "/opt/signal",
    "LibDirectory": "lib",
    "JavaExecutable": "/usr/bin/java",
    "MaxRestartAttempts": 3,
    "HealthCheckIntervalSeconds": 40
  }
}
```

```csharp
services.AddSignalCli(builder.Configuration.GetSection("SignalCli"));
```

---

## ⚙️ API-можливості

З **4.9.0** покриття JSON-RPC методів signal-cli — **100%** (54 з 54). Детально по кожному методу — у [`docs/api/`](docs/).

| Категорія | Підтримано | На черзі |
|-----------|------------|----------|
| **Акаунт** | `ListAccountsAsync`, `SyncAccountAsync`, `GetUserStatusAsync`, `SubmitRateLimitChallengeAsync`, `SendContactsAsync` + 8 destructive (`UpdateAccount`, `UpdateConfiguration`, `SetPin`, `RemovePin`, `Unregister`, `DeleteLocalAccountData`, `StartChangeNumber`, `FinishChangeNumber` — gated через `EnableDestructiveOperations`) | `register`, `verify` (live SMS handshake — поза scope бібліотеки для уже-зареєстрованих акаунтів) |
| **Пристрої** | `StartLinkAsync`, `FinishLinkAsync`, `AddDeviceAsync`, `ListDevicesAsync`, `RemoveDeviceAsync`, `UpdateDeviceAsync` | — |
| **Повідомлення (send-side)** | `SendTextMessageAsync`, `SendAttachmentAsync`, `SendStickerAsync`, `SendReactionAsync`, `SendReceiptAsync`, `SendTypingAsync`, `SendRemoteDeleteAsync`, `SendPollCreate/Vote/TerminateAsync`, `SendAdminDeleteAsync`, `SendPin/UnpinMessageAsync`, `SendMessageRequestResponseAsync`, `SendPaymentNotificationAsync` | — |
| **Групи** | `ListGroupsAsync`, `JoinGroupAsync`, `UpdateGroupAsync` *(dual-mode create/update)*, `QuitGroupAsync` *(idempotent)* | — |
| **Контакти · Identity** | `ListContactsAsync`, `ListIdentitiesAsync`, `TrustAllKnownKeysAsync`, `TrustVerifiedAsync`, `UpdateContactAsync`, `UpdateProfileAsync`, `RemoveContactAsync`, `BlockAsync`, `UnblockAsync` | — |
| **Стікери · Вкладення** | `UploadStickerPackAsync`, `ListStickerPacksAsync`, `AddStickerPackAsync`, `GetAttachmentAsync`, `GetAvatarAsync`, `GetStickerAsync` | — |
| **Події (receive-side)** | `SubscribeAsync`, `UnsubscribeAsync` + 17 потоків × 2 поверхні (`IObservable<T>` + `IAsyncEnumerable<T>`) | — |
| **Системні** | `VersionAsync`, raw `InvokeMethodAsync<TRequest, TResponse>` для розширень | — |

**Підтримувані типи подій (17):** Text, Reaction, Attachment, Sticker, Typing, Receipt, Sync, Quote, Edit, RemoteDelete, PollCreate, PollVote, PollTerminate, Payment, PinMessage, UnpinMessage, AdminDelete.

> ⚠ **Destructive operations** (8 у акаунтах) — за замовчуванням заблоковані. Розблокування через `SignalCliOptions.EnableDestructiveOperations = true` тільки після code-review (`Unregister(deleteAccount: true)` / `DeleteLocalAccountDataAsync` / `FinishChangeNumberAsync` — НЕ МОЖНА скасувати). Деталі — [`docs/api/accounts.md`](docs/api/accounts.md).
>
> Бракує методу або хочеш кастомний RPC? `ISignalCliClient.InvokeMethodAsync<TRequest, TResponse>(method, params, requestInfo, responseInfo)` приймає будь-яку signal-cli команду — AOT-safe, з твоїм власним `[JsonSerializerContext]`. Приклад — [`docs/api/resources-stickers.md § InvokeMethodAsync`](docs/api/resources-stickers.md).

---

## 📚 Документація API

Повна типізована довідка живе у [`docs/`](docs/) — по файлу на категорію, з прикладами і signal-cli source citations:

| Файл | Покриває |
|---|---|
| [`docs/api/messaging.md`](docs/api/messaging.md) | `ISignalMessage` (14 send-методів) |
| [`docs/api/accounts.md`](docs/api/accounts.md) | `ISignalAccounts` (13, з них 8 destructive — opt-in) |
| [`docs/api/devices.md`](docs/api/devices.md) | `ISignalDevices` (linking + secondary management) |
| [`docs/api/groups.md`](docs/api/groups.md) | `ISignalGroups` (CRUD + idempotent quit) |
| [`docs/api/contacts.md`](docs/api/contacts.md) | `ISignalContacts` (trust, profiles, block/unblock) |
| [`docs/api/events.md`](docs/api/events.md) | `ISignalEventService` (17 event-kind'ів × 2 поверхні) |
| [`docs/api/resources-stickers.md`](docs/api/resources-stickers.md) | `ISignalResources` + `ISignalStickers` + raw `InvokeMethodAsync` |
| [`docs/api/di-options.md`](docs/api/di-options.md) | DI extensions + повний reference `SignalCliOptions` |
| [`docs/examples/worker-auto-reply.md`](docs/examples/worker-auto-reply.md) | Console-worker з auto-reply + device-link flow |

**`IRecipient`** реалізації — `UserRecipient(phoneOrUuid)` та `GroupRecipient(groupId)`. Усі `Send*Async` методи повертають **одну** `SendMessageResponse` (з 3.0+ — раніше було `Task<List<...>>` що завжди мав один елемент).

---

## 📡 Події — `IObservable<T>` vs `IAsyncEnumerable<T>`

Кожна подія доступна через **обидві** поверхні. Обирай за сценарієм:

| Сценарій | Поверхня | Контракт |
|---|---|---|
| Broadcast/fan-out (декілька споживачів читають кожне повідомлення) | `IObservable<T>` Rx | Не-blocking, OnNext синхронний; кожен subscriber отримує копію |
| Single-consumer pipeline з back-pressure | `IAsyncEnumerable<T>` Channels | Кожен елемент читає РІВНО ОДИН споживач (exclusive); `await foreach` з типовим `CancellationToken` |
| Drop-oldest семантика при overflow | Обидві (Channel-сторона) | `Channel.CreateBounded<T>(1024, FullMode = DropOldest)`; drop логується на Debug + counter `signalcli.events.dropped` |

```csharp
// Async-stream — рекомендовано для нового коду
await foreach (var msg in eventService.TextMessagesAsync(stoppingToken))
{
    Console.WriteLine($"[{msg.SourceNumber ?? msg.SourceUuid}] {msg.DataMessage.Message}");
}

// Rx — для broadcast у декілька handler'ів
using var sub = eventService.TextMessages.Subscribe(msg =>
{
    Console.WriteLine($"[handler-1] {msg.DataMessage.Message}");
});
```

---

## 🩺 Health-checks (опціональний пакет)

`SignalCli.NET.HealthChecks` — `IHealthCheck`-адаптер для signal-cli process state. Залежить лише від `Microsoft.Extensions.Diagnostics.HealthChecks` (generic-host пакет, **не ASP.NET**). Версія завжди в lockstep з main package (enforced `VersionLockstepTests`).

```bash
dotnet add package SignalCli.NET.HealthChecks
```

### Generic Host (worker / daemon)

```csharp
services.AddSignalCli(o => { /* ... */ });
services.AddHealthChecks().AddSignalCliHealthCheck();

// Periodic probe через HealthCheckService
var hc = host.Services.GetRequiredService<HealthCheckService>();
var report = await hc.CheckHealthAsync();
Console.WriteLine($"signal-cli status: {report.Entries["signal-cli"].Status}");
```

### ASP.NET Core (потрібен окремий пакет `Microsoft.AspNetCore.Diagnostics.HealthChecks`)

```csharp
builder.Services.AddSignalCli(o => { /* ... */ });
builder.Services.AddHealthChecks()
    .AddSignalCliHealthCheck(
        name: "signal-cli",
        failureStatus: HealthStatus.Degraded,   // або Unhealthy — default
        tags: ["signal", "ready"]);

// ... app.Build() ...
app.MapHealthChecks("/healthz");
```

Health-check expose'ить три data-bag поля: `state` (ProcessState enum), `last_ping_ok` (bool), `last_ping_at` (DateTimeOffset). PII-free.

---

## 📊 OpenTelemetry observability

Бібліотека експонує **дві OTel-сумісні поверхні** з іменем `"SignalCli.NET"`. Без активного listener'а — нульова накладна.

```csharp
services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("SignalCli.NET"))
    .WithMetrics(m => m.AddMeter("SignalCli.NET"));
```

**Спани (`ActivitySource`):** `rpc.<method>`, `signalcli.process.start`, `signalcli.healthcheck.ping`, `signalcli.subscribe`.

**Метрики (`Meter`):**

| Інструмент | Тип | Теги |
|---|---|---|
| `signalcli.rpc.requests` | Counter\<long\> | `method`, `status` ∈ {`ok`,`timeout`,`error`} |
| `signalcli.rpc.duration` | Histogram\<double\> (мс) | `method` |
| `signalcli.process.restarts` | Counter\<long\> | `trigger` ∈ {`force`,`crash`,`health`} |
| `signalcli.events.dropped` | Counter\<long\> | `event_type` (один з 10 event-kind'ів) |
| `signalcli.subscriptions.active` | ObservableGauge\<int\> | — |

**Privacy invariant:** значення тегів — лише method-names, status-enums, integer-id, durations, exception-type-names. **Тіло повідомлення, номер телефону, шлях до файлу — НЕ потрапляють у теги.** Enforced unit-тестами `ObservabilityPrivacyTests` через `ActivityListener` + `MeterListener` з seed-PII substring-assertions.

Детальні приклади: [`docs/cloud-development.md`](docs/cloud-development.md#observability).

---

## 📋 FAQ

### Як працює форматування тексту?

Markdown-like syntax всередині `TextMessageOptions.Builder(...).UseStyle().Build()`:

| Стиль | Синтаксис |
|---|---|
| *Курсив* | `*текст*` |
| **Жирний** | `**текст**` |
| `Моноширинний` | `` `текст` `` |
| ~~Закреслений~~ | `~текст~` |
| Спойлер | `\|\|текст\|\|` |

Форматування застосовується автоматично при `SendTextMessageAsync` / `SendAttachmentAsync` (caption).

### Які розміри вкладень підтримуються?

Signal підтримує до **100 МБ**. На low-level бібліотека сама перемикається між inline data-URI (для малих) і temp-file path (для великих ≥ 12 МБ raw — поріг розрахований під Jackson `maxStringLength` 20M + 4M margin). Деталі: `MaxInlineEncodedAttachmentBytes` константа в `SignalMessage.cs`.

### Чи працює на Linux та macOS?

**Linux x64:** так, native-режим (GraalVM-білд signal-cli, без Java) повністю підтримується + CI-тестування на ubuntu-latest. **macOS arm64:** bundled-JRE-пакет ship'ить Eclipse Temurin 25 — працює, але CI-coverage менший. **Linux ARM:** офіційного native-білда немає → потрібна системна JDK 25+.

### AOT-публікація працює?

Так — на main lib увімкнено `<IsAotCompatible>true</IsAotCompatible>` (3.0+). `dotnet publish /p:PublishAot=true` не дає warning'ів **за умови** використання `AddSignalCli(Action<SignalCliOptions>?)` або `AddSignalCli(IConfiguration)` (обидва source-gen-friendly з 4.0.1). JSON-серіалізація — source-gen-only.

### Як додати власний RPC-метод який ще не обгорнутий?

```csharp
var client = host.Services.GetRequiredService<ISignalCliClient>();
var result = await client.InvokeMethodAsync(
    "yourMethodName",
    new YourParameters(...),
    YourJsonContext.Default.YourParameters,    // обов'язково — AOT-safe
    YourJsonContext.Default.YourResponse,
    cancellationToken);
```

Зареєструй DTO у власному `[JsonSerializerContext]` (test-pattern: див. `Tests/SignalCli.Tests/TestSerializationContext.cs`).

---

## 🧩 Залежності

**Core (`SignalCli.NET`):**

| Бібліотека | Призначення |
|---|---|
| `Microsoft.Extensions.Hosting.Abstractions` | `IHostedService` / `IHost` інтеграція |
| `Microsoft.Extensions.Logging.Abstractions` | Source-gen `[LoggerMessage]` |
| `Microsoft.Extensions.Options.DataAnnotations` | `[Required]`/`[Range]` атрибути для `[OptionsValidator]` source-gen |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | `AddSignalCli(IConfiguration)` overload |
| `System.Text.Json` | Source-gen JSON (production code reflection-free) |
| `System.Reactive` | `IObservable<T>` event streams |
| `JetBrains.Annotations` | `PublicAPI`/`NotNull`-hint'и; `PrivateAssets=all` — не потрапляє у consumer dependency graph |

**Опціонально:**

| Пакет | Призначення |
|---|---|
| `SignalCli.NET.HealthChecks` | `IHealthCheck`-адаптер; залежить лише від generic-host `Microsoft.Extensions.Diagnostics.HealthChecks`; **не ASP.NET-coupled** |
| `SignalCli.Runtime` | signal-cli jar (потребує системну Java 25+) |
| `SignalCli.Runtime.Native` | GraalVM-native signal-cli (Linux x64, без Java) |
| `SignalCli.Runtime.Jre.win-x64` | Bundled Temurin JRE + signal-cli (Windows, без системної Java) |
| `SignalCli.Runtime.Jre.osx-arm64` | Те саме для macOS arm64 |

---

## 🤝 Участь у розробці

Запрошуємо до участі. Перспективні напрямки:

- ✅ Реалізація методів API signal-cli, яких бракує (див. таблицю `❌` вище)
- ✅ Тестування на Linux ARM та macOS arm64
- ✅ Покращення документації та прикладів
- ✅ Оптимізація — особливо attachment-pipelines

**Як зробити внесок:**

1. Створіть feature-branch від `main`
2. Внесіть зміни, додайте/оновіть тести (всі 287+ unit + 8+ Integration мають лишитись зеленими)
3. Великі зміни — спершу через OpenSpec change у `openspec/changes/<name>/` (`proposal.md` + `tasks.md` + `specs/<capability>/spec.md`); запустіть `npx -y @fission-ai/openspec@latest validate <name> --strict`
4. Надішліть Pull Request з посиланням на OpenSpec change (якщо є) та коротким описом impact'у

Внутрішня документація для контрибуторів — `CLAUDE.md` у корені (rules, established patterns, audit baseline).

## 📜 Ліцензія

GNU General Public License v3.0 (GPLv3) — через залежність від [signal-cli](https://github.com/AsamK/signal-cli) та libsignal-service-java.

[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](http://www.gnu.org/licenses/gpl-3.0.html)

## 🙏 Подяки

Проект побудований на:

- [signal-cli](https://github.com/AsamK/signal-cli) — Java-CLI що обгортає `libsignal-service-java`
- [System.Reactive](https://github.com/dotnet/reactive) — Rx-стріми
- [System.Text.Json](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/) — source-gen serialization
- [Eclipse Temurin](https://adoptium.net/temurin/) — bundled JRE для Win/macOS пакетів
- [GraalVM](https://www.graalvm.org/) — native signal-cli build для Linux
