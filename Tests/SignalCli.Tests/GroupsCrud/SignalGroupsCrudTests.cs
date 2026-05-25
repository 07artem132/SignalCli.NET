using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.Signal.Groups;
using SignalCli.Services.Signal;

namespace SignalCli.Tests.GroupsCrud;

/// <summary>
/// signal-cli-api-coverage Wave 2: service-level тести для 3-х нових group-методів.
/// (Subfolder name <c>GroupsCrud</c>, а не <c>Services.Signal</c>, щоб уникнути
/// namespace-shadowing — див. Wave 1 lesson learned.)
/// </summary>
public class SignalGroupsCrudTests
{
    // ---- JoinGroupAsync ----

    [Fact]
    public async Task JoinGroupAsync_MapsParamsAndReturnsResponse()
    {
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<JoinGroupParameters>(),
                It.IsAny<JsonTypeInfo<JoinGroupParameters>>(),
                It.IsAny<JsonTypeInfo<JoinGroupResponse>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JoinGroupResponse(42L, null, "Z3JvdXBJZA==", OnlyRequested: true));

        var sut = new SignalGroups(client.Object, Mock.Of<ILogger<SignalGroups>>());
        var resp = await sut.JoinGroupAsync("+1", "https://signal.group/#abc");

        Assert.Equal("Z3JvdXBJZA==", resp.GroupId);
        Assert.True(resp.OnlyRequested);
    }

    [Fact]
    public async Task JoinGroupAsync_EmptyAccount_Throws()
    {
        var sut = new SignalGroups(Mock.Of<ISignalCliClient>(), Mock.Of<ILogger<SignalGroups>>());
        await Assert.ThrowsAsync<ArgumentException>(() => sut.JoinGroupAsync("", "uri"));
    }

    [Fact]
    public async Task JoinGroupAsync_EmptyUri_Throws()
    {
        var sut = new SignalGroups(Mock.Of<ISignalCliClient>(), Mock.Of<ILogger<SignalGroups>>());
        await Assert.ThrowsAsync<ArgumentException>(() => sut.JoinGroupAsync("+1", ""));
    }

    // ---- UpdateGroupAsync ----

    [Fact]
    public async Task UpdateGroupAsync_WithGroupId_PassesGroupIdAndExpectsUpdatePath()
    {
        UpdateGroupParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<UpdateGroupParameters>(),
                It.IsAny<JsonTypeInfo<UpdateGroupParameters>>(),
                It.IsAny<JsonTypeInfo<UpdateGroupResponse>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, UpdateGroupParameters, JsonTypeInfo<UpdateGroupParameters>, JsonTypeInfo<UpdateGroupResponse>, CancellationToken>(
                (_, p, _, _, _) => captured = p)
            .ReturnsAsync(new UpdateGroupResponse(TimeStamp: 1L, Results: null, GroupId: null));

        var sut = new SignalGroups(client.Object, Mock.Of<ILogger<SignalGroups>>());
        var opts = new UpdateGroupOptions.Builder("+1")
            .WithGroupId("g==")
            .WithName("new name")
            .WithLinkState(GroupLinkState.EnabledWithApproval)
            .WithPermissionAddMember(GroupPermission.OnlyAdmins)
            .Build();

        var resp = await sut.UpdateGroupAsync(opts);

        Assert.Equal("g==", captured!.GroupId);
        Assert.Equal("new name", captured.Name);
        // §enum→kebab mapping.
        Assert.Equal("enabled-with-approval", captured.Link);
        Assert.Equal("only-admins", captured.SetPermissionAddMember);
        // §F14: update-path → response.GroupId == null.
        Assert.Null(resp.GroupId);
    }

    [Fact]
    public async Task UpdateGroupAsync_WithoutGroupId_TriggersCreatePath_ResponseHasGroupId()
    {
        // §F14: groupId=null у request → create-mode → response містить новий groupId.
        UpdateGroupParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<UpdateGroupParameters>(),
                It.IsAny<JsonTypeInfo<UpdateGroupParameters>>(),
                It.IsAny<JsonTypeInfo<UpdateGroupResponse>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, UpdateGroupParameters, JsonTypeInfo<UpdateGroupParameters>, JsonTypeInfo<UpdateGroupResponse>, CancellationToken>(
                (_, p, _, _, _) => captured = p)
            .ReturnsAsync(new UpdateGroupResponse(TimeStamp: 1L, Results: null, GroupId: "NEW-GROUP-ID"));

        var sut = new SignalGroups(client.Object, Mock.Of<ILogger<SignalGroups>>());
        var opts = new UpdateGroupOptions.Builder("+1").WithName("brand new group").Build();

        var resp = await sut.UpdateGroupAsync(opts);

        Assert.Null(captured!.GroupId); // create-mode: groupId не передано
        Assert.Equal("brand new group", captured.Name);
        Assert.Equal("NEW-GROUP-ID", resp.GroupId); // upstream повернув новий ID
    }

    [Fact]
    public async Task UpdateGroupAsync_NullOptions_Throws()
    {
        var sut = new SignalGroups(Mock.Of<ISignalCliClient>(), Mock.Of<ILogger<SignalGroups>>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.UpdateGroupAsync(null!));
    }

    // ---- QuitGroupAsync ----

    [Fact]
    public async Task QuitGroupAsync_NormalPath_ReturnsTimestampAndResults()
    {
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<QuitGroupParameters>(),
                It.IsAny<JsonTypeInfo<QuitGroupParameters>>(),
                It.IsAny<JsonTypeInfo<QuitGroupResponse>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuitGroupResponse(TimeStamp: 42L, Results: []));

        var sut = new SignalGroups(client.Object, Mock.Of<ILogger<SignalGroups>>());
        var resp = await sut.QuitGroupAsync("+1", "g==");

        Assert.Equal(42L, resp.TimeStamp);
        Assert.False(resp.WasAlreadyNotMember);
    }

    [Fact]
    public async Task QuitGroupAsync_IdempotentNotMember_ReturnsSuccessNoOp_DoesNotThrow()
    {
        // §F8: upstream NotAGroupMemberException → {} → response.WasAlreadyNotMember == true,
        // НЕ виняток (per CLAUDE.md rule #14).
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<QuitGroupParameters>(),
                It.IsAny<JsonTypeInfo<QuitGroupParameters>>(),
                It.IsAny<JsonTypeInfo<QuitGroupResponse>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuitGroupResponse(TimeStamp: null, Results: null));

        var sut = new SignalGroups(client.Object, Mock.Of<ILogger<SignalGroups>>());
        var resp = await sut.QuitGroupAsync("+1", "g==");

        Assert.True(resp.WasAlreadyNotMember);
        Assert.Null(resp.TimeStamp);
    }

    [Fact]
    public async Task QuitGroupAsync_PassesDeleteAndAdminsToWire()
    {
        QuitGroupParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client
            .Setup(c => c.InvokeMethodAsync(
                It.IsAny<string>(),
                It.IsAny<QuitGroupParameters>(),
                It.IsAny<JsonTypeInfo<QuitGroupParameters>>(),
                It.IsAny<JsonTypeInfo<QuitGroupResponse>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, QuitGroupParameters, JsonTypeInfo<QuitGroupParameters>, JsonTypeInfo<QuitGroupResponse>, CancellationToken>(
                (_, p, _, _, _) => captured = p)
            .ReturnsAsync(new QuitGroupResponse(1L, []));

        var sut = new SignalGroups(client.Object, Mock.Of<ILogger<SignalGroups>>());
        await sut.QuitGroupAsync("+1", "g==", deleteLocally: true, admins: ["+2", "+3"]);

        Assert.True(captured!.Delete);
        Assert.Equal(new[] { "+2", "+3" }, captured.Admins);
    }

    [Fact]
    public async Task QuitGroupAsync_EmptyGroupId_Throws()
    {
        var sut = new SignalGroups(Mock.Of<ISignalCliClient>(), Mock.Of<ILogger<SignalGroups>>());
        await Assert.ThrowsAsync<ArgumentException>(() => sut.QuitGroupAsync("+1", ""));
    }
}
