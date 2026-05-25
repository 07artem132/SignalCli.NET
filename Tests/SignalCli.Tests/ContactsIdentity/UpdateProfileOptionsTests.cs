using SignalCli.Models.Signal.Contacts;

namespace SignalCli.Tests.ContactsIdentity;

public class UpdateProfileOptionsTests
{
    [Fact]
    public void Builder_HappyPath_BuildsAllFields()
    {
        var opts = new UpdateProfileOptions.Builder("+1")
            .WithGivenName("Foo")
            .WithFamilyName("Bar")
            .WithAbout("Hi there")
            .WithAboutEmoji("👋")
            .WithMobileCoinAddress("dGVzdA==")
            .WithAvatarPath("/tmp/avatar.jpg")
            .Build();

        Assert.Equal("Foo", opts.GivenName);
        Assert.Equal("Bar", opts.FamilyName);
        Assert.Equal("Hi there", opts.About);
        Assert.Equal("👋", opts.AboutEmoji);
        Assert.Equal("dGVzdA==", opts.MobileCoinAddress);
        Assert.Equal("/tmp/avatar.jpg", opts.AvatarPath);
        Assert.False(opts.RemoveAvatar);
    }

    [Fact]
    public void Builder_AvatarPathThenRemove_ThrowsXOR()
    {
        // §F18: Builder enforce'ить XOR client-side.
        var b = new UpdateProfileOptions.Builder("+1").WithAvatarPath("/tmp/avatar.jpg");
        Assert.Throws<InvalidOperationException>(() => b.WithRemoveAvatar());
    }

    [Fact]
    public void Builder_RemoveAvatarThenPath_ThrowsXOR()
    {
        var b = new UpdateProfileOptions.Builder("+1").WithRemoveAvatar();
        Assert.Throws<InvalidOperationException>(() => b.WithAvatarPath("/tmp/avatar.jpg"));
    }

    [Fact]
    public void Builder_RemoveAvatarAlone_Works()
    {
        var opts = new UpdateProfileOptions.Builder("+1").WithRemoveAvatar().Build();
        Assert.True(opts.RemoveAvatar);
        Assert.Null(opts.AvatarPath);
    }

    [Fact]
    public void Builder_EmptyAccount_Throws()
    {
        Assert.Throws<ArgumentException>(() => new UpdateProfileOptions.Builder(""));
    }
}
