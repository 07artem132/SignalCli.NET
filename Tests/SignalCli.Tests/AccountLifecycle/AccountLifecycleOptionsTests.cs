using SignalCli.Models.Signal.Accounts;

namespace SignalCli.Tests.AccountLifecycle;

/// <summary>
/// signal-cli-api-coverage Wave 6: builder + validation тести для UpdateAccountOptions XOR
/// та client-side enforce-mins (PIN length).
/// </summary>
public class AccountLifecycleOptionsTests
{
    // ---- UpdateAccountOptions XOR ----

    [Fact]
    public void UpdateAccountOptions_Builder_HappyPath()
    {
        var opts = new UpdateAccountOptions.Builder("+1")
            .WithDeviceName("phone")
            .WithUnrestrictedUnidentifiedSender(true)
            .WithDiscoverableByNumber(false)
            .WithNumberSharing(true) // §F3 bool
            .WithUsername("alice.42")
            .Build();

        Assert.Equal("phone", opts.DeviceName);
        Assert.True(opts.UnrestrictedUnidentifiedSender);
        Assert.False(opts.DiscoverableByNumber);
        Assert.True(opts.NumberSharing);
        Assert.Equal("alice.42", opts.Username);
        Assert.False(opts.DeleteUsername);
    }

    [Fact]
    public void UpdateAccountOptions_UsernameThenDelete_ThrowsXOR()
    {
        var b = new UpdateAccountOptions.Builder("+1").WithUsername("alice.42");
        Assert.Throws<InvalidOperationException>(() => b.WithDeleteUsername());
    }

    [Fact]
    public void UpdateAccountOptions_DeleteThenUsername_ThrowsXOR()
    {
        var b = new UpdateAccountOptions.Builder("+1").WithDeleteUsername();
        Assert.Throws<InvalidOperationException>(() => b.WithUsername("alice.42"));
    }

    [Fact]
    public void UpdateAccountOptions_DeleteAlone_Works()
    {
        var opts = new UpdateAccountOptions.Builder("+1").WithDeleteUsername().Build();
        Assert.True(opts.DeleteUsername);
        Assert.Null(opts.Username);
    }

    [Fact]
    public void UpdateAccountOptions_EmptyAccount_Throws()
    {
        Assert.Throws<ArgumentException>(() => new UpdateAccountOptions.Builder(""));
    }
}
