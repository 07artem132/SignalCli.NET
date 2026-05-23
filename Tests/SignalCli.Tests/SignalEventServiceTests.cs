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

public class SignalEventServiceTests
{
    private const int SubId = 7;
    private const string Account = "+380501234567";

    private static SignalEventService CreateService(
        out Subject<JsonRpcNotification<SubscriptionEventArgs>> notifications)
    {
        notifications = new Subject<JsonRpcNotification<SubscriptionEventArgs>>();

        var rpcClient = new Mock<IJsonRpcClient>();
        rpcClient.Setup(c => c.Notifications).Returns(notifications);

        var provider = new Mock<IJsonRpcClientProvider>();
        provider.Setup(p => p.Client).Returns(rpcClient.Object);

        var signalCli = new Mock<ISignalCliClient>();
        // subscribeReceive повертає ідентифікатор підписки
        signalCli
            .Setup(c => c.InvokeMethodAsync<SubscribeReceiveParameters, JsonElement>(
                It.IsAny<string>(), It.IsAny<SubscribeReceiveParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.SerializeToElement(SubId));

        return new SignalEventService(
            Mock.Of<ILogger<SignalEventService>>(), provider.Object, signalCli.Object);
    }

    private static JsonRpcNotification<SubscriptionEventArgs> Notify(JsonDataMessage data)
    {
        var envelope = new JsonMessageEnvelope(
            Source: Account, SourceNumber: Account, SourceUuid: "uuid", SourceName: "name",
            SourceDevice: 1, Timestamp: 1, ServerReceivedTimestamp: 1, ServerDeliveredTimestamp: 1,
            DataMessage: data, EditMessage: null, StoryMessage: null, SyncMessage: null,
            CallMessage: null, ReceiptMessage: null, TypingMessage: null);

        return new JsonRpcNotification<SubscriptionEventArgs>
        {
            JsonRpc = "2.0",
            Method = "receive",
            Params = new SubscriptionEventArgs(SubId, new SignalEventArgs(Account, envelope))
        };
    }

    private static JsonDataMessage DataMessage(
        string? message = null, List<JsonAttachment>? attachments = null, JsonReaction? reaction = null)
        => new(
            Timestamp: 1, Message: message, ExpiresInSeconds: null, ViewOnce: null, Reaction: reaction,
            Quote: null, Payment: null, Mentions: null, Previews: null, Attachments: attachments,
            Sticker: null, RemoteDelete: null, Contacts: null, TextStyles: null, GroupInfo: null,
            StoryContext: null);

    [Fact]
    public async Task CaptionedAttachment_EmitsBothTextAndAttachment()
    {
        var service = CreateService(out var notifications);
        await service.StartAsync(CancellationToken.None);
        await service.SubscribeAsync(Account);

        var textCount = 0;
        var attachmentCount = 0;
        service.TextMessages.Subscribe(_ => textCount++);
        service.Attachments.Subscribe(_ => attachmentCount++);

        var attachment = new JsonAttachment(
            ContentType: "image/png", Filename: "pic.png", Id: "id", Size: 10,
            Width: 1, Height: 1, Caption: "caption", UploadTimestamp: 1);

        notifications.OnNext(Notify(DataMessage(message: "caption", attachments: [attachment])));

        Assert.Equal(1, textCount);
        Assert.Equal(1, attachmentCount);
    }

    [Fact]
    public async Task ReactionWithoutBody_EmitsOnlyReaction()
    {
        var service = CreateService(out var notifications);
        await service.StartAsync(CancellationToken.None);
        await service.SubscribeAsync(Account);

        var textCount = 0;
        var reactionCount = 0;
        service.TextMessages.Subscribe(_ => textCount++);
        service.Reaction.Subscribe(_ => reactionCount++);

        var reaction = new JsonReaction(
            Emoji: "👍", TargetAuthor: "a", TargetAuthorNumber: "n", TargetAuthorUuid: "u",
            TargetSentTimestamp: 1, IsRemove: false);

        notifications.OnNext(Notify(DataMessage(message: null, reaction: reaction)));

        Assert.Equal(0, textCount);
        Assert.Equal(1, reactionCount);
    }

    [Fact]
    public async Task UnknownSubscription_IsIgnored()
    {
        var service = CreateService(out var notifications);
        await service.StartAsync(CancellationToken.None);
        // НЕ підписуємось, тож SubId невідомий

        var textCount = 0;
        service.TextMessages.Subscribe(_ => textCount++);

        notifications.OnNext(Notify(DataMessage(message: "hello")));

        Assert.Equal(0, textCount);
    }
}
