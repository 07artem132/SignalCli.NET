# SignalCli.NET
![Lines](.github/badges/lines.svg) ![Methods](.github/badges/methods.svg) ![Branches](.github/badges/branches.svg)

[![License](https://img.shields.io/badge/license-GPLv3-blue.svg)](http://www.gnu.org/licenses/gpl-3.0.html)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Java](https://img.shields.io/badge/JDK-25+-007396)](https://www.oracle.com/java/technologies/javase-downloads.html)
[![Build Status](https://github.com/07artem132/SignalCli.NET/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/07artem132/SignalCli.NET/actions/workflows/dotnet-desktop.yml)

**Обгортка для signal-cli на базі .NET з підтримкою реактивного програмування**

## 📖 Зміст

- [Про проект](#-про-проект)
- [Особливості](#-особливості)
- [Вимоги](#-вимоги)
- [Встановлення](#-встановлення)
- [Швидкий старт](#-швидкий-старт)
- [Функціональність API](#%EF%B8%8F-функціональність-api)
- [Інтерфейси бібліотеки](#-інтерфейси-бібліотеки)
- [Розширені приклади](#-розширені-приклади)
- [Часті запитання](#-часті-запитання-faq)
- [Залежності](#-залежності)
- [Участь у розробці](#-участь-у-розробці)
- [Ліцензія](#-ліцензія)
- [Подяки](#-подяки)

---

## 📱 Про проект

**SignalCli.NET** — .NET-обгортка для [signal-cli](https://github.com/AsamK/signal-cli), яка забезпечує зручний та зрозумілий API для інтеграції месенджера [Signal](https://signal.org/) у ваші .NET-застосунки.


## 🚀 Особливості
- 💻 **Кросплатформність**: підтримка Windows 🪟, Linux 🐧 та macOS 🍎*
- ⚙️ **Автоматичне управління процесом** `signal-cli`: запуск, моніторинг, перезапуск при збоях
- 📡 **Реактивна обробка подій**: повідомлення, вкладення, реакції, індикатор набору тексту, все це через `System.Reactive` (Rx)
- ✍️ **Форматування повідомлень**: підтримка *курсиву*, **жирного**, `моноширинного` тексту
- 📎 **Підтримка вкладень**: зображення, документи, стікери
- 🧩 **Інтеграція з DI-контейнером**: підтримка `Microsoft.Extensions.DependencyInjection` для гнучкої конфігурації

> \* *Linux і macOS офіційно підтримуються, але ще потребують додаткового тестування.*

## 🔧 Вимоги

- **.NET 10.0 (LTS)** або новіше — [Завантажити](https://dotnet.microsoft.com/download/dotnet/10.0)
- **JDK 25+** — [Завантажити](https://www.oracle.com/java/technologies/javase-downloads.html) — *signal-cli 0.14.3 потребує саме Java 25+; не потрібна в native-режимі (Linux x64) або з bundled-JRE пакетами (Windows/macOS), див. нижче*
- **signal-cli v0.14.3+** — [Завантажити](https://github.com/AsamK/signal-cli/releases)

## 📦 Встановлення
> Пакети публікуються в [GitHub Packages](https://github.com/07artem132/SignalCli.NET/pkgs/nuget) репозиторію.

1. 🔐 Додайте джерело пакета GitHub Packages:
 ```bash
dotnet nuget add source "https://nuget.pkg.github.com/07artem132/index.json" 
   --name github 
   --username USERNAME 
   --password GITHUB_TOKEN 
   --store-password-in-clear-text
   ```
2. 📦 Додайте сам пакет до свого проєкту:

```bash
 dotnet add package SignalCli.NET
 dotnet add package SignalCli.Runtime
```

### 🚫☕ Без Java (native-режим, Linux x64)

signal-cli має офіційний **GraalVM native** збірку — самодостатній бінарник, якому **не потрібна Java**. Доступний лише для **Linux x64** (офіційних native-білдів для Windows/macOS немає).

1. Замість `SignalCli.Runtime` підключіть нативний пакет:
```bash
 dotnet add package SignalCli.NET
 dotnet add package SignalCli.Runtime.Native
```
2. Вкажіть шлях до нативного бінарника (його кладе пакет у вихідну папку):
```csharp
services.AddSignalCli(config =>
{
    config.AppHome = AppContext.BaseDirectory;
    config.StoragePathCli = Path.Combine(AppContext.BaseDirectory, "SignalCliStorageData");
    // Native-режим: Java не запускається взагалі
    config.SignalCliExecutable = Path.Combine(AppContext.BaseDirectory, "signal-cli-native", "signal-cli");
});
```
> Якщо `SignalCliExecutable` задано — бібліотека запускає бінарник напряму. Інакше використовується JVM-режим (`SignalCli.Runtime` + JDK 25+).
> Для **Windows/macOS** офіційного native-білда немає → використовуйте bundled-JRE пакети нижче (або системну Java).

### 🚫☕ Без системної Java (bundled-JRE, Windows/macOS)

Для платформ, де нативного білда немає, є пакети з **вбудованим Eclipse Temurin 25 JRE** —
самодостатні, **системна Java не потрібна**. Це drop-in заміна `SignalCli.Runtime`:

| Платформа | Пакет |
|-----------|-------|
| Windows x64 | `SignalCli.Runtime.Jre.win-x64` |
| macOS arm64 (Apple Silicon) | `SignalCli.Runtime.Jre.osx-arm64` |

```bash
 dotnet add package SignalCli.NET
 dotnet add package SignalCli.Runtime.Jre.win-x64   # або .osx-arm64
```

```csharp
services.AddSignalCli(config =>
{
    config.AppHome = AppContext.BaseDirectory;
    config.LibDirectory = "signal-cli/lib";
    config.StoragePathCli = Path.Combine(AppContext.BaseDirectory, "SignalCliStorageData");
    // config.JavaExecutable НЕ задаємо — він автоматично резолвиться у jre/bin/java[.exe]
});
```

> Пакет кладе у вихідну папку `jre/` (вбудований JRE) та `signal-cli/` (jar-файли).
> `Config.CreateDefault()` спершу шукає вбудований JRE у `jre/bin/java[.exe]` і лише потім — системну Java.
> ⚠️ Пакети великі (~150 МБ): містять і JRE, і signal-cli.

> ⚠️ **Зверніть увагу**  
> Без додавання джерела з GitHub цей пакет не буде доступний.

---
## 🚦 Швидкий старт

### 1. Реєстрація сервісів у DI-контейнері

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SignalCli.Extensions;

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Реєстрація основних сервісів Signal CLI.
        // ✨ 2.1.0: рекомендований overload — типована конфігурація через
        //          SignalCliOptions з DataAnnotations + ValidateOnStart.
        //          Помилки конфігу видно одразу на host.StartAsync(), а не у
        //          ToProcessConfig() пізніше.
        services.AddSignalCli((Action<SignalCliOptions>)(o =>
        {
            o.AppHome = AppContext.BaseDirectory;
            o.LibDirectory = "SignalCli/lib";
            o.StoragePathCli = Path.Combine(AppContext.BaseDirectory, "SignalCliStorageData");
            o.MaxRestartAttempts = 3;
            o.HealthCheckIntervalSeconds = 40;
            o.HealthCheckTimeoutSeconds = 10;
        }));

        // Додавання підтримки подій
        services.AddSignalEvents();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        // ⚠️ Приватність: рівень Trace вмикає логування сирого JSON-RPC трафіку
        // (тіла повідомлень, номери, вкладення). Для звичайної роботи лишайте Information.
        logging.SetMinimumLevel(LogLevel.Information);
        logging.AddConsole();
    })
    .Build();

host.Start();
```

> 💡 **Альтернатива: `appsettings.json`-секція.** Якщо ви бажаєте binding з конфіг-секції, можна
> поєднати `AddOptions<>().Bind(...)` зі стандартним `Configuration`-API ASP.NET:
>
> ```json
> { "SignalCli": { "AppHome": "/app", "LibDirectory": "lib", "JavaExecutable": "java", "MaxRestartAttempts": 5 } }
> ```
> ```csharp
> services.AddOptions<SignalCliOptions>()
>     .Bind(builder.Configuration.GetSection("SignalCli"))
>     .ValidateDataAnnotations()
>     .ValidateOnStart();
> services.AddSignalCli((Action<SignalCliOptions>?)null);  // зареєструє сервіси, не торкаючись Options
> ```

> 🕰 **Legacy overload `AddSignalCli(Action<Config>?)`** лишається, але позначений `[Obsolete]`
> і буде видалений у 3.0 — мігруйте на `Action<SignalCliOptions>`.

### 2. Зв'язування нового пристрою

```csharp
var signalDevices = host.Services.GetRequiredService<ISignalDevices>();

// Починаємо процес зв'язування
var linkResponse = await signalDevices.StartLink();
Console.WriteLine("Для зв'язування відскануйте QR-код у застосунку Signal");
Console.WriteLine($"DeviceLinkUri: {linkResponse.DeviceLinkUri}");

// Тут можна використати бібліотеку для генерації QR-коду
// Наприклад: QRCoder, ZXing.Net тощо

// Після сканування QR-коду завершуємо процес зв'язування
var finishResult = await signalDevices.FinishLink(
    linkResponse.DeviceLinkUri, 
    "Мій новий комп'ютер"
);
Console.WriteLine($"Пристрій успішно зв'язано. Номер: {finishResult.number}");

```

### 3. Надсилання повідомлення

```csharp
var signalMessage = serviceProvider.GetRequiredService<ISignalMessage>();

// Надсилання текстового повідомлення (через Builder + UseStyle для форматування)
var options = new TextMessageOptions.Builder(
        account: "+380501234568",
        recipients: new List<IRecipient> { new UserRecipient("+380501234567") },
        message: "Привіт! *Це текст* з **форматуванням**.")
    .UseStyle()
    .Build();

await signalMessage.SendTextMessageAsync(options);
```

## ⚙️ API-можливості, доступні через обгортку

### 📱 Акаунт

| Функція | Статус | Опис                                   |
|---------|:------:|----------------------------------------|
| ListAccounts | ✅ | Отримання списку акаунтів              |
| SyncAccount | ✅ | Синхронізація акаунта (групи,контакти) |
| register | ❌ | Реєстрація                             |
| verify | ❌ | Підтвердження                          |
| unregister | ❌ | Вимкнення реєстрації                   |
| deleteLocalAccountData | ❌ | Видалення локальних даних              |
| updateAccount | ❌ | Оновлення акаунта                      |
| startChangeNumber | ❌ | Початок зміни номера                   |
| finishChangeNumber | ❌ | Завершення зміни номера                |
| setPin | ❌ | Встановлення PIN-коду                  |
| removePin | ❌ | Видалення PIN-коду                     |

### 📲 Пристрої

| Функція | Статус | Опис |
|---------|:------:|----------|
| StartLink | ✅ | Початок прив'язки пристрою |
| FinishLink | ✅ | Завершення прив'язки пристрою |
| listDevices | ❌ | Список пристроїв |
| addDevice | ❌ | Додавання пристрою |
| removeDevice | ❌ | Видалення пристрою |

### 💬 Повідомлення

| Функція | Статус | Опис |
|---------|:------:|----------|
| SendTextMessageAsync | ✅ | Надсилання текстового повідомлення |
| SendAttachmentAsync | ✅ | Надсилання повідомлення з вкладенням |
| SendStickerAsync | ✅ | Надсилання стікера |
| sendMessageRequestResponse | ❌ | Відповідь на запит |
| sendPaymentNotification | ❌ | Платіжне повідомлення |
| sendReaction | ❌ | Реакція |
| sendReceipt | ❌ | Квитанція про прочитання/перегляд |
| sendTyping | ❌ | Набір тексту |
| remoteDelete | ❌ | Видалення |
| receive | ❌ | Отримання |

### 👥 Групи

| Функція | Статус | Опис |
|---------|:------:|----------|
| ListGroups | ✅ | Отримання списку груп |
| joinGroup | ❌ | Приєднання |
| updateGroup | ❌ | Оновлення/створення |
| quitGroup | ❌ | Вихід |

### 📡 Події

| Функція | Статус | Опис |
|---------|:------:|----------|
| SubscribeAsync | ✅ | Підписка на події |
| UnsubscribeAsync | ✅ | Відписка від подій |

**Підтримувані типи подій:**
- ✅ Текстові повідомлення
- ✅ Реакції
- ✅ Вкладення
- ✅ Стікери
- ✅ Набір тексту
- ✅ Квитанції (звіт про доставку та отримання)
- ✅ Синхронізація

### ⚙️ Системні

| Функція | Статус | Опис |
|---------|:------:|----------|
| Version | ✅ | Отримання версії |
| submitRateLimitChallenge | ❌ | Розв'язання CAPTCHA |

### 📊 Observability (OpenTelemetry)

Бібліотека експонує **дві OTel-сумісні поверхні** — `ActivitySource` і `Meter`, обидві з іменем `"SignalCli.NET"`. Без активного listener'а — нульова накладна.

```csharp
services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("SignalCli.NET"))
    .WithMetrics(m => m.AddMeter("SignalCli.NET"));
```

**Спани:** `rpc.<method>`, `signalcli.process.start`, `signalcli.healthcheck.ping`, `signalcli.subscribe`. **Метрики:** `signalcli.rpc.requests` (counter), `signalcli.rpc.duration` (histogram, ms), `signalcli.process.restarts` (counter), `signalcli.events.dropped` (counter), `signalcli.subscriptions.active` (observable gauge).

**Privacy invariant** (CLAUDE.md rule #1): значення тегів — лише method-names, status-enums, integer-id, durations, exception-type-names. Тіло повідомлення, номер телефону, шлях до файлу — НЕ потрапляють у теги. Enforced unit-тестами `ObservabilityPrivacyTests` через `ActivityListener` + `MeterListener` із seed-PII substring assertions.

**Окремий пакет `SignalCli.NET.HealthChecks`** дає `IHealthCheck`-адаптер для signal-cli process state. Залежить лише від `Microsoft.Extensions.Diagnostics.HealthChecks` (це generic-host пакет, **не ASP.NET**). Працює у будь-якому застосунку з `IHost`:

```csharp
services.AddHealthChecks().AddSignalCliHealthCheck();
```

Detailed examples — у [`docs/cloud-development.md`](docs/cloud-development.md#observability).

### 👤 Профіль

| Функція | Статус | Опис |
|---------|:------:|----------|
| updateProfile | ❌ | Оновлення профілю |

### 📓 Контакти

| Функція | Статус | Опис |
|---------|:------:|----------|
| listContacts | ❌ | Список контактів |
| updateContact | ❌ | Оновлення контакта |
| removeContact | ❌ | Видалення контакта |
| block | ❌ | Блокування |
| unblock | ❌ | Розблокування |
| sendContacts | ❌ | Надсилання списку контактів |
| getUserStatus | ❌ | Отримання статусу користувача |

### 🔒 Безпека

| Функція | Статус | Опис |
|---------|:------:|----------|
| listIdentities | ❌ | Список ключів |
| trust | ❌ | Встановлення довіри |
| updateConfiguration | ❌ | Оновлення конфігурації |

### 🎭 Стікери

| Функція | Статус | Опис |
|---------|:------:|----------|
| uploadStickerPack | ❌ | Завантаження набору стікерів |
| listStickerPacks | ❌ | Список наборів стікерів |
| addStickerPack | ❌ | Додавання набору стікерів |

### 📎 Вкладення

| Функція | Статус | Опис |
|---------|:------:|----------|
| getAttachment | ❌ | Отримання вкладення |
| getAvatar | ❌ | Отримання аватара |
| getSticker | ❌ | Отримання стікера |

## 🧩 Інтерфейси бібліотеки

Взаємодія з бібліотекою відбувається через такі основні інтерфейси:

### IRecipient

Абстракція для представлення отримувачів повідомлень.

```csharp
public interface IRecipient
{
    bool IsGroup { get; }
    string Identifier { get; }
}
```

**Реалізації:**
- `UserRecipient` - для надсилання повідомлень користувачам
- `GroupRecipient` - для надсилання повідомлень у групи

### ISignalAccounts

Сервіс для роботи з обліковими записами Signal.

```csharp
public interface ISignalAccounts
{
    Task<ListAccountsResponse> ListAccounts(CancellationToken cancellationToken = default);
    Task<SyncAccountsResponse> SyncAccount(CancellationToken cancellationToken = default);
}
```

### ISignalDevices

Сервіс для зв'язування та керування пристроями.

```csharp
public interface ISignalDevices
{
    Task<StartLinkResponse> StartLink(CancellationToken cancellationToken = default);
    Task<FinishLinkResponse> FinishLink(string deviceLinkUri, string deviceName, CancellationToken cancellationToken = default);
}
```

### ISignalEventService

Реактивний сервіс для обробки подій Signal.

```csharp
public interface ISignalEventService
{
    Task<SubscribeReceiveResponse> SubscribeAsync(string account, CancellationToken cancellationToken = default);
    Task<UnsubscribeReceiveResponse> UnsubscribeAsync(int subscriptionId, CancellationToken cancellationToken = default);
    
    IObservable<TextMessageEventArgs> TextMessages { get; }
    IObservable<ReactionEventArgs> Reaction { get; }
    IObservable<AttachmentEventArgs> Attachments { get; }
    IObservable<StickerEventArgs> Sticker { get; }
    IObservable<TypingEventArgs> TypingNotifications { get; }
    IObservable<ReceiptEventArgs> Receipts { get; }
    IObservable<SyncEventArgs> Syncs { get; }
}
```

### ISignalGroups

Сервіс для роботи з групами.

```csharp
public interface ISignalGroups
{
    Task<ListGroupsResponse> ListGroups(string account, CancellationToken cancellationToken = default);
}
```

### ISignalMessage

Сервіс для надсилання різних типів повідомлень.

```csharp
public interface ISignalMessage
{
    Task<List<SendMessageResponse>> SendTextMessageAsync(TextMessageOptions options);
    Task<List<SendMessageResponse>> SendAttachmentAsync(AttachmentMessageOptions options);
    Task<List<SendMessageResponse>> SendStickerAsync(StickerMessageOptions options);
}
```

## 📝 Розширені приклади

### Налаштування проекту та ініціалізація

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SignalCli.Extensions;
using SignalCli.Interfaces.Signal;
using SignalCli.Models.Signal.Message;

// Налаштування DI-контейнера з використанням хоста
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Базове налаштування SignalCli
        services.AddSignalCli(config =>
        {
            config.AppHome = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);
            config.LibDirectory = "SignalCli/lib";
            config.StoragePathCli = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SignalCliStorageData");
            config.MaxRestartAttempts = 3;
            config.HealthCheckIntervalSeconds = 40;
            config.HealthCheckTimeoutSeconds = 10;
        });
        
        // Реєстрація сервісу подій
        services.AddSignalEvents();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        // ⚠️ Приватність: рівень Trace вмикає логування сирого JSON-RPC трафіку
        // (тіла повідомлень, номери, вкладення). Для звичайної роботи лишайте Information.
        logging.SetMinimumLevel(LogLevel.Information);
        logging.AddConsole();
    })
    .Build();

host.Start();
```

### Отримання списку акаунтів та груп

```csharp
// Отримання необхідних сервісів
var signalAccounts = host.Services.GetRequiredService<ISignalAccounts>();
var signalGroups = host.Services.GetRequiredService<ISignalGroups>();

// Отримання списку акаунтів та робота з групами
await signalAccounts.ListAccounts().ContinueWith(async accountsTask =>
{
    var accounts = accountsTask.Result;
    if (accounts.Count == 0)
    {
        Console.WriteLine("Немає зареєстрованих акаунтів");
        return;
    }

    // Отримуємо активний акаунт
    var activeAccount = accounts[0].Number;
    Console.WriteLine($"Активний акаунт: {activeAccount}");
    
    // Отримуємо список груп для акаунта
    var groups = await signalGroups.ListGroups(activeAccount);
    Console.WriteLine($"Всього груп: {groups.Count}");
    
    // Пошук конкретної групи за назвою
    var targetGroup = groups
        .Where(group => group.IsMember)
        .FirstOrDefault(group => group.Name.Contains("test group"));
        
    if (targetGroup != null)
    {
        Console.WriteLine($"Знайдено групу: {targetGroup.Name}");
        Console.WriteLine($"Ідентифікатор групи: {targetGroup.Id}");
        Console.WriteLine($"Кількість учасників: {targetGroup.Members.Count}");
    }
});
```

### Зв'язування нового пристрою

```csharp
var signalDevices = host.Services.GetRequiredService<ISignalDevices>();

// Починаємо процес зв'язування
var linkResponse = await signalDevices.StartLink();
Console.WriteLine("Для зв'язування відскануйте QR-код у застосунку Signal");
Console.WriteLine($"DeviceLinkUri: {linkResponse.DeviceLinkUri}");

// Тут можна використати бібліотеку для генерації QR-коду
// Наприклад: QRCoder, ZXing.Net тощо

// Після сканування QR-коду завершуємо процес зв'язування
var finishResult = await signalDevices.FinishLink(
    linkResponse.DeviceLinkUri, 
    "Мій новий комп'ютер"
);
Console.WriteLine($"Пристрій успішно зв'язано. Номер: {finishResult.number}");
```

### Підписка на повідомлення та автоматична відповідь

> ✨ **2.1.0:** для нового коду рекомендуємо `IAsyncEnumerable<T>`-варіанти (`TextMessagesAsync`,
> `AttachmentsAsync`, `ReactionAsync`, …) — це стандартний C# `await foreach` з back-pressure
> (drop-oldest, capacity 1024) і коректним завершенням при `Dispose`. Rx-API (`TextMessages`,
> …) лишається для broadcast/fan-out-сценаріїв.
>
> ```csharp
> // Async-stream API: один споживач читає кожен елемент.
> await foreach (var msg in eventService.TextMessagesAsync(stoppingToken))
> {
>     Console.WriteLine($"[{DateTime.Now}] {msg.SourceNumber}: {msg.DataMessage.Message}");
>     await signalMessage.SendTextMessageAsync(
>         new TextMessageOptions.Builder(msg.Account, [new UserRecipient(msg.SourceUuid)], "Got it!").Build(),
>         stoppingToken);
> }
> ```

Класичний Rx-варіант (без змін у 2.x):

```csharp
var eventService = host.Services.GetRequiredService<ISignalEventService>();
var signalMessage = host.Services.GetRequiredService<ISignalMessage>();

// Підписуємося на отримання подій для акаунта
var accountNumber = "+380501234567";
await eventService.SubscribeAsync(accountNumber);

// Підписуємося на текстові повідомлення
var textSubscription = eventService.TextMessages.Subscribe(message =>
{
    Console.WriteLine($"[{DateTime.Now}] Отримано повідомлення від {message.SourceNumber ?? message.SourceUuid}:");
    Console.WriteLine(message.DataMessage.Message);
    
    // Надсилаємо відповідь
    // Примітка: функціональність цитування повідомлень поки не підтримується через Builder API
    var replyOptions = new TextMessageOptions.Builder(
        account: message.Account,
        recipients: new List<IRecipient> { new UserRecipient(message.SourceUuid) },
        message: "Отримав ваше повідомлення!"
    ).Build();
    
    signalMessage.SendTextMessageAsync(replyOptions);
});

// Підписуємося на вкладення.
// AttachmentEventArgs.Attachments — це List<JsonAttachment> з метаданими;
// сирі байти signal-cli НЕ передає інлайн — їх треба окремо забирати з диска
// (signal-cli зберігає файли у конфіг-каталозі). Тут лише читаємо метадані.
var attachmentSubscription = eventService.Attachments.Subscribe(attachment =>
{
    Console.WriteLine($"[{DateTime.Now}] Отримано вкладення:");
    foreach (var att in attachment.Attachments)
    {
        Console.WriteLine($"Тип: {att.ContentType}, ім'я: {att.Filename}, " +
                          $"id: {att.Id}, розмір: {att.Size} B");
    }

    // Надсилаємо підтвердження
    var confirmOptions = new TextMessageOptions.Builder(
        account: attachment.Account,
        recipients: new List<IRecipient> { new UserRecipient(attachment.SourceUuid) },
        message: $"Отримав ваші вкладення ({attachment.Attachments.Count})"
    ).Build();

    signalMessage.SendTextMessageAsync(confirmOptions);
});

// Підписуємося на події реакцій.
// ReactionEventArgs.Reaction — це сам JsonReaction (не DataMessage.Reaction).
var reactionSubscription = eventService.Reaction.Subscribe(reaction =>
{
    var emoji = reaction.Reaction.Emoji;
    var remove = reaction.Reaction.IsRemove;
    var operation = remove ? "видалив(ла)" : "додав(ла)";

    Console.WriteLine($"[{DateTime.Now}] {reaction.SourceNumber ?? reaction.SourceUuid} {operation} реакцію {emoji}");
});

Console.WriteLine("Обробник повідомлень запущено. Натисніть будь-яку клавішу для виходу...");
Console.ReadKey();

// Відписуємося при завершенні роботи
textSubscription.Dispose();
attachmentSubscription.Dispose();
reactionSubscription.Dispose();
```

### Надсилання різних типів повідомлень

```csharp
var signalMessage = host.Services.GetRequiredService<ISignalMessage>();
var accountNumber = "+380501234567";

// 1. Надсилання простого текстового повідомлення користувачу
var userRecipient = new UserRecipient("+380501234568");
var textOptions = new TextMessageOptions.Builder(
    account: accountNumber,
    recipients: new List<IRecipient> { userRecipient },
    message: "Привіт! Як справи?"
).Build();

await signalMessage.SendTextMessageAsync(textOptions);

// 2. Надсилання форматованого тексту
var formattedTextOptions = new TextMessageOptions.Builder(
    account: accountNumber,
    recipients: new List<IRecipient> { userRecipient },
    message: "Привіт! *Це курсив* і **це жирний текст**, а `це моноширинний`."
)
.UseStyle() // Активація форматування тексту
.Build();

await signalMessage.SendTextMessageAsync(formattedTextOptions);

// 3. Надсилання повідомлення в групу
var groupRecipient = new GroupRecipient("група-ідентифікатор-GUID");
var groupMessageOptions = new TextMessageOptions.Builder(
    account: accountNumber,
    recipients: new List<IRecipient> { groupRecipient },
    message: "Всім привіт у групі!"
).Build();

await signalMessage.SendTextMessageAsync(groupMessageOptions);

// 4. Надсилання повідомлення з вкладенням
var documentOptions = new AttachmentMessageOptions.Builder(
    account: accountNumber,
    recipients: new List<IRecipient> { userRecipient },
    attachments: new List<IAttachmentEntry> { new AttachmentEntry("report.pdf", File.ReadAllBytes(@"C:\Documents\report.pdf")) }
)
.WithMessage("Подивись документ, який я тобі надіслав:")
.Build();

await signalMessage.SendAttachmentAsync(documentOptions);

// 5. Надсилання зображення
var imageOptions = new AttachmentMessageOptions.Builder(
    account: accountNumber,
    recipients: new List<IRecipient> { userRecipient },
    attachments: new List<IAttachmentEntry> { new AttachmentEntry("meeting.jpg", File.ReadAllBytes(@"C:\Photos\meeting.jpg")) }
)
.WithMessage("Ось фото з вчорашньої зустрічі:")
.Build();

await signalMessage.SendAttachmentAsync(imageOptions);

// 6. Надсилання стікера
var stickerOptions = new StickerMessageOptions.Builder(
    account: accountNumber,
    recipients: new List<IRecipient> { userRecipient },
    sticker: "b2e11667c59bce03b6bd13de0377a0b5:32" // ID пакета стікерів : ID стікера
).Build();

await signalMessage.SendStickerAsync(stickerOptions);
```

## 📋 Часті запитання (FAQ)

### Як працює форматування тексту?

Бібліотека підтримує такі стилі форматування тексту:

- *Курсив*: `*текст*`
- **Жирний текст**: `**текст**`
- Моноширинний: `` `текст` ``
- ~~Закреслений~~: `~текст~`
- Спойлер: `||текст||`

Форматування застосовується автоматично під час надсилання повідомлень через `SendTextMessageAsync` або `SendAttachmentAsync`.

### Які розміри вкладень підтримуються?

Signal підтримує вкладення розміром до 100 МБ. Зверніть увагу, що великі файли можуть потребувати додаткового часу для завантаження та надсилання.

### Чи працює бібліотека на Linux та macOS?

Бібліотека розроблена з урахуванням кросплатформності та має працювати на Linux і macOS, однак повне тестування на цих платформах ще не проведено. Якщо ви використовуєте бібліотеку на Linux або macOS, будь ласка, повідомте про результати тестування.

## 🧩 Залежності

| Бібліотека | Опис                                                 |
|------------|------------------------------------------------------|
| Microsoft.Extensions.Hosting.Abstractions | Абстракції для інтеграції з хостингом .NET           |
| Microsoft.Extensions.Logging.Abstractions | Абстракції для логування                             |
| System.Text.Json | Робота з JSON (вбудована в .NET) |
| Nito.AsyncEx | Утиліта для асинхронного програмування               |
| System.Reactive | Бібліотека для реактивного програмування             |

## 🤝 Участь у розробці

Запрошую всіх бажаючих до участі в розвитку проекту! Ось деякі напрямки, де потрібна допомога:

- ✅ Реалізація методів API Signal, яких бракує
- ✅ Тестування на Linux та macOS
- ✅ Покращення документації та прикладів
- ✅ Оптимізація продуктивності

### Як зробити внесок

1. Внесіть необхідні зміни в окремій гілці
2. Надішліть Pull Request з описом змін

## 📜 Ліцензія

Проект поширюється за ліцензією **GNU General Public License v3.0 (GPLv3)** через використання signal-cli та libsignal-service-java.

[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](http://www.gnu.org/licenses/gpl-3.0.html)

## 🙏 Подяки

Проект використовує такі відкриті бібліотеки:

- [signal-cli](https://github.com/AsamK/signal-cli)
- [System.Reactive](https://github.com/dotnet/reactive)
- [System.Text.Json](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/)
- [Nito.AsyncEx](https://github.com/StephenCleary/AsyncEx)

---

> ⚠️ **Зверніть увагу**  
> Бібліотека повністю протестована на **Windows**. Для платформ **Linux** та **macOS** потрібне додаткове тестування. Якщо ви використовуєте ці платформи, будемо вдячні за зворотний зв'язок та допомогу в тестуванні.

---
> **Розроблено з ❤️ для .NET-спільноти та ЗСУ**.
> Якщо виникли запитання чи є ідеї — створюйте Pull Request або Issue!