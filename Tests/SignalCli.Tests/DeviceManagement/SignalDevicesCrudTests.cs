using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.Signal.Devices;
using SignalCli.Services.Signal;

namespace SignalCli.Tests.DeviceManagement;

/// <summary>
/// signal-cli-api-coverage Wave 5: service-level тести для 4-х нових device-management методів.
/// </summary>
public class SignalDevicesCrudTests
{
    // ---- FinishLinkAsync (add-per-call-rpc-timeout) ----

    [Fact]
    public async Task FinishLinkAsync_ForwardsPerCallTimeoutToClient()
    {
        // add-per-call-rpc-timeout: FinishLinkAsync має прокинути per-call timeout у транспорт
        // (ручний QR-скан довший за глобальний RequestTimeoutSeconds).
        TimeSpan? capturedTimeout = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<FinishLinkParameters>(),
                It.IsAny<JsonTypeInfo<FinishLinkParameters>>(),
                It.IsAny<JsonTypeInfo<FinishLinkResponse>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()))
            .Callback<string, FinishLinkParameters, JsonTypeInfo<FinishLinkParameters>, JsonTypeInfo<FinishLinkResponse>, CancellationToken, TimeSpan?>(
                (_, _, _, _, _, t) => capturedTimeout = t)
            .ReturnsAsync(new FinishLinkResponse("+15551234567"));

        var sut = new SignalDevices(client.Object, Mock.Of<ILogger<SignalDevices>>());
        var expected = TimeSpan.FromSeconds(150);

        await sut.FinishLinkAsync("sgnl://linkdevice?uuid=abc", "Worker bot", CancellationToken.None, expected);

        Assert.NotNull(capturedTimeout);
        Assert.Equal(expected, capturedTimeout);
    }

    // ---- AddDeviceAsync ----

    [Fact]
    public async Task AddDeviceAsync_PassesUriThroughVerbatim()
    {
        AddDeviceParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<AddDeviceParameters>(),
                It.IsAny<JsonTypeInfo<AddDeviceParameters>>(),
                It.IsAny<JsonTypeInfo<JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, AddDeviceParameters, JsonTypeInfo<AddDeviceParameters>, JsonTypeInfo<JsonElement>, CancellationToken, TimeSpan?>(
                (_, p, _, _, _, _) => captured = p)
            .ReturnsAsync(default(JsonElement));

        var sut = new SignalDevices(client.Object, Mock.Of<ILogger<SignalDevices>>());
        const string uri = "sgnl://linkdevice?uuid=abc&pub_key=unpaddedBase64Here";

        await sut.AddDeviceAsync("+1", uri);

        // §F11: pass-through без padding modification — signal-cli приймає обидва форми.
        Assert.Equal(uri, captured!.Uri);
    }

    [Fact]
    public async Task AddDeviceAsync_EmptyAccount_Throws()
    {
        var sut = new SignalDevices(Mock.Of<ISignalCliClient>(), Mock.Of<ILogger<SignalDevices>>());
        await Assert.ThrowsAsync<ArgumentException>(() => sut.AddDeviceAsync("", "sgnl://..."));
    }

    // ---- ListDevicesAsync ----

    [Fact]
    public async Task ListDevicesAsync_ReturnsResponse()
    {
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<ListDevicesParameters>(),
                It.IsAny<JsonTypeInfo<ListDevicesParameters>>(),
                It.IsAny<JsonTypeInfo<ListDevicesResponse>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListDevicesResponse([
                new Device(Id: 1L, Name: "Primary", CreatedTimestamp: 1L, LastSeenTimestamp: 2L),
            ]));

        var sut = new SignalDevices(client.Object, Mock.Of<ILogger<SignalDevices>>());
        var resp = await sut.ListDevicesAsync("+1");

        Assert.Single(resp);
        Assert.Equal(1L, resp[0].Id);
    }

    [Fact]
    public async Task ListDevicesAsync_NullResponse_Throws()
    {
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<ListDevicesParameters>(),
                It.IsAny<JsonTypeInfo<ListDevicesParameters>>(),
                It.IsAny<JsonTypeInfo<ListDevicesResponse>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ListDevicesResponse?)null!);

        var sut = new SignalDevices(client.Object, Mock.Of<ILogger<SignalDevices>>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ListDevicesAsync("+1"));
    }

    // ---- RemoveDeviceAsync ----

    [Fact]
    public async Task RemoveDeviceAsync_PassesDeviceIdAsInt()
    {
        RemoveDeviceParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<RemoveDeviceParameters>(),
                It.IsAny<JsonTypeInfo<RemoveDeviceParameters>>(),
                It.IsAny<JsonTypeInfo<JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, RemoveDeviceParameters, JsonTypeInfo<RemoveDeviceParameters>, JsonTypeInfo<JsonElement>, CancellationToken, TimeSpan?>(
                (_, p, _, _, _, _) => captured = p)
            .ReturnsAsync(default(JsonElement));

        var sut = new SignalDevices(client.Object, Mock.Of<ILogger<SignalDevices>>());
        await sut.RemoveDeviceAsync("+1", 42);

        Assert.Equal(42, captured!.DeviceId);
    }

    [Fact]
    public async Task RemoveDeviceAsync_NegativeDeviceId_Throws()
    {
        var sut = new SignalDevices(Mock.Of<ISignalCliClient>(), Mock.Of<ILogger<SignalDevices>>());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.RemoveDeviceAsync("+1", -1));
    }

    [Fact]
    public async Task RemoveDeviceAsync_ZeroDeviceId_Throws()
    {
        var sut = new SignalDevices(Mock.Of<ISignalCliClient>(), Mock.Of<ILogger<SignalDevices>>());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.RemoveDeviceAsync("+1", 0));
    }

    // ---- UpdateDeviceAsync ----

    [Fact]
    public async Task UpdateDeviceAsync_PassesAllFields()
    {
        UpdateDeviceParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<UpdateDeviceParameters>(),
                It.IsAny<JsonTypeInfo<UpdateDeviceParameters>>(),
                It.IsAny<JsonTypeInfo<JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, UpdateDeviceParameters, JsonTypeInfo<UpdateDeviceParameters>, JsonTypeInfo<JsonElement>, CancellationToken, TimeSpan?>(
                (_, p, _, _, _, _) => captured = p)
            .ReturnsAsync(default(JsonElement));

        var sut = new SignalDevices(client.Object, Mock.Of<ILogger<SignalDevices>>());
        await sut.UpdateDeviceAsync("+1", 5, "New Name");

        Assert.Equal(5, captured!.DeviceId);
        Assert.Equal("New Name", captured.DeviceName);
    }

    [Fact]
    public async Task UpdateDeviceAsync_NullDeviceName_Throws()
    {
        var sut = new SignalDevices(Mock.Of<ISignalCliClient>(), Mock.Of<ILogger<SignalDevices>>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.UpdateDeviceAsync("+1", 1, null!));
    }

    [Fact]
    public async Task UpdateDeviceAsync_EmptyDeviceName_NotBlocked()
    {
        // Upstream НЕ блокує empty-string deviceName client-side; передаємо як є.
        UpdateDeviceParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<UpdateDeviceParameters>(),
                It.IsAny<JsonTypeInfo<UpdateDeviceParameters>>(),
                It.IsAny<JsonTypeInfo<JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, UpdateDeviceParameters, JsonTypeInfo<UpdateDeviceParameters>, JsonTypeInfo<JsonElement>, CancellationToken, TimeSpan?>(
                (_, p, _, _, _, _) => captured = p)
            .ReturnsAsync(default(JsonElement));

        var sut = new SignalDevices(client.Object, Mock.Of<ILogger<SignalDevices>>());
        await sut.UpdateDeviceAsync("+1", 1, ""); // empty allowed

        Assert.Equal("", captured!.DeviceName);
    }
}
