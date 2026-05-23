using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.SignalCli;

namespace SignalCli.Tests.SignalCliHealthMonitor;

public class SignalCliHealthMonitorLoopTests : SignalCliHealthMonitorTestBase
{
    [Fact]
    public async Task MonitorLoop_WhenPingFails_ShouldForceRestart()
    {
        // G.14: TaskCompletionSource замість Stopwatch-вікон + ранній stop,
        // щоб loop із HealthCheckIntervalSeconds=0 не намолочував тисячі ітерацій
        // (OOM-ризик у тест-хості).
        var service = CreateHostedService();
        await service.StartAsync(CancellationToken.None);
        Assert.Equal(ProcessState.Running, StateManager.CurrentState);

        var streamPairs = new List<StreamPair?>();
        var secondPairSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = service.StreamPairChanged
            .Subscribe(sp =>
            {
                lock (streamPairs)
                {
                    streamPairs.Add(sp);
                    if (streamPairs.Count(s => s != null) >= 2)
                        secondPairSeen.TrySetResult(true);
                }
            });

        JsonRpcClientMock
            .Setup(c => c.InvokeMethodAsync<VersionParameters, VersionResponse>(
                It.IsAny<string>(),
                It.IsAny<VersionParameters>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(new VersionResponse(""));

        var monitor = CreateMonitor(service);

        await monitor.StartAsync(CancellationToken.None);
        await secondPairSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await monitor.StopAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        VerifyWarningLog("Signal CLI не відповідає", Times.AtLeastOnce());
        int nonNullCount;
        lock (streamPairs) { nonNullCount = streamPairs.Count(s => s != null); }
        Assert.True(nonNullCount >= 2,
            $"Очікувалося ≥ 2 не-null StreamPair, отримано {nonNullCount}");
    }


    [Fact]
    public async Task MonitorLoop_WhenPingSucceeds_ShouldNotRestart()
    {
        // Arrange
        var service = CreateHostedService();
        await service.StartAsync(CancellationToken.None);
        
        var streamPairs = new List<StreamPair?>();
        using var subscription = service.StreamPairChanged
            .Subscribe(sp => streamPairs.Add(sp));

        var monitor = CreateMonitor(service);

        // Act
        await monitor.StartAsync(CancellationToken.None);
        await Task.Delay(1000);
        await monitor.StopAsync(CancellationToken.None);

        // Assert
        LoggerMonitorMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Signal CLI not responding")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Never);

        var nonNullCount = streamPairs.Count(sp => sp != null);
        Assert.True(nonNullCount <= 1, "No additional StreamPairs should be published");
    }

    [Fact]
    public async Task MonitorLoop_WhenPingThrows_ShouldTriggerRestart()
    {
        // Arrange
        var service = CreateHostedService();
        await service.StartAsync(CancellationToken.None);

        // G.14: знімаємо тіньове очікування «рівно 1 пінг» — TestBase задає
        // HealthCheckIntervalSeconds=0, тож монітор може встигнути виконати >1 ітерації
        // за 500мс. Достатньо переконатися, що пінг бодай раз спрацював і викликаний
        // ForceRestartAsync відмалював відповідні логи. ITC-stop сигналом — фіксація
        // самого факту через TaskCompletionSource (без Stopwatch-вікон і race-у).
        var pingObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pingCount = 0;
        JsonRpcClientMock
            .Setup(c => c.InvokeMethodAsync<VersionParameters, VersionResponse>(
                It.IsAny<string>(),
                It.IsAny<VersionParameters>(),
                It.IsAny<CancellationToken>()
            ))
            .Callback(() =>
            {
                Interlocked.Increment(ref pingCount);
                pingObserved.TrySetResult(true);
            })
            .ThrowsAsync(new InvalidOperationException("RPC error"));

        var monitor = CreateMonitor(service);

        // Act
        await monitor.StartAsync(CancellationToken.None);

        // Чекаємо подію (а не довільний sleep) із розумним fallback-таймаутом.
        await pingObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Зупиняємо монітор ВІДРАЗУ, поки лічильник не накопичує тисячі ітерацій (OOM-ризик).
        await monitor.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(pingCount >= 1, $"Expected at least one ping, got {pingCount}");
        VerifyWarningLog("Signal CLI не відповідає", Times.AtLeastOnce());
        VerifyErrorLog("PingCliAsync: пінг CLI невдалий", Times.AtLeastOnce());

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task MonitorLoop_WhenNoStreamPair_ShouldTriggerRestart()
    {
        // G.14: ловимо подію через TaskCompletionSource у самому логері, щоб не лишати
        // монітор крутитися 500мс і не накопичувати алокації.
        var service = CreateHostedService();

        ProcessRunnerMock
            .Setup(r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var pMock = new Mock<IProcess>();
                return (pMock.Object, null as StreamPair);
            });
        await service.StartAsync(CancellationToken.None);

        var debugSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        LoggerMonitorMock
            .Setup(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("немає поточного StreamPair")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()))
            .Callback(() => debugSeen.TrySetResult(true));

        var monitor = CreateMonitor(service);

        await monitor.StartAsync(CancellationToken.None);
        await debugSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await monitor.StopAsync(CancellationToken.None);

        VerifyDebugLog("PingCliAsync: немає поточного StreamPair => CLI не готовий", Times.AtLeastOnce());
        VerifyWarningLog("Signal CLI не відповідає", Times.AtLeastOnce());

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task MonitorLoop_ShouldRespectHealthCheckInterval()
    {
        // G.14: тест працює у віртуальному часі (FakeTimeProvider). Кожен Advance(1с)
        // має вивільнити рівно один Task.Delay у MonitorLoop і спричинити один пінг.
        // Реальний DateTime.UtcNow та Task.Delay(3500) видалено — flake неможливий.
        var pingTimes = new List<DateTimeOffset>();
        var pingFired = new SemaphoreSlim(0);
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow);

        JsonRpcClientMock
            .Setup(c => c.InvokeMethodAsync<VersionParameters, VersionResponse>(
                It.IsAny<string>(),
                It.IsAny<VersionParameters>(),
                It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                lock (pingTimes) { pingTimes.Add(fakeTime.GetUtcNow()); }
                pingFired.Release();
            })
            .ReturnsAsync(new VersionResponse("test-version"));

        ServiceConfig.HealthCheckIntervalSeconds = 1;

        var hostedService = CreateHostedService();
        await hostedService.StartAsync(CancellationToken.None);
        var monitor = CreateMonitor(hostedService, fakeTime);

        try
        {
            await monitor.StartAsync(CancellationToken.None);

            // Тричі провертаємо віртуальний годинник на 1с і чекаємо на сигнал від пінгу.
            // Маленька реальна пауза перед Advance — щоб MonitorLoop встиг ДОЙТИ до
            // await Task.Delay(_timeProvider) і зареєструвати таймер на віртуальному
            // годиннику; інакше Advance може спрацювати в порожнечу.
            for (var i = 0; i < 3; i++)
            {
                await Task.Delay(50);
                fakeTime.Advance(TimeSpan.FromSeconds(1));
                Assert.True(await pingFired.WaitAsync(TimeSpan.FromSeconds(5)),
                    $"Очікувався пінг #{i + 1} після Advance(1c) у віртуальному часі");
            }

            await monitor.StopAsync(CancellationToken.None);

            List<DateTimeOffset> snapshot;
            lock (pingTimes) { snapshot = new List<DateTimeOffset>(pingTimes); }

            Assert.True(snapshot.Count >= 3, $"Мало бути ≥ 3 пінгів, отримано {snapshot.Count}");
            for (var i = 1; i < snapshot.Count; i++)
            {
                var interval = snapshot[i] - snapshot[i - 1];
                // У віртуальному часі інтервал точно 1с (без flake-вікон).
                Assert.Equal(1.0, interval.TotalSeconds);
            }
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }
    }

    private void VerifyDebugLog(string messageContains, Times? times = null)
    {
        times ??= Times.Once();

        LoggerMonitorMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(messageContains)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            times.Value);
    }
}