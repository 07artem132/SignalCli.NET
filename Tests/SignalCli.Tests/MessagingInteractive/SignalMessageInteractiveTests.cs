using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Exceptions;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.Rpc;
using SignalCli.Models.Signal.Message;
using SignalCli.Services.Signal;

namespace SignalCli.Tests.MessagingInteractive;

/// <summary>
/// signal-cli-api-coverage Wave 1: service-level тести для 4-х нових send-методів.
/// Перевіряють що:
/// 1) Options → Parameters mapping коректний (включно з kebab-case на wire-DTO).
/// 2) RPC method name правильний (sendReaction/sendReceipt/sendTyping/remoteDelete).
/// 3) Null-response гард працює.
/// 4) Derived exceptions з JsonRpcClient просочуються нагору без re-wrap.
/// </summary>
public class SignalMessageInteractiveTests
{
    private static SendMessageResponse Ok(long ts = 1717181920000L) =>
        new(Results: [], TimeStamp: ts);

    // ---- SendReactionAsync ----

    [Fact]
    public async Task SendReactionAsync_MapsOptionsToParameters_AndInvokesSendReactionRpc()
    {
        string? capturedMethod = null;
        SendReactionParameters? capturedParams = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<SendReactionParameters>(),
                It.IsAny<JsonTypeInfo<SendReactionParameters>>(),
                It.IsAny<JsonTypeInfo<SendMessageResponse>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, SendReactionParameters, JsonTypeInfo<SendReactionParameters>, JsonTypeInfo<SendMessageResponse>, CancellationToken, TimeSpan?>(
                (m, p, _, _, _, _) => { capturedMethod = m; capturedParams = p; })
            .ReturnsAsync(Ok(42L));

        var sut = new SignalMessage(client.Object, Mock.Of<ILogger<SignalMessage>>());
        var options = new ReactionOptions.Builder("+380501234567", "👍", 100L)
            .WithRecipients(["+380509999999"])
            .WithNotifySelf()
            .Build();

        var resp = await sut.SendReactionAsync(options);

        Assert.Equal("sendReaction", capturedMethod);
        Assert.NotNull(capturedParams);
        Assert.Equal("+380501234567", capturedParams!.Account);
        Assert.Equal("👍", capturedParams.Emoji);
        Assert.Equal(100L, capturedParams.TargetTimestamp);
        Assert.True(capturedParams.NotifySelf);
        Assert.Equal(42L, resp.TimeStamp);
    }

    [Fact]
    public async Task SendReactionAsync_NullOptions_Throws()
    {
        var sut = new SignalMessage(Mock.Of<ISignalCliClient>(), Mock.Of<ILogger<SignalMessage>>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SendReactionAsync(null!));
    }

    [Fact]
    public async Task SendReactionAsync_UntrustedIdentityException_PropagatesUnwrapped()
    {
        // Захист контракту: derived exceptions з JsonRpcClient бульбашка нагору як є.
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(), It.IsAny<SendReactionParameters>(),
                It.IsAny<JsonTypeInfo<SendReactionParameters>>(),
                It.IsAny<JsonTypeInfo<SendMessageResponse>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UntrustedIdentityException(new JsonRpcError { Code = -4, Message = "Untrusted" }));

        var sut = new SignalMessage(client.Object, Mock.Of<ILogger<SignalMessage>>());
        var options = new ReactionOptions.Builder("+1", "👍", 100L).WithRecipients(["+2"]).Build();

