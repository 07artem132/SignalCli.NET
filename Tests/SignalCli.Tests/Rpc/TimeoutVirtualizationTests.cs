using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using System.Reactive.Subjects;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models;
using SignalCli.Models.SignalCli;
using SignalCli.Services.Rpc;

namespace SignalCli.Tests.Rpc;

/// <summary>
/// post-modernize-tuning §1.8 (audit N4): timeout-шлях у <see cref="JsonRpcClient.InvokeMethodAsync"/>
/// віртуалізується через <see cref="FakeTimeProvider"/>. Підтверджує що
/// <c>new CancellationTokenSource(_requestTimeout, _timeProvider)</c>-overload справді
/// прокидає FakeTimeProvider.Advance як trigger для timeout-CTS.
/// </summary>
/// <remarks>
/// Раніше тести таймаута (<c>A3_InvokeMethodAsync_SilentProcess_FaultsWithTimeoutException</c>)
/// чекали реальний wall-clock-таймаут (1с + race-window). З §1.8 — нуль wall-clock-залежності.
/// </remarks>
public sealed class TimeoutVirtualizationTests
{
    [Fact]
    public async Task InvokeMethodAsync_TimeoutPath_VirtualizedByFakeTimeProvider_ThrowsTimeoutException()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<JsonRpcClient>>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var streamProviderMock = new Mock<IStreamPairProvider>();
        var streamPairSubject = new Subject<StreamPair?>();
        var inputWriter = new StreamWriter(new MemoryStream());
        var outputReader = new StreamReader(new MemoryStream());
        var errorReader = new StreamReader(new MemoryStream());
        var pair = new StreamPair(inputWriter, outputReader, errorReader);
        streamProviderMock.Setup(p => p.CurrentStreamPair).Returns(pair);
        streamProviderMock.Setup(p => p.StreamPairChanged).Returns(streamPairSubject);

        var config = new Config
        {
            AppHome = Path.GetTempPath(),
            JavaExecutable = string.Empty,
            LibDirectory = string.Empty,
            // Великий wall-clock-таймаут; справжній trigger — FakeTimeProvider.Advance.
            RequestTimeoutSeconds = 60
        };
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var client = new JsonRpcClient(
            loggerMock.Object,
            streamProviderMock.Object,
            config.ToOptions(),
            fakeTime);

        // Act: запит, який ніколи не отримає відповідь (signal-cli мовчить — output stream порожній).
        var invokeTask = client.InvokeMethodAsync(
            "silent",
            new TestProbeRequest(),
            TestSerializationContext.Default.TestProbeRequest,
            TestSerializationContext.Default.TestProbeResponse);

        // Даємо невеликий шанс SendRequestAsync дописати у stream і дойти до await-у TCS.
        // Це не race-вікно — навіть якщо ми Advance'немо раніше за await, FakeTime'овий timer
        // спрацює при наступному Yield.
        await Task.Yield();

        // Провертаємо віртуальний годинник за межі _requestTimeout — timeout-CTS має спрацювати.
        fakeTime.Advance(TimeSpan.FromSeconds(61));

        // Assert: маємо отримати TimeoutException БЕЗ wall-clock-очікування.
        // WaitAsync із real-time fallback 5с — bo якщо віртуалізація НЕ працює, тест зависне.
        var ex = await Assert.ThrowsAsync<TimeoutException>(async () =>
            await invokeTask.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains("silent", ex.Message);
    }

    [Fact]
    public async Task InvokeMethodAsync_CallerCancellation_DoesNotFalselyAttributeToTimeout()
    {
        // Sanity check: якщо callerCancel спрацював раніше за timeout — отримуємо
        // TaskCanceledException, НЕ TimeoutException. FakeTimeProvider підтверджує, що
        // ми не Advance'нули достатньо для timeout — caller-cancel виграв race.
        var loggerMock = new Mock<ILogger<JsonRpcClient>>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var streamProviderMock = new Mock<IStreamPairProvider>();
        var streamPairSubject = new Subject<StreamPair?>();
        var inputWriter = new StreamWriter(new MemoryStream());
        var outputReader = new StreamReader(new MemoryStream());
        var errorReader = new StreamReader(new MemoryStream());
        var pair = new StreamPair(inputWriter, outputReader, errorReader);
        streamProviderMock.Setup(p => p.CurrentStreamPair).Returns(pair);
        streamProviderMock.Setup(p => p.StreamPairChanged).Returns(streamPairSubject);

        var config = new Config
        {
            AppHome = Path.GetTempPath(),
            JavaExecutable = string.Empty,
            LibDirectory = string.Empty,
            RequestTimeoutSeconds = 60
        };
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var client = new JsonRpcClient(
            loggerMock.Object,
            streamProviderMock.Object,
            config.ToOptions(),
            fakeTime);

        using var cts = new CancellationTokenSource();
        var invokeTask = client.InvokeMethodAsync(
            "cancel-me",
            new TestProbeRequest(),
            TestSerializationContext.Default.TestProbeRequest,
            TestSerializationContext.Default.TestProbeResponse,
            cts.Token);

        await Task.Yield();
        cts.Cancel();
        // НЕ Advance — таймер ще не спрацював, тож має вилетіти TaskCanceledException.

        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await invokeTask.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
