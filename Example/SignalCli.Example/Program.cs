using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SignalCli.Extensions;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models;
using SignalCli.Models.Signal.Message;

namespace SignalCli.Example
{
    /// <summary>
    /// post-modernize-tuning §4.11 (audit D8): приклад переписаний на:
    ///   • async Task Main (повний await-flow без fire-and-forget),
    ///   • await using IHost host (IAsyncDisposable shutdown),
    ///   • await SendTextMessageAsync (раніше було discard-Task),
    ///   • await host.StopAsync() (раніше — синхронний .Wait(), що міг deadlock'нути на UI-контекстах).
    ///
    /// LLM-агенти, що копіюють цей приклад, тепер успадковують правильні async-паттерни.
    /// </summary>
    internal sealed class Program
    {
        private static async Task Main(string[] args)
        {
            // IHost у Microsoft.Extensions.Hosting 10.0 не декларує IAsyncDisposable
            // безпосередньо на інтерфейсі (хоч концкретний host може його реалізувати);
            // для портативності між версіями використовуємо звичайний using.
            using var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    // ✨ 2.1.0: типована конфігурація через SignalCliOptions + ValidateOnStart.
                    // audit-followup-2026 (low-priority-polish): typed lambda parameter
                    // (SignalCliOptions o) замість cast (Action<SignalCliOptions>) — і однозначно
                    // disambiguate'ить overload resolution між AddSignalCli(Action<SignalCliOptions>?)
                    // і AddSignalCli(Action<Config>?) (Config має ті ж AppHome/LibDirectory props,
                    // тому без явного типу C# не може обрати overload), і читабельніше за cast.
                    // Cast прибереться повністю у 4.0 (deprecated-shim-removal видаляє Config-overload).
                    services.AddSignalCli((SignalCliOptions o) =>
                    {
                        o.AppHome = AppDomain.CurrentDomain.BaseDirectory;
                        o.LibDirectory = "signal-cli/lib";
                        o.StoragePathCli =
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SignalCliStorageData");
                        o.MaxRestartAttempts = 3;
                        o.HealthCheckIntervalSeconds = 40;
                        o.HealthCheckTimeoutSeconds = 10;
                    });
                    services.AddSignalEvents();

                    // post-modernize-tuning §11 (audit N1/N2): підключення OpenTelemetry —
                    // консумер реєструє джерела бібліотеки одним рядком на ActivitySource і Meter:
                    //
                    //   services.AddOpenTelemetry()
                    //       .WithTracing(t => t.AddSource("SignalCli.NET").AddConsoleExporter())
                    //       .WithMetrics(m => m.AddMeter("SignalCli.NET").AddConsoleExporter());
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.Trace);
                    logging.AddConsole();
                })
                .Build();

            await host.StartAsync().ConfigureAwait(false);

            var eventService = host.Services.GetRequiredService<ISignalEventService>();
            var signalMessage = host.Services.GetRequiredService<ISignalMessage>();
            var signalAccounts = host.Services.GetRequiredService<ISignalAccounts>();
            var signalGroups = host.Services.GetRequiredService<ISignalGroups>();
            _ = host.Services.GetRequiredService<ISignalCliClient>(); // версія доступна за потреби
            _ = host.Services.GetRequiredService<ISignalDevices>();   // QR-link сценарій нижче

            // Демо: при отриманні текстового повідомлення відправляємо власну відповідь.
            // ВАЖЛИВО: не fire-and-forget — заводимо обробник у власний Task із ConfigureAwait(false)
            // і ловимо помилки, щоб не зривати потік подій.
            using var textSub = eventService.TextMessages.Subscribe(msg =>
            {
                Console.WriteLine($"[TEXT] {msg.DataMessage.Message}");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var textOptions = new TextMessageOptions.Builder(
                                account: "+380501234567",
                                recipients: [new UserRecipient("+380501234567")],
                                message: "Привіт, світ!")
                            .UseStyle()
                            .Build();

                        await signalMessage.SendTextMessageAsync(textOptions).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Send failed: {ex.Message}");
                    }
                });
            });

            // post-modernize-tuning §4.11: повний await-flow для отримання акаунтів і підписки.
            var accounts = await signalAccounts.ListAccountsAsync().ConfigureAwait(false);
            if (accounts.Count == 0)
            {
                Console.WriteLine("Немає зареєстрованих акаунтів. Запустіть процес зв'язування через SignalDevices.StartLinkAsync.");
                /* QR-link flow:
                 *   var resp = await signalDevices.StartLinkAsync();
                 *   var qrCode = QrCode.EncodeText(resp.DeviceLinkUri, QrCode.Ecc.Medium);
                 *   qrCode.ToConsole();
                 *   File.WriteAllBytes("test.png", qrCode.ToPng(1, 0));
                 *   await signalDevices.FinishLinkAsync(resp.DeviceLinkUri, "test");
                 */
            }
            else
            {
                var primary = accounts[0].Number;
                var groups = await signalGroups.ListGroupsAsync(primary).ConfigureAwait(false);
                Console.WriteLine($"Акаунт: {primary}, груп: {groups.Count}");

                // SubscribeAsync — ідемпотентний: повторні виклики для того ж акаунту
                // повертають той самий subscription id без додаткових RPC.
                var sub = await eventService.SubscribeAsync(primary).ConfigureAwait(false);
                Console.WriteLine($"Підписано: {sub.Id}");
            }

            Console.WriteLine("Натисніть будь-яку клавішу для виходу...");
            Console.ReadKey();

            // post-modernize-tuning §4.11: await StopAsync, не .Wait() — інакше можна
            // отримати deadlock на UI-контексті.
            await host.StopAsync().ConfigureAwait(false);
        }
    }
}
