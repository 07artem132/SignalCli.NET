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
/// add-per-call-rpc-timeout: per-call таймаут-override у
/// <see cref="JsonRpcClient.InvokeMethodAsync"/>. Усі timeout-шляхи віртуалізовані через
/// <see cref="FakeTimeProvider"/> (rule #11 — нуль wall-clock-залежності), дзеркалячи
/// <see cref="TimeoutVirtualizationTests"/>.
/// </summary>
public sealed class PerCallTimeoutTests
{
    /// <summary>
    /// Будує <see cref="JsonRpcClient"/> з живою (але «мовчазною») stream-парою й FakeTimeProvider'ом.
    /// Output stream порожній — signal-cli ніколи не відповість, тож у гру вступає лише timeout-CTS.
    /// </summary>
    private static JsonRpcClient CreateSilentClient(int defaultTimeoutSeconds, FakeTimeProvider fakeTime)
    {
        var loggerMock = new Mock<ILogger<JsonRpcClient>>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var streamProviderMock = new Mock<IStreamPairProvider>();
        var streamPairSubject = new Subject<StreamPair?>();
        var pair = new StreamPair(
            new StreamWriter(new MemoryStream()),
            new StreamReader(new MemoryStream()),
            new StreamReader(new MemoryStream()));
        streamProviderMock.Setup(p => p.CurrentStreamPair).Returns(pair);
        streamProviderMock.Setup(p => p.StreamPairChanged).Returns(streamPairSubject);

        var options = new SignalCliOptions
        {
            AppHome = Path.GetTempPath(),
            JavaExecutable = string.Empty,
            LibDirectory = string.Empty,
            RequestTimeoutSeconds = defaultTimeoutSeconds
        };

        return new JsonRpcClient(loggerMock.Object, streamProviderMock.Object, options, fakeTime);
    }

    [Fact]
    public async Task InvokeMethodAsync_PerCallTimeoutShorterThanDefault_TimesOutAtPerCallValue()
    {
        // default 60 с; per-call 5 с — таймаут має спрацювати на 5 с, задовго до default'у.
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var client = CreateSilentClient(defaultTimeoutSeconds: 60, fakeTime);

        var invokeTask = client.InvokeMethodAsync(
            "per-call-short",
            new TestProbeRequest(),
            TestSerializationContext.Default.TestProbeRequest,
            TestSerializationContext.Default.TestProbeResponse,
            cancellationToken: default,
            timeout: TimeSpan.FromSeconds(5));

        await Task.Yield();

        // До 5 с — запит ще живий (і це задовго до default'у 60 с).
        fakeTime.Advance(TimeSpan.FromSeconds(4));
        Assert.False(invokeTask.IsCompleted);

        // Повз 5 с (усе ще < 60 с default) → TimeoutException саме на per-call значенні.
        fakeTime.Advance(TimeSpan.FromSeconds(2));

        var ex = await Assert.ThrowsAsync<TimeoutException>(async () =>
            await invokeTask.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains("per-call-short", ex.Message);
        // Повідомлення несе фактично застосований (per-call) таймаут, не глобальний default.
        Assert.Contains("5 с", ex.Message);
    }

    [Fact]
    public async Task InvokeMethodAsync_PerCallTimeoutLongerThanDefault_DefaultDoesNotFireEarly()
    {
        // default 30 с; per-call 130 с — default НЕ повинен спрацювати раніше.
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var client = CreateSilentClient(defaultTimeoutSeconds: 30, fakeTime);

        var invokeTask = client.InvokeMethodAsync(
            "per-call-long",
            new TestProbeRequest(),
            TestSerializationContext.Default.TestProbeRequest,
            TestSerializationContext.Default.TestProbeResponse,
            cancellationToken: default,
            timeout: TimeSpan.FromSeconds(130));

        await Task.Yield();

        // Провертаємо повз default (30 с) — запит МУСИТЬ лишитись живим, бо діє per-call 130 с.
        fakeTime.Advance(TimeSpan.FromSeconds(40));
        await Task.Yield();
        Assert.False(invokeTask.IsCompleted);

        // Провертаємо повз 130 с сумарно (40 + 100 = 140 с) → аж тепер TimeoutException.
        fakeTime.Advance(TimeSpan.FromSeconds(100));

        var ex = await Assert.ThrowsAsync<TimeoutException>(async () =>
            await invokeTask.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains("130 с", ex.Message);
    }

    [Fact]
    public async Task InvokeMethodAsync_NullTimeout_UsesClientDefault()
    {
        // timeout: null → поведінка = client default (10 с).
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var client = CreateSilentClient(defaultTimeoutSeconds: 10, fakeTime);

        var invokeTask = client.InvokeMethodAsync(
            "null-default",
            new TestProbeRequest(),
            TestSerializationContext.Default.TestProbeRequest,
            TestSerializationContext.Default.TestProbeResponse,
            cancellationToken: default,
            timeout: null);

        await Task.Yield();

        // До default'у — живий.
        fakeTime.Advance(TimeSpan.FromSeconds(9));
        Assert.False(invokeTask.IsCompleted);

        // Повз 10 с default → TimeoutException на клієнтському default'і.
        fakeTime.Advance(TimeSpan.FromSeconds(2));

        var ex = await Assert.ThrowsAsync<TimeoutException>(async () =>
            await invokeTask.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains("10 с", ex.Message);
    }

    [Fact]
    public async Task InvokeMethodAsync_NegativeTimeout_ThrowsArgumentOutOfRange()
    {
        // Валідація на межі: від'ємний per-call таймаут — програмна помилка виклику.
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var client = CreateSilentClient(defaultTimeoutSeconds: 30, fakeTime);

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.InvokeMethodAsync(
                "negative",
                new TestProbeRequest(),
                TestSerializationContext.Default.TestProbeRequest,
                TestSerializationContext.Default.TestProbeResponse,
                cancellationToken: default,
                timeout: TimeSpan.FromSeconds(-1)));
        Assert.Equal("timeout", ex.ParamName);
    }
}
