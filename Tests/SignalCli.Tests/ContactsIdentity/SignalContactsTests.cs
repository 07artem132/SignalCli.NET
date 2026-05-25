using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.Signal.Contacts;
using SignalCli.Services.Signal;

namespace SignalCli.Tests.ContactsIdentity;

/// <summary>
/// signal-cli-api-coverage Wave 3: service-level тести для 9 нових methods
/// (Trust split → 2 type-safe API).
/// </summary>
public class SignalContactsTests
{
    // ---- ListContactsAsync ----

    [Fact]
    public async Task ListContactsAsync_PassesFilterParameters()
    {
        ListContactsParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<ListContactsParameters>(),
                It.IsAny<JsonTypeInfo<ListContactsParameters>>(),
                It.IsAny<JsonTypeInfo<ListContactsResponse>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, ListContactsParameters, JsonTypeInfo<ListContactsParameters>, JsonTypeInfo<ListContactsResponse>, CancellationToken>(
                (_, p, _, _, _) => captured = p)
            .ReturnsAsync(new ListContactsResponse([]));

        var sut = new SignalContacts(client.Object, Mock.Of<ILogger<SignalContacts>>());
        await sut.ListContactsAsync("+1", recipients: ["+2"], allRecipients: true, blocked: false, name: "Foo", includeInternal: true);

        Assert.Equal("+1", captured!.Account);
        Assert.True(captured.AllRecipients);
        Assert.False(captured.Blocked);
        Assert.Equal("Foo", captured.Name);
        Assert.True(captured.Internal);
    }

    [Fact]
    public async Task ListContactsAsync_EmptyAccount_Throws()
    {
        var sut = new SignalContacts(Mock.Of<ISignalCliClient>(), Mock.Of<ILogger<SignalContacts>>());
        await Assert.ThrowsAsync<ArgumentException>(() => sut.ListContactsAsync(""));
    }

    // ---- ListIdentitiesAsync ----

    [Fact]
    public async Task ListIdentitiesAsync_PassesNumberFilter()
    {
        ListIdentitiesParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<ListIdentitiesParameters>(),
                It.IsAny<JsonTypeInfo<ListIdentitiesParameters>>(),
                It.IsAny<JsonTypeInfo<ListIdentitiesResponse>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, ListIdentitiesParameters, JsonTypeInfo<ListIdentitiesParameters>, JsonTypeInfo<ListIdentitiesResponse>, CancellationToken>(
                (_, p, _, _, _) => captured = p)
            .ReturnsAsync(new ListIdentitiesResponse([]));

        var sut = new SignalContacts(client.Object, Mock.Of<ILogger<SignalContacts>>());
        await sut.ListIdentitiesAsync("+1", recipient: "+2");

        Assert.Equal("+2", captured!.Number);
    }

    // ---- Trust split (XOR через 2 type-safe API) ----

    [Fact]
    public async Task TrustAllKnownKeysAsync_SetsCorrectFlags()
    {
        TrustParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<TrustParameters>(),
                It.IsAny<JsonTypeInfo<TrustParameters>>(),
                It.IsAny<JsonTypeInfo<JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, TrustParameters, JsonTypeInfo<TrustParameters>, JsonTypeInfo<JsonElement>, CancellationToken>(
                (_, p, _, _, _) => captured = p)
            .ReturnsAsync(default(JsonElement));

        var sut = new SignalContacts(client.Object, Mock.Of<ILogger<SignalContacts>>());
        await sut.TrustAllKnownKeysAsync("+1", "+2");

        Assert.True(captured!.TrustAllKnownKeys);
        Assert.Null(captured.VerifiedSafetyNumber);
    }

    [Fact]
    public async Task TrustVerifiedAsync_SetsCorrectFlags()
    {
        TrustParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<TrustParameters>(),
                It.IsAny<JsonTypeInfo<TrustParameters>>(),
                It.IsAny<JsonTypeInfo<JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, TrustParameters, JsonTypeInfo<TrustParameters>, JsonTypeInfo<JsonElement>, CancellationToken>(
                (_, p, _, _, _) => captured = p)
            .ReturnsAsync(default(JsonElement));

        var sut = new SignalContacts(client.Object, Mock.Of<ILogger<SignalContacts>>());
        await sut.TrustVerifiedAsync("+1", "+2", "12345 67890");

        Assert.False(captured!.TrustAllKnownKeys);
        Assert.Equal("12345 67890", captured.VerifiedSafetyNumber);
    }

    // ---- RemoveContactAsync: enum→XOR mapping ----

    [Theory]
    [InlineData(RemoveContactMode.DeleteContact, false, false)]
    [InlineData(RemoveContactMode.Hide, true, false)]
    [InlineData(RemoveContactMode.Forget, false, true)]
    public async Task RemoveContactAsync_EnumModeMapsToCorrectFlags(RemoveContactMode mode, bool expectedHide, bool expectedForget)
    {
        RemoveContactParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<RemoveContactParameters>(),
                It.IsAny<JsonTypeInfo<RemoveContactParameters>>(),
                It.IsAny<JsonTypeInfo<JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, RemoveContactParameters, JsonTypeInfo<RemoveContactParameters>, JsonTypeInfo<JsonElement>, CancellationToken>(
                (_, p, _, _, _) => captured = p)
            .ReturnsAsync(default(JsonElement));

        var sut = new SignalContacts(client.Object, Mock.Of<ILogger<SignalContacts>>());
        await sut.RemoveContactAsync("+1", "+2", mode);

        // §F9: XOR гарантовано (рівно одне з {hide=true, forget=true, обидва=false}).
        Assert.Equal(expectedHide, captured!.Hide);
        Assert.Equal(expectedForget, captured.Forget);
    }

    // ---- UpdateProfileAsync: defense-in-depth XOR (Builder + service guard) ----

    [Fact]
    public async Task UpdateProfileAsync_BothAvatarAndRemove_ThrowsOnServiceSide()
    {
        // Defense-in-depth: якщо хтось обійшов Builder XOR (direct record-construction),
        // service-метод усе одно guards.
        var options = new UpdateProfileOptions.Builder("+1").Build();
        // Поки використовуємо reflection щоб виставити обидва поля (через Builder неможливо).
        var avatarProp = typeof(UpdateProfileOptions).GetProperty("AvatarPath")!;
        var removeProp = typeof(UpdateProfileOptions).GetProperty("RemoveAvatar")!;
        avatarProp.SetValue(options, "/tmp/avatar.jpg");
        removeProp.SetValue(options, true);

        var sut = new SignalContacts(Mock.Of<ISignalCliClient>(), Mock.Of<ILogger<SignalContacts>>());
        await Assert.ThrowsAsync<ArgumentException>(() => sut.UpdateProfileAsync(options));
    }

    // ---- Block/Unblock ----

    [Fact]
    public async Task BlockAsync_NullCollections_TolerantWithoutThrow()
    {
        BlockParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<BlockParameters>(),
                It.IsAny<JsonTypeInfo<BlockParameters>>(),
                It.IsAny<JsonTypeInfo<JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, BlockParameters, JsonTypeInfo<BlockParameters>, JsonTypeInfo<JsonElement>, CancellationToken>(
                (_, p, _, _, _) => captured = p)
            .ReturnsAsync(default(JsonElement));

        var sut = new SignalContacts(client.Object, Mock.Of<ILogger<SignalContacts>>());
        await sut.BlockAsync("+1"); // обидва nulls — допустимо (upstream приймає empty arrays silently)

        Assert.NotNull(captured);
        Assert.Equal("+1", captured.Account);
    }
}
