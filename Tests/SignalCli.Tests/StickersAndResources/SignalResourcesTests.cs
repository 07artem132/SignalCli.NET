using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.Signal.Resources;
using SignalCli.Services.Signal;

namespace SignalCli.Tests.StickersAndResources;

public class SignalResourcesTests
{
    private const string ValidBase64 = "SGVsbG8="; // "Hello"

    private static Mock<ISignalCliClient> ClientReturningData<TParam>(string base64)
        where TParam : class
    {
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<TParam>(),
                It.IsAny<JsonTypeInfo<TParam>>(),
                It.IsAny<JsonTypeInfo<JsonAttachmentData>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JsonAttachmentData(base64));
        return client;
    }

    // ---- GetAttachmentAsync ----

    [Fact]
    public async Task GetAttachmentAsync_DecodesBase64ToBytes()
    {
        var client = ClientReturningData<GetAttachmentParameters>(ValidBase64);
        var sut = new SignalResources(client.Object, Mock.Of<ILogger<SignalResources>>());

        var bytes = await sut.GetAttachmentAsync("+1", "attachment-id");

        Assert.Equal("Hello", System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task GetAttachmentAsync_InvalidBase64_ThrowsInvalidOperation()
    {
        // §4.6: invalid base64 → InvalidOperationException (НЕ raw FormatException), clear diagnostic.
        var client = ClientReturningData<GetAttachmentParameters>("NOT VALID BASE64 @@@");
        var sut = new SignalResources(client.Object, Mock.Of<ILogger<SignalResources>>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetAttachmentAsync("+1", "id"));
        Assert.Contains("invalid base64", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("getAttachment", ex.Message);
    }

    [Fact]
    public async Task GetAttachmentAsync_EmptyId_Throws()
    {
        var sut = new SignalResources(Mock.Of<ISignalCliClient>(), Mock.Of<ILogger<SignalResources>>());
        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetAttachmentAsync("+1", ""));
    }

    // ---- GetAvatarAsync ----

    [Fact]
    public async Task GetAvatarAsync_Contact_SendsContactField()
    {
        GetAvatarParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<GetAvatarParameters>(),
                It.IsAny<JsonTypeInfo<GetAvatarParameters>>(),
                It.IsAny<JsonTypeInfo<JsonAttachmentData>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, GetAvatarParameters, JsonTypeInfo<GetAvatarParameters>, JsonTypeInfo<JsonAttachmentData>, CancellationToken, TimeSpan?>(
                (_, p, _, _, _, _) => captured = p)
            .ReturnsAsync(new JsonAttachmentData(ValidBase64));

        var sut = new SignalResources(client.Object, Mock.Of<ILogger<SignalResources>>());
        var opts = new GetAvatarOptions.Builder("+1").WithContact("+2").Build();

        var bytes = await sut.GetAvatarAsync(opts);

        Assert.Equal("+2", captured!.Contact);
        Assert.Null(captured.Profile);
        Assert.Null(captured.GroupId);
        Assert.Equal("Hello", System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task GetAvatarAsync_DefenseInDepth_BothFieldsSet_Throws()
    {
        // Defense-in-depth: пряма construction record'у поза Builder'ом дозволила б обидва поля.
        // Service-метод guards.
        var opts = new GetAvatarOptions.Builder("+1").WithContact("+2").Build();
        var profileProp = typeof(GetAvatarOptions).GetProperty("Profile")!;
        profileProp.SetValue(opts, "+3");

        var sut = new SignalResources(Mock.Of<ISignalCliClient>(), Mock.Of<ILogger<SignalResources>>());
        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetAvatarAsync(opts));
    }

    // ---- GetStickerAsync ----

    [Fact]
    public async Task GetStickerAsync_DecodesBase64ToBytes()
    {
        var client = ClientReturningData<GetStickerParameters>(ValidBase64);
        var sut = new SignalResources(client.Object, Mock.Of<ILogger<SignalResources>>());

        var bytes = await sut.GetStickerAsync("+1", "abcdef0123456789abcdef0123456789", stickerId: 0);

        Assert.Equal("Hello", System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task GetStickerAsync_InvalidHexPackId_Throws()
    {
        // Client-side hex-validation запобігає upstream IOException → -32603.
        var sut = new SignalResources(Mock.Of<ISignalCliClient>(), Mock.Of<ILogger<SignalResources>>());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.GetStickerAsync("+1", "NOT-HEX-32-CHARS", stickerId: 0));
    }

    [Fact]
    public async Task GetStickerAsync_WrongLengthPackId_Throws()
    {
        var sut = new SignalResources(Mock.Of<ISignalCliClient>(), Mock.Of<ILogger<SignalResources>>());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.GetStickerAsync("+1", "abc", stickerId: 0)); // < 32 chars
    }

    [Fact]
    public async Task GetStickerAsync_UppercasePackId_Throws()
    {
        // Lowercase-hex enforce'ується щоб match upstream Hex.toByteArray expectations.
        var sut = new SignalResources(Mock.Of<ISignalCliClient>(), Mock.Of<ILogger<SignalResources>>());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.GetStickerAsync("+1", "ABCDEF0123456789ABCDEF0123456789", stickerId: 0));
    }
}
