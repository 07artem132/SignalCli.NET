using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.Signal.Message;
using SignalCli.Services.FileSystem;
using SignalCli.Services.Signal;

namespace SignalCli.Tests;

public class SignalMessageValidationTests
{
    private static (SignalMessage sut, Func<SendMessageFullParameters?> captured) CreateSut()
    {
        SendMessageFullParameters? captured = null;
        var client = new Mock<ISignalCliClient>();
        client.Setup(c => c.InvokeMethodAsync<SendMessageResponse, SendMessageFullParameters>(
                "send", It.IsAny<SendMessageFullParameters>(), It.IsAny<CancellationToken>()))
            .Callback<string, SendMessageFullParameters, CancellationToken>((_, p, _) => captured = p)
            .ReturnsAsync(new SendMessageResponse(null, 1));
        return (new SignalMessage(client.Object, Mock.Of<ILogger<SignalMessage>>()), () => captured);
    }

    // ---- Builder validation (edge cases) ----
    [Theory]
    [InlineData("", "+1", "msg")]
    [InlineData("+1", null, "msg")]
    [InlineData("+1", "+1", "")]
    public void TextMessageBuilder_InvalidArgs_Throws(string account, string? recipient, string message)
    {
        var recipients = recipient is null ? new List<IRecipient>() : [new UserRecipient(recipient)];
        Assert.Throws<ArgumentException>(() =>
            new TextMessageOptions.Builder(account, recipients, message).Build());
    }

    // ---- More than one group recipient is rejected ----
    [Fact]
    public async Task SendText_WithTwoGroupRecipients_Throws()
    {
        var (sut, _) = CreateSut();
        var options = new TextMessageOptions.Builder(
            "+1",
            [new GroupRecipient("g1"), new GroupRecipient("g2")],
            "hi").Build();

        await Assert.ThrowsAsync<ArgumentException>(() => sut.SendTextMessageAsync(options));
    }

    // ---- Small attachment -> inline data URI ----
    [Fact]
    public async Task SendAttachment_Small_UsesInlineDataUri()
    {
        var (sut, captured) = CreateSut();
        var entry = new AttachmentEntry("a.txt", Encoding.UTF8.GetBytes("hello"));
        var options = new AttachmentMessageOptions.Builder(
            "+1", [new UserRecipient("+2")], [entry]).Build();

        await sut.SendAttachmentAsync(options);

        var att = captured()!.Attachments!.ToList();
        Assert.Single(att);
        Assert.StartsWith("data:", att[0]);
    }

    // ---- Sticker ----
    [Fact]
    public async Task SendSticker_SetsStickerParameter()
    {
        var (sut, captured) = CreateSut();
        var options = new StickerMessageOptions.Builder(
            "+1", [new UserRecipient("+2")], "pack:3").Build();

        await sut.SendStickerAsync(options);

        Assert.Equal("pack:3", captured()!.Sticker);
    }

    // ---- Styled text -> parsed message + text style ranges ----
    [Fact]
    public async Task SendText_WithStyle_ParsesMarkersIntoRanges()
    {
        var (sut, captured) = CreateSut();
        var options = new TextMessageOptions.Builder("+1", [new UserRecipient("+2")], "*hi*")
            .UseStyle().Build();

        await sut.SendTextMessageAsync(options);

        var p = captured()!;
        Assert.Equal("hi", p.Message);
        Assert.NotNull(p.TextStyle);
        Assert.Contains("0:2:ITALIC", p.TextStyle!);
    }
}
