using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SignalCli.Extensions;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.Signal.Events;
using SignalCli.Models.Signal.Message;

namespace SignalCli.Example
{
    internal sealed class Program
    {
        static void Main(string[] args)
        {
            using var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    // Реєстрація основних сервісів Signal CLI
                    services.AddSignalCli(config =>
                    {
                        config.AppHome = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);
                        config.LibDirectory = "signal-cli/lib";
                        config.StoragePathCli =
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SignalCliStorageData");
                        config.MaxRestartAttempts = 3;
                        config.HealthCheckIntervalSeconds = 40;
                        config.HealthCheckTimeoutSeconds = 10;
                    });
                    // Додавання підтримки подій
                    services.AddSignalEvents(); 
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.Trace);
                    logging.AddConsole();
                })
                .Build();

            host.Start();

            // Получаем ISignalEventService
            var eventService = host.Services.GetRequiredService<ISignalEventService>();
            // Подписываемся на текстовые сообщения:


            // Достаём ISignalMessage
            var signalMessage = host.Services.GetRequiredService<ISignalMessage>();
            var signalDevices = host.Services.GetRequiredService<ISignalDevices>();
            var signalAccounts = host.Services.GetRequiredService<ISignalAccounts>();
            var signalGroups = host.Services.GetRequiredService<ISignalGroups>();
            var signalCli = host.Services.GetRequiredService<ISignalCliClient>();

            var textSub = eventService.TextMessages.Subscribe(msg =>
            {
                Console.WriteLine(
                    $"[TEXT] From=" + msg.DataMessage.Message
                );
                var textOptions = new TextMessageOptions.Builder("+380501234567",
                        [new UserRecipient("+380501234567")], 
                        "Привіт, світ!")
                    .UseStyle()
                    .Build();
                
                
                var response = signalMessage.SendTextMessageAsync( textOptions);
            });
            signalAccounts.ListAccounts().ContinueWith(async x =>
            {
                var result = x.Result;
                if (result.Count == 0)
                {
                    /*var resp = await signalDevices.StartLink();
                    var qrCode = QrCode.EncodeText(resp.DeviceLinkUri, QrCode.Ecc.Medium);
                    qrCode.ToConsole();
                    File.WriteAllBytes("test.png", qrCode.ToPng(1, 0));
                    await signalDevices.FinishLink(resp.DeviceLinkUri, "test");*/
                    return;
                }

                var groups = await signalGroups.ListGroups(result[0].Number);
                var group = groups.Where(group => group.IsMember)
                    .FirstOrDefault(group => group.Name?.Contains("test cli") == true);
                var group2 = groups.Where(group => group.IsMember)
                    .FirstOrDefault(group => group.Name?.Contains("test cl2") == true);
                Task<SubscribeReceiveResponse> eventId = eventService.SubscribeAsync(result[0].Number);
                
                /* var response = await signalMessage.SendTextMessageAsync(
                 result[0].Number,
                 [
                     new UserRecipient("+380509720960")
                 ], DateTime.Now.ToString(CultureInfo.InvariantCulture));
             response = await signalMessage.SendTextMessageAsync(
                 result[0].Number,
                 [
                     new GroupRecipient(group2.Id),
                 ], DateTime.Now.ToString(CultureInfo.InvariantCulture));
             response = await signalMessage.SendAttachmentAsync(
                 result[0].Number,
                 [
                     new UserRecipient("+380509720960")
                 ], DateTime.Now.ToString(CultureInfo.InvariantCulture),
                 [new AttachmentEntry(@"c:\1.txt")]);
             response = await signalMessage.SendAttachmentAsync(
                 result[0].Number,
                 [
                     new GroupRecipient(group2.Id),
                 ], DateTime.Now.ToString(CultureInfo.InvariantCulture),
                 [new AttachmentEntry(@"c:\1.txt")]);*/
            });


            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();

            // Отписываемся
            textSub.Dispose();
            host.StopAsync().Wait();
        }
    }
}