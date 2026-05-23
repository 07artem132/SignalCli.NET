using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Interfaces.Rpc;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.Signal.Accounts;
using SignalCli.Models.Signal.Devices;
using SignalCli.Models.Signal.Groups;
using SignalCli.Models.SignalCli;
using SignalCli.Services.Signal;

namespace SignalCli.Tests;

public class SignalApiFacadeTests
{
    private static Mock<ISignalCliClient> Client() => new(MockBehavior.Strict);

    // ---- SignalAccounts ----
    [Fact]
    public async Task ListAccounts_ReturnsResponse()
    {
        var client = Client();
        var expected = new ListAccountsResponse { new Account("+1") };
        client.Setup(c => c.InvokeMethodAsync<ListAccountsResponse, ListAccountsParameters>(
            "listAccounts", It.IsAny<ListAccountsParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await new SignalAccounts(client.Object, Mock.Of<ILogger<SignalAccounts>>()).ListAccounts();

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task ListAccounts_WhenNull_Throws()
    {
        var client = Client();
        client.Setup(c => c.InvokeMethodAsync<ListAccountsResponse, ListAccountsParameters>(
            It.IsAny<string>(), It.IsAny<ListAccountsParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ListAccountsResponse)null!);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new SignalAccounts(client.Object, Mock.Of<ILogger<SignalAccounts>>()).ListAccounts());
    }

    // ---- SignalDevices ----
    [Fact]
    public async Task StartLink_ReturnsResponse()
    {
        var client = Client();
        client.Setup(c => c.InvokeMethodAsync<StartLinkResponse, StartLinkParameters>(
            "startLink", It.IsAny<StartLinkParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StartLinkResponse("sgnl://linkdevice?uuid=x"));

        var result = await new SignalDevices(client.Object, Mock.Of<ILogger<SignalDevices>>()).StartLink();

        Assert.Equal("sgnl://linkdevice?uuid=x", result.DeviceLinkUri);
    }

    [Fact]
    public async Task FinishLink_PassesUriAndName_ReturnsResponse()
    {
        var client = Client();
        FinishLinkParameters? captured = null;
        client.Setup(c => c.InvokeMethodAsync<FinishLinkResponse, FinishLinkParameters>(
            "finishLink", It.IsAny<FinishLinkParameters>(), It.IsAny<CancellationToken>()))
            .Callback<string, FinishLinkParameters, CancellationToken>((_, p, _) => captured = p)
            .ReturnsAsync(new FinishLinkResponse("+380501234567"));

        var result = await new SignalDevices(client.Object, Mock.Of<ILogger<SignalDevices>>())
            .FinishLink("sgnl://uri", "MyDevice");

        Assert.Equal("+380501234567", result.number);
        // Перевіряємо, що аргументи реально дійшли у параметри RPC (а не лише "не null")
        Assert.Equal("sgnl://uri", captured!.deviceLinkUri);
        Assert.Equal("MyDevice", captured.deviceName);
    }

    [Fact]
    public async Task StartLink_WhenNull_Throws()
    {
        var client = Client();
        client.Setup(c => c.InvokeMethodAsync<StartLinkResponse, StartLinkParameters>(
            It.IsAny<string>(), It.IsAny<StartLinkParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StartLinkResponse)null!);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new SignalDevices(client.Object, Mock.Of<ILogger<SignalDevices>>()).StartLink());
    }

    // ---- SignalGroups ----
    [Fact]
    public async Task ListGroups_PassesAccount_ReturnsResponse()
    {
        var client = Client();
        ListGroupsParameters? captured = null;
        var expected = new ListGroupsResponse();
        client.Setup(c => c.InvokeMethodAsync<ListGroupsResponse, ListGroupsParameters>(
            "listGroups", It.IsAny<ListGroupsParameters>(), It.IsAny<CancellationToken>()))
            .Callback<string, ListGroupsParameters, CancellationToken>((_, p, _) => captured = p)
            .ReturnsAsync(expected);

        var result = await new SignalGroups(client.Object, Mock.Of<ILogger<SignalGroups>>())
            .ListGroups("+380501234567");

        Assert.Same(expected, result);
        // account має дійти у параметри listGroups
        Assert.Equal("+380501234567", captured!.Account);
    }

    // ---- SignalService (facade over IJsonRpcClientProvider) ----
    [Fact]
    public async Task SignalService_Version_ReturnsResponse()
    {
        var rpc = new Mock<IJsonRpcClient>();
        rpc.Setup(c => c.InvokeMethodAsync<VersionResponse, VersionParameters>(
            "version", It.IsAny<VersionParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VersionResponse("0.14.3"));
        var provider = new Mock<IJsonRpcClientProvider>();
        provider.Setup(p => p.Client).Returns(rpc.Object);

        var version = await new SignalService(provider.Object, Mock.Of<ILogger<SignalService>>()).VersionAsync();

        Assert.Equal("0.14.3", version.Version);
    }

    [Fact]
    public async Task SyncAccount_ReturnsResponse()
    {
        var client = Client();
        client.Setup(c => c.InvokeMethodAsync<SyncAccountsResponse, SyncAccountsParameters>(
            "sendSyncRequest", It.IsAny<SyncAccountsParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncAccountsResponse());

        var result = await new SignalAccounts(client.Object, Mock.Of<ILogger<SignalAccounts>>()).SyncAccount();

        Assert.NotNull(result);
    }

    [Fact]
    public async Task FinishLink_WhenNull_Throws()
    {
        var client = Client();
        client.Setup(c => c.InvokeMethodAsync<FinishLinkResponse, FinishLinkParameters>(
            It.IsAny<string>(), It.IsAny<FinishLinkParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FinishLinkResponse)null!);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new SignalDevices(client.Object, Mock.Of<ILogger<SignalDevices>>()).FinishLink("u", "n"));
    }

    [Fact]
    public async Task ListGroups_WhenNull_Throws()
    {
        var client = Client();
        client.Setup(c => c.InvokeMethodAsync<ListGroupsResponse, ListGroupsParameters>(
            It.IsAny<string>(), It.IsAny<ListGroupsParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ListGroupsResponse)null!);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new SignalGroups(client.Object, Mock.Of<ILogger<SignalGroups>>()).ListGroups("+1"));
    }

    [Fact]
    public async Task SignalService_InvokeMethod_WhenNull_Throws()
    {
        var rpc = new Mock<IJsonRpcClient>();
        rpc.Setup(c => c.InvokeMethodAsync<VersionResponse, VersionParameters>(
            It.IsAny<string>(), It.IsAny<VersionParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VersionResponse)null!);
        var provider = new Mock<IJsonRpcClientProvider>();
        provider.Setup(p => p.Client).Returns(rpc.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new SignalService(provider.Object, Mock.Of<ILogger<SignalService>>()).VersionAsync());
    }
}
