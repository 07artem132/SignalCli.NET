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
            .Setup(c => c.InvokeMethodAsync<SendMessageFullParameters, SendMessageResponse>(It.IsAny<string>(), It.IsAny<SendMessageFullParameters>(), It.IsAny<JsonTypeInfo<SendMessageFullParameters>>(), It.IsAny<JsonTypeInfo<SendMessageResponse>>(), It.IsAny<CancellationToken>()))
            .Callback<string, SendMessageFullParameters, JsonTypeInfo<SendMessageFullParameters>, JsonTypeInfo<SendMessageResponse>, CancellationToken, TimeSpan?>(
                (_, p, _, _, _, _) => passedAttachmentPath = p.Attachments?.FirstOrDefault())
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

    /// <summary>
    /// post-modernize-tuning §8c.9 (audit C9): stateful <see cref="IEnumerable{T}"/>-recipient'и
    /// (зчитують файл, ітерують DB) мають бути enumerated РІВНО ОДИН раз. Регресія до
    /// "validate + 2× Where" дала б 3 ітерації — для side-effect'ивних джерел це баг.
    /// </summary>
    [Fact]
    public async Task SendTextMessageAsync_StatefulEnumerableRecipients_AreEnumeratedExactlyOnce()
    {
        // Підрахунковий лічильник: кожен виклик GetEnumerator() => +1.
        var enumerationCount = 0;
        IEnumerable<IRecipient> CountingRecipients()
        {
            enumerationCount++;
            yield return new UserRecipient("+380501234568");
            yield return new UserRecipient("+380501234569");
        }

        var signalCli = new Mock<ISignalCliClient>();
        signalCli
            .Setup(c => c.InvokeMethodAsync<SendMessageFullParameters, SendMessageResponse>(It.IsAny<string>(), It.IsAny<SendMessageFullParameters>(), It.IsAny<JsonTypeInfo<SendMessageFullParameters>>(), It.IsAny<JsonTypeInfo<SendMessageResponse>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageResponse(Results: [], TimeStamp: 1));

        var sut = new SignalMessage(signalCli.Object, Mock.Of<ILogger<SignalMessage>>());

        // Builder приймає List<IRecipient> (eagerly materialized). Щоб протестувати
        // single-pass-контракт на самому SendUnifiedMessageAsync — підмінюємо приватне
        // Recipients-property через reflection. Це інтенаціональний test-only shortcut:
        // production-shлях через Builder завжди приходить вже з матеріалізованим списком.
        var options = new TextMessageOptions.Builder(
            account: "+380501234567",
            recipients: [new UserRecipient("+380501234568")],
            message: "test").Build();
        var recipientsProp = typeof(TextMessageOptions).GetProperty("Recipients")!;
        recipientsProp.SetValue(options, CountingRecipients());

        // Act
        await sut.SendTextMessageAsync(options);

        // Assert: GetEnumerator виклик ОДИН раз (single-pass materialization).
        Assert.Equal(1, enumerationCount);
    }
}
