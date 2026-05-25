using SignalCli.Models.Signal.Message;

namespace SignalCli.Tests.MessagingInteractive;

/// <summary>
/// signal-cli-api-coverage Wave 1: builder + validation тести для 4-х нових Options-типів.
/// </summary>
public class InteractiveOptionsTests
{
    // ---- ReactionOptions ----

    [Fact]
    public void ReactionOptions_Builder_HappyPath_BuildsCorrectShape()
    {
        var opts = new ReactionOptions.Builder("+1", "👍", 100L)
            .WithRecipients(["+2"])
            .WithTargetAuthor("+3")
            .AsRemove()
            .AsStoryReaction()
            .WithNotifySelf()
            .Build();

        Assert.Equal("+1", opts.Account);
        Assert.Equal("👍", opts.Emoji);
        Assert.Equal(100L, opts.TargetTimestamp);
        Assert.Equal(["+2"], opts.Recipients);
        Assert.Equal("+3", opts.TargetAuthor);
        Assert.True(opts.Remove);
        Assert.True(opts.Story);
        Assert.True(opts.NotifySelf);
    }

    [Fact]
    public void ReactionOptions_Builder_EmptyAccount_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ReactionOptions.Builder("", "👍", 1L));
    }

    [Fact]
    public void ReactionOptions_Builder_EmptyEmoji_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ReactionOptions.Builder("+1", "", 1L));
    }

    [Fact]
    public void ReactionOptions_Build_WithoutAnyRecipientSource_Throws()
    {
        // Має бути виставлений як мінімум один з Recipients/GroupIds/Usernames/NoteToSelf.
        var b = new ReactionOptions.Builder("+1", "👍", 1L);
        Assert.Throws<InvalidOperationException>(() => b.Build());
    }

    [Fact]
    public void ReactionOptions_NoteToSelfAlone_Sufficient()
    {
        var opts = new ReactionOptions.Builder("+1", "👍", 1L).WithNoteToSelf().Build();
        Assert.True(opts.NoteToSelf);
    }

    // ---- ReceiptOptions ----

    [Fact]
    public void ReceiptOptions_Builder_HappyPath()
    {
        var opts = new ReceiptOptions.Builder("+1", "+2", [10L, 20L])
            .WithType(ReceiptType.Viewed)
            .Build();

        Assert.Equal("+2", opts.Recipient); // singular per §F7
        Assert.Equal(new[] { 10L, 20L }, opts.TargetTimestamps);
        Assert.Equal(ReceiptType.Viewed, opts.Type);
    }

    [Fact]
    public void ReceiptOptions_DefaultType_IsRead()
    {
        var opts = new ReceiptOptions.Builder("+1", "+2", [1L]).Build();
        Assert.Equal(ReceiptType.Read, opts.Type);
    }

    [Fact]
    public void ReceiptOptions_Builder_EmptyTimestamps_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ReceiptOptions.Builder("+1", "+2", []));
    }

    [Fact]
    public void ReceiptOptions_Builder_NullTimestamps_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ReceiptOptions.Builder("+1", "+2", null!));
    }

    [Fact]
    public void ReceiptOptions_Builder_EmptyRecipient_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ReceiptOptions.Builder("+1", "", [1L]));
    }

    // ---- TypingOptions ----

    [Fact]
    public void TypingOptions_Builder_DefaultIsStart()
    {
        var opts = new TypingOptions.Builder("+1").WithRecipients(["+2"]).Build();
        Assert.False(opts.Stop);
    }

    [Fact]
    public void TypingOptions_AsStop_SetsStopFlag()
    {
        var opts = new TypingOptions.Builder("+1").WithRecipients(["+2"]).AsStop().Build();
        Assert.True(opts.Stop);
    }

    [Fact]
    public void TypingOptions_Build_WithoutRecipientOrGroup_Throws()
    {
        var b = new TypingOptions.Builder("+1");
        Assert.Throws<InvalidOperationException>(() => b.Build());
    }

    [Fact]
    public void TypingOptions_GroupOnly_Sufficient()
    {
        var opts = new TypingOptions.Builder("+1").WithGroupIds(["g=="]).Build();
        Assert.Equal(["g=="], opts.GroupIds);
    }

    // ---- RemoteDeleteOptions ----

    [Fact]
    public void RemoteDeleteOptions_Builder_HappyPath()
    {
        var opts = new RemoteDeleteOptions.Builder("+1", 555L)
            .WithRecipients(["+2"])
            .WithGroupIds(["g=="])
            .WithUsernames(["alice.42"])
            .WithNoteToSelf()
            .Build();

        Assert.Equal(555L, opts.TargetTimestamp);
        Assert.Equal(["+2"], opts.Recipients);
        Assert.Equal(["g=="], opts.GroupIds);
        Assert.Equal(["alice.42"], opts.Usernames);
        Assert.True(opts.NoteToSelf);
    }

    [Fact]
    public void RemoteDeleteOptions_Build_WithoutRecipientSource_Throws()
    {
        var b = new RemoteDeleteOptions.Builder("+1", 100L);
        Assert.Throws<InvalidOperationException>(() => b.Build());
    }

    [Fact]
    public void RemoteDeleteOptions_Builder_EmptyAccount_Throws()
    {
        Assert.Throws<ArgumentException>(() => new RemoteDeleteOptions.Builder("", 1L));
    }
}
