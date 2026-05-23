using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.Signal.Message;
using SignalCli.Services.FileSystem;
using SignalCli.Services.Signal;

namespace SignalCli.Tests;

public class SignalMessageTests
{
    [Fact]
    public async Task SendAttachmentAsync_WhenSendFails_DeletesTempFiles()
    {
        // Arrange: signal-cli клієнт фіксує переданий шлях вкладення і кидає помилку на "send"
        string? passedAttachmentPath = null;
        var signalCli = new Mock<ISignalCliClient>();
        signalCli
            .Setup(c => c.InvokeMethodAsync<SendMessageFullParameters, SendMessageResponse>(
                It.IsAny<string>(), It.IsAny<SendMessageFullParameters>(), It.IsAny<CancellationToken>()))
            .Callback<string, SendMessageFullParameters, CancellationToken>(
                (_, p, _) => passedAttachmentPath = p.Attachments?.FirstOrDefault())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var sut = new SignalMessage(signalCli.Object, Mock.Of<ILogger<SignalMessage>>());

        // 12 МБ -> ~16 МБ після base64 (> поріг 15 МБ) -> шлях через temp-файл
        var entry = new AttachmentEntry($"big-{Guid.NewGuid():N}.bin", new byte[12_000_000]);
        var options = new AttachmentMessageOptions.Builder(
            account: "+380501234567",
            recipients: [new UserRecipient("+380501234568")],
            attachments: [entry]).Build();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SendAttachmentAsync(options));

        // Assert: великий обсяг пішов через temp-файл (шлях, а не data-URI)...
        Assert.NotNull(passedAttachmentPath);
        Assert.DoesNotContain("data:", passedAttachmentPath!);
        // ...і цей temp-файл прибрано у finally навіть при збої відправки.
        Assert.False(File.Exists(passedAttachmentPath));
        Assert.True(string.IsNullOrEmpty(entry.FilePath));
    }
}
