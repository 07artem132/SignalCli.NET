using SignalCli.Models.Signal.Resources;

namespace SignalCli.Tests.StickersAndResources;

/// <summary>
/// signal-cli-api-coverage Wave 4: §F19 3-way XOR enforcement у GetAvatarOptions.Builder.
/// </summary>
public class GetAvatarOptionsTests
{
    [Fact]
    public void Builder_Contact_BuildsOnlyContact()
    {
        var opts = new GetAvatarOptions.Builder("+1").WithContact("+2").Build();
        Assert.Equal("+2", opts.Contact);
        Assert.Null(opts.Profile);
        Assert.Null(opts.GroupId);
    }

    [Fact]
    public void Builder_Profile_BuildsOnlyProfile()
    {
        var opts = new GetAvatarOptions.Builder("+1").WithProfile("+2").Build();
        Assert.Equal("+2", opts.Profile);
        Assert.Null(opts.Contact);
        Assert.Null(opts.GroupId);
    }

    [Fact]
    public void Builder_GroupId_BuildsOnlyGroupId()
    {
        var opts = new GetAvatarOptions.Builder("+1").WithGroupId("g==").Build();
        Assert.Equal("g==", opts.GroupId);
        Assert.Null(opts.Contact);
        Assert.Null(opts.Profile);
    }

    [Fact]
    public void Builder_ContactThenProfile_ThrowsXOR()
    {
        var b = new GetAvatarOptions.Builder("+1").WithContact("+2");
        Assert.Throws<InvalidOperationException>(() => b.WithProfile("+3"));
    }

    [Fact]
    public void Builder_ProfileThenGroupId_ThrowsXOR()
    {
        var b = new GetAvatarOptions.Builder("+1").WithProfile("+2");
        Assert.Throws<InvalidOperationException>(() => b.WithGroupId("g=="));
    }

    [Fact]
    public void Builder_NoSource_Build_Throws()
    {
        var b = new GetAvatarOptions.Builder("+1");
        Assert.Throws<InvalidOperationException>(() => b.Build());
    }

    [Fact]
    public void Builder_EmptyAccount_Throws()
    {
        Assert.Throws<ArgumentException>(() => new GetAvatarOptions.Builder(""));
    }
}
