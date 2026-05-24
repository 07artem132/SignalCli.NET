using System.Reactive.Subjects;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models;
using SignalCli.Models.Rpc;
using SignalCli.Models.Signal.Events;
using SignalCli.Models.SignalCli;
using SignalCli.Services.Rpc;

namespace SignalCli.Tests.Rpc;

/// <summary>
/// post-modernize-tuning §1.6 / §7.7 (audit A3): slow-subscriber back-pressure.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="JsonRpcClient"/> має bounded <see cref="System.Threading.Channels.Channel{T}"/>
/// між stdout-парсером і fan-out-споживачем (<c>NotificationChannelCapacity = 1024</c>
/// за замовчуванням). <c>FullMode = Wait</c> означає: повільний підписник створює
/// back-pressure аж до signal-cli — нотифікації НЕ накопичуються в пам'яті безконтрольно.
/// </para>
/// <para>
/// Цей тест перевіряє два інваріанти:
/// <list type="number">
/// <item>Усі N нотифікацій ДОХОДЯТЬ до підписника, навіть якщо він повільний.</item>
/// <item>Порядок збережено — channel single-reader + single-writer гарантує FIFO.</item>
/// </list>
/// </para>
/// <para>
/// Stdout-reader path (приватний) не імітуємо — використовуємо <c>ProcessMessageAsync</c>
/// через reflection, що внутрішньо викликає <c>channel.Writer.WriteAsync</c>. Це той
/// самий path, що й реальний reader.
/// </para>
/// </remarks>
public sealed class BackPressureTests
{
    private const int BurstSize = 100;
    private const int SubscriberDelayMs = 5;

    [Fact]
    public async Task NotificationBurst_WithSlowSubscriber_AllMessagesDeliveredInOrder()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<JsonRpcClient>>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var streamProviderMock = new Mock<IStreamPairProvider>();
        var streamPairSubject = new Subject<StreamPair?>();
        streamProviderMock.Setup(p => p.CurrentStreamPair).Returns((StreamPair?)null);
        streamProviderMock.Setup(p => p.StreamPairChanged).Returns(streamPairSubject);

        var config = new Config
        {
            AppHome = Path.GetTempPath(),
            JavaExecutable = string.Empty,
            LibDirectory = string.Empty,
        };
        var options = config.ToOptions();
        // Малий канал — щоб гарантовано спостерігати back-pressure ефект на BurstSize > capacity.
        // NotificationChannelCapacity — лише на SignalCliOptions, тож мутуємо після .ToOptions().
        options.NotificationChannelCapacity = 8;
        await using var client = new JsonRpcClient(
            loggerMock.Object,
            streamProviderMock.Object,
            options);

        var receivedOrder = new List<int>();
        var allDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Повільний синхронний підписник: затримує OnNext-обробник, чим створює back-pressure.
        using var sub = client.Notifications.Subscribe(n =>
        {
            // Затримка симулює "тяжку" обробку. Sync-Wait блокує consumer-loop.
            Thread.Sleep(SubscriberDelayMs);
            lock (receivedOrder)
            {
                receivedOrder.Add(n.Params.Subscription);
                if (receivedOrder.Count == BurstSize)
                    allDone.TrySetResult(true);
            }
        });

        // Act: 100 нотифікацій burst'ом. Кожна з унікальним subscription-ID для перевірки порядку.
        var processMessage = typeof(JsonRpcClient).GetMethod(
            "ProcessMessageAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        for (int i = 0; i < BurstSize; i++)
        {
            // Wire-shape: notification з полями params.subscription + params.result.account.
            var json = $$"""
                {
                  "jsonrpc": "2.0",
                  "method": "receive",
                  "params": {
                    "subscription": {{i}},
                    "result": { "account": "+1" }
                  }
                }
                """;
            // ProcessMessageAsync — async, повертає Task. WriteAsync всередині блокує
            // при наповненому channel — це back-pressure.
            await (Task)processMessage.Invoke(client, [json, CancellationToken.None])!;
        }

        // Assert: всі N доставлено. Real-time fallback — BurstSize * SubscriberDelayMs * 3.
        await allDone.Task.WaitAsync(TimeSpan.FromMilliseconds(BurstSize * SubscriberDelayMs * 4));

        List<int> snapshot;
        lock (receivedOrder) { snapshot = [.. receivedOrder]; }
        Assert.Equal(BurstSize, snapshot.Count);
        // FIFO: subscription-IDs мають іти 0,1,2,...,99 у тому самому порядку.
        Assert.Equal(Enumerable.Range(0, BurstSize), snapshot);
    }
}
