using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.Signal.Stickers;
using SignalCli.Services.Signal;

namespace SignalCli.Tests.StickersAndResources;

public class SignalStickersTests
{
    [Fact]
    public async Task UploadStickerPackAsync_ReturnsUrl()
    {
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<UploadStickerPackParameters>(),
                It.IsAny<JsonTypeInfo<UploadStickerPackParameters>>(),
                It.IsAny<JsonTypeInfo<UploadStickerPackResponse>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadStickerPackResponse("https://signal.art/#abc"));

        var sut = new SignalStickers(client.Object, Mock.Of<ILogger<SignalStickers>>());
        var resp = await sut.UploadStickerPackAsync("+1", "/tmp/pack.zip");

        Assert.Contains("signal.art", resp.Url);
    }

    [Fact]
    public async Task ListStickerPacksAsync_ReturnsResponse()
    {
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<ListStickerPacksParameters>(),
                It.IsAny<JsonTypeInfo<ListStickerPacksParameters>>(),
                It.IsAny<JsonTypeInfo<ListStickerPacksResponse>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListStickerPacksResponse([]));

        var sut = new SignalStickers(client.Object, Mock.Of<ILogger<SignalStickers>>());
        var resp = await sut.ListStickerPacksAsync("+1");

        Assert.NotNull(resp);
        Assert.Empty(resp);
    }

    [Fact]
    public async Task AddStickerPackAsync_EmptyUris_Throws()
    {
        var sut = new SignalStickers(Mock.Of<ISignalCliClient>(), Mock.Of<ILogger<SignalStickers>>());
        await Assert.ThrowsAsync<ArgumentException>(() => sut.AddStickerPackAsync("+1", []));
    }

    [Fact]
    public async Task AddStickerPackAsync_PassesUriListToWire()
    {
        AddStickerPackParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<AddStickerPackParameters>(),
                It.IsAny<JsonTypeInfo<AddStickerPackParameters>>(),
                It.IsAny<JsonTypeInfo<JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, AddStickerPackParameters, JsonTypeInfo<AddStickerPackParameters>, JsonTypeInfo<JsonElement>, CancellationToken, TimeSpan?>(
                (_, p, _, _, _, _) => captured = p)
            .ReturnsAsync(default(JsonElement));

        var sut = new SignalStickers(client.Object, Mock.Of<ILogger<SignalStickers>>());
        await sut.AddStickerPackAsync("+1", ["https://signal.art/#a", "https://signal.art/#b"]);

        Assert.Equal(2, captured!.Uri.Count());
    }
}
