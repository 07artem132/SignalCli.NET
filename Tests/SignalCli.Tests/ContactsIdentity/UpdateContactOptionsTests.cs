using SignalCli.Models.Signal.Contacts;

namespace SignalCli.Tests.ContactsIdentity;

public class UpdateContactOptionsTests
{
    [Fact]
    public void Builder_HappyPath_BuildsAllFields()
    {
        var opts = new UpdateContactOptions.Builder("+1", "+2")
            .WithName("Foo")
            .WithGivenName("Foo")
            .WithFamilyName("Bar")
            .WithNickGivenName("F")
            .WithNickFamilyName("B")
            .WithNote("VIP")
            .WithExpiration(3600)
            .Build();

        Assert.Equal("+1", opts.Account);
        Assert.Equal("+2", opts.Recipient);
        Assert.Equal("Foo", opts.Name);
        Assert.Equal("Bar", opts.FamilyName);
        Assert.Equal(3600, opts.Expiration);
    }

    [Fact]
    public void Builder_EmptyRecipient_Throws()
    {
        Assert.Throws<ArgumentException>(() => new UpdateContactOptions.Builder("+1", ""));
    }

    [Fact]
    public void Builder_NegativeExpiration_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UpdateContactOptions.Builder("+1", "+2").WithExpiration(-1));
    }

    [Fact]
    public void Builder_EmptyStringClearsField()
    {
        // Empty string — canonical "clear" value (per UpdateContactOptions XMLDoc).
        var opts = new UpdateContactOptions.Builder("+1", "+2").WithNote("").Build();
        Assert.Equal("", opts.Note);
    }
}
