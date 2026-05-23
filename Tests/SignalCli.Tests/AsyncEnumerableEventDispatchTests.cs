using System.Reactive.Subjects;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Interfaces.Rpc;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.Rpc;
using SignalCli.Models.Signal;
using SignalCli.Models.Signal.Events;
using SignalCli.Services.Signal;

namespace SignalCli.Tests;

/// <summary>
/// E (async-stream events): перевіряє новий <see cref="System.Collections.Generic.IAsyncEnumerable{T}"/>-API
/// поверх <see cref="System.Threading.Channels.Channel{T}"/> у <c>SignalEventService</c>.
/// </summary>
public class AsyncEnumerableEventDispatchTests
{
    private const int SubId = 7;
    private const string Account = "+380501110099";

    private static SignalEventService Create(out Subject<JsonRpcNotification<SubscriptionEventArgs>> notifications)
    {
        notifications = new Subject<JsonRpcNotification<SubscriptionEventArgs>>();
        var rpcClient = new Mock<IJsonRpcClient>();
        rpcClient.Setup(c => c.Notifications).Returns(notifications);
        var provider = new Mock<IJsonRpcClientProvider>();
        provider.Setup(p => p.Client).Returns(rpcClient.Object);
        var signalCli = new Mock<ISignalCliClient>();
        signalCli.Setup(c => c.InvokeMethodAsync<JsonElement, SubscribeReceiveParameters>(
                It.IsAny<string>(), It.IsAny<SubscribeReceiveParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.SerializeToElement(SubId));
        return new SignalEventService(Mock.Of<ILogger<SignalEventService>>(), provider.Object, signalCli.Object);
    }

    private static JsonMessageEnvelope EnvelopeWithText(string text)
    {
        var data = new JsonDataMessage(
            Timestamp: 1, Message: text, ExpiresInSeconds: null, ViewOnce: null,
            Reaction: null, Quote: null, Payment: null, Mentions: null,
            Previews: null, Attachments: null, Sticker: null, RemoteDelete: null,
            Contacts: null, TextStyles: null, GroupInfo: null, StoryContext: null);
        return new JsonMessageEnvelope(Account, Account, "u", "n", 1, 1, 1, 1, data, null, null, null, null, null, null);
    }

    private static JsonRpcNotification<SubscriptionEventArgs> Notify(JsonMessageEnvelope env)
        => new()
        {
            JsonRpc = "2.0",
            Method = "receive",
            Params = new SubscriptionEventArgs(SubId, new SignalEventArgs(Account, env)),
        };

    [Fact]
    public async Task TextMessagesAsync_DeliversInOrder()
    {
        // Arrange
        using var service = Create(out var notifications);
        await service.StartAsync(CancellationToken.None);
        await service.SubscribeAsync(Account);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act: пишемо 3 повідомлення, читаємо 3 з async-stream.
        notifications.OnNext(Notify(EnvelopeWithText("one")));
        notifications.OnNext(Notify(EnvelopeWithText("two")));
        notifications.OnNext(Notify(EnvelopeWithText("three")));

        var received = new List<string>();
        await foreach (var msg in service.TextMessagesAsync(cts.Token))
        {
            received.Add(msg.DataMessage.Message!);
            if (received.Count == 3) break;
        }

        // Assert
        Assert.Equal(new[] { "one", "two", "three" }, received);
    }

    [Fact]
    public async Task ChannelComplete_CompletesAwaitForEachWithoutThrow()
    {
        // Arrange
        var service = Create(out var notifications);
        await service.StartAsync(CancellationToken.None);
        await service.SubscribeAsync(Account);

        notifications.OnNext(Notify(EnvelopeWithText("first")));

        // Act: читаємо в окремій таску, потім диспозимо сервіс — TryComplete завершує цикл.
        var readingTask = Task.Run(async () =>
        {
            var seen = new List<string>();
            await foreach (var msg in service.TextMessagesAsync())
            {
                seen.Add(msg.DataMessage.Message!);
            }
            return seen;
        });

        // Дамо тасці прочитати перший елемент.
        await Task.Delay(50);
        service.Dispose();

        var result = await readingTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert: цикл завершився нормально, без виключень; перший елемент потрапив.
        Assert.Contains("first", result);
    }

    [Fact]
    public async Task BothRxAndChannel_ReceiveSameNotification()
    {
        // Arrange
        using var service = Create(out var notifications);
        await service.StartAsync(CancellationToken.None);
        await service.SubscribeAsync(Account);

        var rxCount = 0;
        using var sub = service.TextMessages.Subscribe(_ => Interlocked.Increment(ref rxCount));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        notifications.OnNext(Notify(EnvelopeWithText("hello")));

        // Act
        TextMessageEventArgs? fromChannel = null;
        await foreach (var msg in service.TextMessagesAsync(cts.Token))
        {
            fromChannel = msg;
            break;
        }

        // Assert: Rx-підписник побачив 1 елемент, channel-споживач — той самий 1 елемент.
        Assert.Equal(1, rxCount);
        Assert.NotNull(fromChannel);
        Assert.Equal("hello", fromChannel!.DataMessage.Message);
    }
}