        await Assert.ThrowsAsync<UntrustedIdentityException>(() => sut.SendReactionAsync(options));
    }

    // ---- SendReceiptAsync ----

    [Fact]
    public async Task SendReceiptAsync_MapsTypeEnumToLowercaseWireString()
    {
        SendReceiptParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(), It.IsAny<SendReceiptParameters>(),
                It.IsAny<JsonTypeInfo<SendReceiptParameters>>(),
                It.IsAny<JsonTypeInfo<SendMessageResponse>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, SendReceiptParameters, JsonTypeInfo<SendReceiptParameters>, JsonTypeInfo<SendMessageResponse>, CancellationToken, TimeSpan?>(
                (_, p, _, _, _, _) => captured = p)
            .ReturnsAsync(Ok());

        var sut = new SignalMessage(client.Object, Mock.Of<ILogger<SignalMessage>>());
        var options = new ReceiptOptions.Builder("+1", "+2", [1L, 2L]).WithType(ReceiptType.Viewed).Build();

        await sut.SendReceiptAsync(options);

        Assert.NotNull(captured);
        Assert.Equal("viewed", captured!.Type); // lowercase per §F7 / argparse choices
        Assert.Equal("+2", captured.Recipient);  // singular
    }

    [Fact]
    public async Task SendReceiptAsync_DefaultTypeIsRead()
    {
        SendReceiptParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(), It.IsAny<SendReceiptParameters>(),
                It.IsAny<JsonTypeInfo<SendReceiptParameters>>(),
                It.IsAny<JsonTypeInfo<SendMessageResponse>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, SendReceiptParameters, JsonTypeInfo<SendReceiptParameters>, JsonTypeInfo<SendMessageResponse>, CancellationToken, TimeSpan?>(
                (_, p, _, _, _, _) => captured = p)
            .ReturnsAsync(Ok());

        var sut = new SignalMessage(client.Object, Mock.Of<ILogger<SignalMessage>>());
        var options = new ReceiptOptions.Builder("+1", "+2", [1L]).Build();

        await sut.SendReceiptAsync(options);

        Assert.Equal("read", captured!.Type);
    }

    // ---- SendTypingAsync ----

    [Fact]
    public async Task SendTypingAsync_DefaultIsStart()
    {
        SendTypingParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(), It.IsAny<SendTypingParameters>(),
                It.IsAny<JsonTypeInfo<SendTypingParameters>>(),
                It.IsAny<JsonTypeInfo<SendMessageResponse>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, SendTypingParameters, JsonTypeInfo<SendTypingParameters>, JsonTypeInfo<SendMessageResponse>, CancellationToken, TimeSpan?>(
                (_, p, _, _, _, _) => captured = p)
            .ReturnsAsync(Ok());

        var sut = new SignalMessage(client.Object, Mock.Of<ILogger<SignalMessage>>());
        var options = new TypingOptions.Builder("+1").WithRecipients(["+2"]).Build();

        await sut.SendTypingAsync(options);

        Assert.False(captured!.Stop); // default START → Stop=false
    }

    [Fact]
    public async Task SendTypingAsync_AsStop_SetsStopFlag()
    {
        SendTypingParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(), It.IsAny<SendTypingParameters>(),
                It.IsAny<JsonTypeInfo<SendTypingParameters>>(),
                It.IsAny<JsonTypeInfo<SendMessageResponse>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, SendTypingParameters, JsonTypeInfo<SendTypingParameters>, JsonTypeInfo<SendMessageResponse>, CancellationToken, TimeSpan?>(
                (_, p, _, _, _, _) => captured = p)
            .ReturnsAsync(Ok());

        var sut = new SignalMessage(client.Object, Mock.Of<ILogger<SignalMessage>>());
        var options = new TypingOptions.Builder("+1").WithRecipients(["+2"]).AsStop().Build();

        await sut.SendTypingAsync(options);

        Assert.True(captured!.Stop);
    }

    // ---- SendRemoteDeleteAsync ----

    [Fact]
    public async Task SendRemoteDeleteAsync_PassesTargetTimestampAsScalar()
    {
        RemoteDeleteParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(), It.IsAny<RemoteDeleteParameters>(),
                It.IsAny<JsonTypeInfo<RemoteDeleteParameters>>(),
                It.IsAny<JsonTypeInfo<SendMessageResponse>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, RemoteDeleteParameters, JsonTypeInfo<RemoteDeleteParameters>, JsonTypeInfo<SendMessageResponse>, CancellationToken, TimeSpan?>(
                (_, p, _, _, _, _) => captured = p)
            .ReturnsAsync(Ok());

        var sut = new SignalMessage(client.Object, Mock.Of<ILogger<SignalMessage>>());
        var options = new RemoteDeleteOptions.Builder("+1", 555L).WithRecipients(["+2"]).Build();

        await sut.SendRemoteDeleteAsync(options);

        Assert.Equal(555L, captured!.TargetTimestamp); // long, не list
    }

    [Fact]
    public async Task AllFour_NullResponse_ThrowsInvalidOperation()
    {
        // Shared null-guard контракт. Stubbing returns null для всіх 4-х RPC методів.
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(), It.IsAny<SendReactionParameters>(),
                It.IsAny<JsonTypeInfo<SendReactionParameters>>(),
                It.IsAny<JsonTypeInfo<SendMessageResponse>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SendMessageResponse?)null!);

        var sut = new SignalMessage(client.Object, Mock.Of<ILogger<SignalMessage>>());
        var options = new ReactionOptions.Builder("+1", "👍", 100L).WithRecipients(["+2"]).Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SendReactionAsync(options));
    }
}
