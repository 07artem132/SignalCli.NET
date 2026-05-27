# Розширений приклад — console-worker з авто-відповіддю

Кінцевий код для console-сервіса який:

1. Стартує signal-cli (bundled JRE);
2. Підписується на первинний акаунт;
3. Слухає текстові повідомлення через `IAsyncEnumerable<T>` з back-pressure;
4. На кожне повідомлення — auto-reply з форматованим текстом.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SignalCli.Exceptions;
using SignalCli.Extensions;
using SignalCli.Interfaces.Signal;
using SignalCli.Models.Signal.Message;

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSignalCliWithBundledRuntimeDefaults(o =>
        {
            o.StoragePathCli = Path.Combine(AppContext.BaseDirectory, "SignalCliStorageData");
            o.MaxRestartAttempts = 3;
        });
        services.AddSignalEvents();
    })
    .Build();

await host.StartAsync();

var eventService = host.Services.GetRequiredService<ISignalEventService>();
var signalMessage = host.Services.GetRequiredService<ISignalMessage>();
var signalAccounts = host.Services.GetRequiredService<ISignalAccounts>();

// Знайти первинний акаунт (якщо порожньо — спершу запусти device-link flow, див. нижче).
var accounts = await signalAccounts.ListAccountsAsync();
if (accounts.Count == 0)
{
    Console.WriteLine("Немає зареєстрованих акаунтів. Запусти QR-link через ISignalDevices.StartLinkAsync.");
    return;
}
var account = accounts[0].Number;

// Підписка ідемпотентна: повторні виклики для того ж акаунту повертають той самий subscription id.
await eventService.SubscribeAsync(account);

// Auto-reply loop. SIGINT / SIGTERM ловиться у Host.StoppingToken — IAsyncEnumerable
// завершиться gracefully коли host shutdown.
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { cts.Cancel(); e.Cancel = true; };

await foreach (var msg in eventService.TextMessagesAsync(cts.Token))
{
    Console.WriteLine($"[{msg.SourceNumber ?? msg.SourceUuid}] {msg.DataMessage.Message}");

    // Відповідаємо тому ж відправнику (UUID — стабільніший за phone-number).
    var reply = new TextMessageOptions.Builder(
            account: msg.Account,
            recipients: [new UserRecipient(msg.SourceUuid)],
            message: "**Отримав!** Дякую за повідомлення.")
        .UseStyle()  // bold/italic/strikethrough/spoiler/monospace через markdown-like syntax
        .Build();

    try
    {
        await signalMessage.SendTextMessageAsync(reply, cts.Token);
    }
    catch (RateLimitException ex)
    {
        Console.Error.WriteLine($"Rate-limit: {ex.Error.Message}; retry-in вказаний в Error.Data");
    }
    catch (UntrustedIdentityException)
    {
        Console.Error.WriteLine($"Безпековий номер змінився; verify-safety-number перш ніж надсилати");
    }
}

await host.StopAsync();
```

## Device-link flow (один раз перед першим запуском)

```csharp
var signalDevices = host.Services.GetRequiredService<ISignalDevices>();
var linkResponse = await signalDevices.StartLinkAsync();
Console.WriteLine($"Скануйте QR з URI: {linkResponse.DeviceLinkUri}");
// Згенеруй QR (QRCoder/ZXing.Net) → сканування у Signal mobile app.
var finishResult = await signalDevices.FinishLinkAsync(linkResponse.DeviceLinkUri, "Мій worker");
Console.WriteLine($"Зв'язано як: {finishResult.Number}");
```

## Що далі

- [`docs/api/events.md`](../api/events.md) — повна таблиця 17 event-kind'ів × 2 поверхні.
- [`docs/api/messaging.md`](../api/messaging.md) — усі 14 send-методів з прикладами.
- [`docs/api/devices.md`](../api/devices.md) — `Start/FinishLink` + management secondary devices.
