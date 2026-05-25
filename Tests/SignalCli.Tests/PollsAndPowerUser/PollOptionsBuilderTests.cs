using SignalCli.Models.Signal.Message;

namespace SignalCli.Tests.PollsAndPowerUser;

/// <summary>
/// signal-cli-api-coverage Wave 7a: builder + validation тести.
/// </summary>
public class PollOptionsBuilderTests
{
    // ---- SendPollCreateOptions §F15 validation ----

    [Fact]
    public void PollCreate_OneOption_Throws()
    {
        // §F15: ≥2 options.
        var b = new SendPollCreateOptions.Builder("+1", "Q?", ["only-one"]);
        Assert.Throws<InvalidOperationException>(() => b.Build());
    }

    [Fact]
    public void PollCreate_ElevenOptions_Throws()
    {
        // §F15: ≤10 options.
        var b = new SendPollCreateOptions.Builder("+1", "Q?", Enumerable.Range(0, 11).Select(i => $"opt{i}"));
        Assert.Throws<InvalidOperationException>(() => b.Build());
    }

    [Fact]
    public void PollCreate_OptionExceeding100Chars_Throws()
    {
        // §F15: ≤100 chars per option.
        var b = new SendPollCreateOptions.Builder("+1", "Q?", ["valid", new string('x', 101)]);
        Assert.Throws<InvalidOperationException>(() => b.Build());
    }

    [Fact]
    public void PollCreate_EmptyOption_Throws()
    {
        var b = new SendPollCreateOptions.Builder("+1", "Q?", ["valid", ""]);
        Assert.Throws<InvalidOperationException>(() => b.Build());
    }

    [Fact]
    public void PollCreate_HappyPath_DefaultsToAllowMultipleVotes()
    {
        // §F21: default polarity AllowMultipleVotes=true.
        var opts = new SendPollCreateOptions.Builder("+1", "Q?", ["A", "B"]).Build();
        Assert.True(opts.AllowMultipleVotes);
    }

    // ---- SendPinMessageOptions §F23 sentinel ----

    [Fact]
    public void PinMessage_DefaultDuration_IsMinusOne_Forever()
    {
        var opts = new SendPinMessageOptions.Builder("+1", "+2", 100L).Build();
        Assert.Equal(-1, opts.PinDurationSeconds);
    }

    [Fact]
    public void PinMessage_PositiveDuration_IsPreserved()
    {
        var opts = new SendPinMessageOptions.Builder("+1", "+2", 100L).WithPinDurationSeconds(3600).Build();
        Assert.Equal(3600, opts.PinDurationSeconds);
    }

    // ---- SendAdminDeleteOptions group-only ----

    [Fact]
    public void AdminDelete_EmptyGroupIds_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SendAdminDeleteOptions.Builder("+1", "+2", 100L, []));
    }

    [Fact]
    public void AdminDelete_HappyPath_PreservesAllFields()
    {
        var opts = new SendAdminDeleteOptions.Builder("+1", "+2", 100L, ["g=="])
            .WithNotifySelf().AsStoryDelete().Build();
        Assert.True(opts.NotifySelf);
        Assert.True(opts.Story);
        Assert.Single(opts.GroupIds);
    }
}
