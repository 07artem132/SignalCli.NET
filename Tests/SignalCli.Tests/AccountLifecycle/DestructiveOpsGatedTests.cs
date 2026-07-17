using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models;
using SignalCli.Models.Signal.Accounts;
using SignalCli.Services.Signal;

namespace SignalCli.Tests.AccountLifecycle;

/// <summary>
/// signal-cli-api-coverage Wave 6: pin'ить opt-in destructive-ops gate.
/// Усі 8 нових методів МУСЯТЬ кидати InvalidOperationException ПЕРЕД RPC dispatch
/// якщо EnableDestructiveOperations=false (default).
/// </summary>
public class DestructiveOpsGatedTests
{
    private static SignalAccounts NewSut(bool enabled, Mock<ISignalCliClient>? client = null)
    {
        var options = Options.Create(new SignalCliOptions
        {
            AppHome = "/tmp",
            LibDirectory = "lib",
            JavaExecutable = "/usr/bin/java",
            EnableDestructiveOperations = enabled,
        });
        return new SignalAccounts(
            (client ?? new Mock<ISignalCliClient>()).Object,
            Mock.Of<ILogger<SignalAccounts>>(),
            options);
    }

    // ---- 8 destructive methods × default-false → throw ----

    [Fact]
    public async Task UpdateAccountAsync_GatedOff_Throws()
    {
        var sut = NewSut(enabled: false);
        var opts = new UpdateAccountOptions.Builder("+1").WithDeviceName("x").Build();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.UpdateAccountAsync(opts));
        Assert.Contains("EnableDestructiveOperations", ex.Message);
    }

    [Fact]
    public async Task UnregisterAsync_GatedOff_Throws()
    {
        var sut = NewSut(enabled: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.UnregisterAsync("+1"));
    }

    [Fact]
    public async Task DeleteLocalAccountDataAsync_GatedOff_Throws()
    {
        var sut = NewSut(enabled: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.DeleteLocalAccountDataAsync("+1"));
    }

    [Fact]
    public async Task StartChangeNumberAsync_GatedOff_Throws()
    {
        var sut = NewSut(enabled: false);
        var opts = new StartChangeNumberOptions { Account = "+1", NewNumber = "+2" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.StartChangeNumberAsync(opts));
    }

    [Fact]
    public async Task FinishChangeNumberAsync_GatedOff_Throws()
    {
        var sut = NewSut(enabled: false);
        var opts = new FinishChangeNumberOptions { Account = "+1", NewNumber = "+2", VerificationCode = "123456" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.FinishChangeNumberAsync(opts));
    }

    [Fact]
    public async Task UpdateConfigurationAsync_GatedOff_Throws()
    {
        var sut = NewSut(enabled: false);
        var opts = new UpdateConfigurationOptions { Account = "+1" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.UpdateConfigurationAsync(opts));
    }

    [Fact]
    public async Task SetPinAsync_GatedOff_Throws()
    {
        var sut = NewSut(enabled: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SetPinAsync("+1", "1234"));
    }

    [Fact]
    public async Task RemovePinAsync_GatedOff_Throws()
    {
        var sut = NewSut(enabled: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RemovePinAsync("+1"));
    }

    // ---- Non-destructive methods (ListAccounts/SyncAccount) НЕ повинні affected ----

    [Fact]
    public async Task ListAccountsAsync_GatedOff_StillWorks()
    {
        var client = new Mock<ISignalCliClient>();
        client.Setup(c => c.InvokeMethodAsync(
                "listAccounts",
                It.IsAny<ListAccountsParameters>(),
                It.IsAny<System.Text.Json.Serialization.Metadata.JsonTypeInfo<ListAccountsParameters>>(),
                It.IsAny<System.Text.Json.Serialization.Metadata.JsonTypeInfo<ListAccountsResponse>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListAccountsResponse([]));

        var sut = NewSut(enabled: false, client);
        var resp = await sut.ListAccountsAsync();

        Assert.NotNull(resp); // listAccounts — non-destructive, gate не fire'ить.
    }

    // ---- enabled=true → gate proceeds (RPC actually dispatched) ----

    [Fact]
    public async Task UnregisterAsync_GatedOn_DispatchesRpc()
    {
        UnregisterParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client.Setup(c => c.InvokeMethodAsync(
                "unregister",
                It.IsAny<UnregisterParameters>(),
                It.IsAny<System.Text.Json.Serialization.Metadata.JsonTypeInfo<UnregisterParameters>>(),
                It.IsAny<System.Text.Json.Serialization.Metadata.JsonTypeInfo<System.Text.Json.JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, UnregisterParameters, System.Text.Json.Serialization.Metadata.JsonTypeInfo<UnregisterParameters>, System.Text.Json.Serialization.Metadata.JsonTypeInfo<System.Text.Json.JsonElement>, CancellationToken, TimeSpan?>(
                (_, p, _, _, _, _) => captured = p)
            .ReturnsAsync(default(System.Text.Json.JsonElement));

        var sut = NewSut(enabled: true, client);
        await sut.UnregisterAsync("+1", deleteAccount: true);

        Assert.NotNull(captured);
        Assert.True(captured.DeleteAccount);
    }
}
