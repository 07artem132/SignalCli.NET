using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using SignalCli.Interfaces.Rpc;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.Rpc;
using SignalCli.Models.Signal;
using SignalCli.Models.Signal.Events;
using SignalCli.Services.Signal;

namespace SignalCli.Tests;

public class SignalEventServiceDispatchTests
{
    private const int SubId = 5;
    private const string Account = "+380501234567";

    private static SignalEventService Create(
        out Subject<JsonRpcNotification<SubscriptionEventArgs>> notifications,
        out Mock<ISignalCliClient> signalCli)
    {
        notifications = new Subject<JsonRpcNotification<SubscriptionEventArgs>>();
        var rpcClient = new Mock<IJsonRpcClient>();
        rpcClient.Setup(c => c.Notifications).Returns(notifications);
        var provider = new Mock<IJsonRpcClientProvider>();
        provider.Setup(p => p.Client).Returns(rpcClient.Object);
        signalCli = new Mock<ISignalCliClient>();
        signalCli.Setup(c => c.InvokeMethodAsync<JsonElement, SubscribeReceiveParameters>(
                It.IsAny<string>(), It.IsAny<SubscribeReceiveParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.SerializeToElement(SubId));
        return new SignalEventService(Mock.Of<ILogger<SignalEventService>>(), provider.Object, signalCli.Object);
    }

    private static JsonMessageEnvelope Envelope(
        JsonDataMessage? data = null, JsonTypingMessage? typing = null,
        JsonReceiptMessage? receipt = null, JsonSyncMessage? sync = null)
        => new(Account, Account, "u", "n", 1, 1, 1, 1, data, null, null, sync, null, receipt, typing);

    private static JsonRpcNotification<SubscriptionEventArgs> Notify(JsonMessageEnvelope env)
        => new() { JsonRpc = "2.0", Method = "receive", Params = new SubscriptionEventArgs(SubId, new SignalEventArgs(Account, env)) };

    [Fact]
    public async Task TypingMessage_EmitsTyping()
    {
        var service = Create(out var n, out _);
        await service.StartAsync(CancellationToken.None);
        await service.SubscribeAsync(Account);
        var count = 0;
        service.TypingNotifications.Subscribe(_ => count++);

        n.OnNext(Notify(Envelope(typing: new JsonTypingMessage("STARTED", 1, null))));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ReceiptMessage_EmitsReceipt()
    {
        var service = Create(out var n, out _);
        await service.StartAsync(CancellationToken.None);
        await service.SubscribeAsync(Account);
        var count = 0;
        service.Receipts.Subscribe(_ => count++);

        n.OnNext(Notify(Envelope(receipt: new JsonReceiptMessage(1, true, false, false, [1]))));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SyncMessage_EmitsSync()
    {
        var service = Create(out var n, out _);
        await service.StartAsync(CancellationToken.None);
        await service.SubscribeAsync(Account);
        var count = 0;
        service.Syncs.Subscribe(_ => count++);

        n.OnNext(Notify(Envelope(sync: new JsonSyncMessage(null, null, null, null, null, JsonSyncMessageType.CONTACTS_SYNC))));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Subscribe_Twice_SameAccount_Throws()
    {
        var service = Create(out _, out _);
        await service.StartAsync(CancellationToken.None);
        await service.SubscribeAsync(Account);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubscribeAsync(Account));
    }

    [Fact]
    public async Task Unsubscribe_UnknownId_DoesNotThrow()
    {
        var service = Create(out _, out _);
        await service.StartAsync(CancellationToken.None);

        var result = await service.UnsubscribeAsync(9999);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task EnvelopeWithoutSubscription_IsIgnored()
    {
        var service = Create(out var n, out _);
        await service.StartAsync(CancellationToken.None);
        // не підписуємось
        var count = 0;
        service.TypingNotifications.Subscribe(_ => count++);

        n.OnNext(Notify(Envelope(typing: new JsonTypingMessage("STARTED", 1, null))));

        Assert.Equal(0, count);
    }
}
