using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models;
using SignalCli.Models.Signal.Accounts;
using SignalCli.Services.Signal;

namespace SignalCli.Tests.UtilityRpc;

/// <summary>
/// signal-cli-api-coverage Wave 8: service-level тести для 3-х utility methods.
/// Жоден з них НЕ destructive, тож працюють із default EnableDestructiveOperations=false.
/// </summary>
public class SignalAccountsUtilityTests
{
    private static SignalAccounts NewSut(Mock<ISignalCliClient>? client = null)
    {
        var options = Options.Create(new SignalCliOptions
        {
            AppHome = "/tmp",
            LibDirectory = "lib",
            JavaExecutable = "/usr/bin/java",
        });
        return new SignalAccounts(
            (client ?? new Mock<ISignalCliClient>()).Object,
            Mock.Of<ILogger<SignalAccounts>>(),
            options);
    }

    [Fact]
    public async Task GetUserStatusAsync_ReturnsResponse()
    {
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<GetUserStatusParameters>(),
                It.IsAny<JsonTypeInfo<GetUserStatusParameters>>(),
                It.IsAny<JsonTypeInfo<GetUserStatusResponse>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserStatusResponse([
                new JsonUserStatus("+1", "+1", null, "uuid-1", true),
            ]));

        var sut = NewSut(client);
        var resp = await sut.GetUserStatusAsync("+1", recipients: ["+1"]);

        Assert.Single(resp);
        Assert.True(resp[0].IsRegistered);
    }

    [Fact]
    public async Task GetUserStatusAsync_NoOptInRequired_WorksWithDefaultOptions()
    {
        // Wave 8 = non-destructive → жодного opt-in не потрібно (на відміну від Wave 6).
        var client = new Mock<ISignalCliClient>();
        client.Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<GetUserStatusParameters>(),
                It.IsAny<JsonTypeInfo<GetUserStatusParameters>>(),
                It.IsAny<JsonTypeInfo<GetUserStatusResponse>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserStatusResponse([]));

        var sut = NewSut(client);
        // EnableDestructiveOperations НЕ виставлено — і це OK для GetUserStatus.
        var resp = await sut.GetUserStatusAsync("+1");
        Assert.Empty(resp);
    }

    [Fact]
    public async Task GetUserStatusAsync_EmptyAccount_Throws()
    {
        var sut = NewSut();
        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetUserStatusAsync(""));
    }

    [Fact]
    public async Task SubmitRateLimitChallengeAsync_DispatchesRpc()
    {
        SubmitRateLimitChallengeParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<SubmitRateLimitChallengeParameters>(),
                It.IsAny<JsonTypeInfo<SubmitRateLimitChallengeParameters>>(),
                It.IsAny<JsonTypeInfo<JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, SubmitRateLimitChallengeParameters, JsonTypeInfo<SubmitRateLimitChallengeParameters>, JsonTypeInfo<JsonElement>, CancellationToken, TimeSpan?>(
                (_, p, _, _, _, _) => captured = p)
            .ReturnsAsync(default(JsonElement));

        var sut = NewSut(client);
        await sut.SubmitRateLimitChallengeAsync("+1", "challenge", "captcha");

        Assert.Equal("challenge", captured!.Challenge);
        Assert.Equal("captcha", captured.Captcha);
    }

    [Fact]
    public async Task SubmitRateLimitChallengeAsync_EmptyChallenge_Throws()
    {
        var sut = NewSut();
        await Assert.ThrowsAsync<ArgumentException>(() => sut.SubmitRateLimitChallengeAsync("+1", "", "captcha"));
    }

    [Fact]
    public async Task SubmitRateLimitChallengeAsync_EmptyCaptcha_Throws()
    {
        var sut = NewSut();
        await Assert.ThrowsAsync<ArgumentException>(() => sut.SubmitRateLimitChallengeAsync("+1", "challenge", ""));
    }

    [Fact]
    public async Task SendContactsAsync_DispatchesRpc()
    {
        SendContactsParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<SendContactsParameters>(),
                It.IsAny<JsonTypeInfo<SendContactsParameters>>(),
                It.IsAny<JsonTypeInfo<JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, SendContactsParameters, JsonTypeInfo<SendContactsParameters>, JsonTypeInfo<JsonElement>, CancellationToken, TimeSpan?>(
                (_, p, _, _, _, _) => captured = p)
            .ReturnsAsync(default(JsonElement));

        var sut = NewSut(client);
        await sut.SendContactsAsync("+1");

        Assert.Equal("+1", captured!.Account);
    }
}
