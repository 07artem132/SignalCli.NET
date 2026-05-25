using SignalCli.Models.Signal.Groups;

namespace SignalCli.Tests.GroupsCrud;

/// <summary>
/// signal-cli-api-coverage Wave 2: builder + validation тести для <see cref="UpdateGroupOptions"/>.
/// </summary>
public class UpdateGroupOptionsTests
{
    [Fact]
    public void Builder_HappyPath_BuildsAllFields()
    {
        var opts = new UpdateGroupOptions.Builder("+1")
            .WithGroupId("g==")
            .WithName("name")
            .WithDescription("desc")
            .WithAvatarPath("/tmp/avatar.jpg")
            .WithMembers(["+2"])
            .WithRemoveMembers(["+3"])
            .WithAdmins(["+4"])
            .WithRemoveAdmins(["+5"])
            .WithBans(["+6"])
            .WithUnbans(["+7"])
            .WithResetLink()
            .WithLinkState(GroupLinkState.Enabled)
            .WithPermissionAddMember(GroupPermission.OnlyAdmins)
            .WithPermissionEditDetails(GroupPermission.EveryMember)
            .WithPermissionSendMessages(GroupPermission.OnlyAdmins)
            .WithExpiration(3600)
            .Build();

        Assert.Equal("g==", opts.GroupId);
        Assert.Equal("name", opts.Name);
        Assert.Equal("desc", opts.Description);
        Assert.Equal("/tmp/avatar.jpg", opts.AvatarPath);
        Assert.Equal(["+2"], opts.Members);
        Assert.Equal(["+3"], opts.RemoveMembers);
        Assert.Equal(["+4"], opts.Admins);
        Assert.Equal(["+5"], opts.RemoveAdmins);
        Assert.Equal(["+6"], opts.Bans);
        Assert.Equal(["+7"], opts.Unbans);
        Assert.True(opts.ResetLink);
        Assert.Equal(GroupLinkState.Enabled, opts.LinkState);
        Assert.Equal(GroupPermission.OnlyAdmins, opts.PermissionAddMember);
        Assert.Equal(GroupPermission.EveryMember, opts.PermissionEditDetails);
        Assert.Equal(GroupPermission.OnlyAdmins, opts.PermissionSendMessages);
        Assert.Equal(3600, opts.Expiration);
    }

    [Fact]
    public void Builder_WithoutGroupId_DefaultsToCreateMode()
    {
        // §F14: відсутність GroupId = валідний create-mode сценарій, не помилка валідації.
        var opts = new UpdateGroupOptions.Builder("+1").WithName("brand-new").Build();
        Assert.Null(opts.GroupId);
        Assert.Equal("brand-new", opts.Name);
    }

    [Fact]
    public void Builder_EmptyAccount_Throws()
    {
        Assert.Throws<ArgumentException>(() => new UpdateGroupOptions.Builder(""));
    }

    [Fact]
    public void Builder_NegativeExpiration_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UpdateGroupOptions.Builder("+1").WithExpiration(-1));
    }

    [Fact]
    public void Builder_ExpirationZero_DisablesTimer()
    {
        var opts = new UpdateGroupOptions.Builder("+1").WithExpiration(0).Build();
        Assert.Equal(0, opts.Expiration);
    }
}
