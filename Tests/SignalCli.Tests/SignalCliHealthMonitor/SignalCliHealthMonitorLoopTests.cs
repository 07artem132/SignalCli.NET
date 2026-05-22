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
        // Arrange
        var service = CreateHostedService();
        await service.StartAsync(CancellationToken.None);
        Assert.Equal(ProcessState.Running, StateManager.CurrentState);

        var pingCount = 0;
        var streamPairs = new List<StreamPair?>();
        using var subscription = service.StreamPairChanged
            .Subscribe(sp => streamPairs.Add(sp));

        JsonRpcClientMock
            .Setup(c => c.InvokeMethodAsync<VersionResponse, VersionParameters>(
                It.IsAny<string>(),
                It.IsAny<VersionParameters>(),
                It.IsAny<CancellationToken>()
            ))
            .Callback(() => pingCount++)
            .ReturnsAsync(new VersionResponse(""));

        var monitor = CreateMonitor(service);

        // Act
        await monitor.StartAsync(CancellationToken.None);
        
        // Ждём до тех пор пока не получим два StreamPair или не истечет таймаут
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < timeout && streamPairs.Count(sp => sp != null) < 2)
        {
            await Task.Delay(100);
        }

        // Assert
        Assert.True(pingCount > 0, "PingCliAsync должен быть вызван");
        VerifyWarningLog("Signal CLI не відповідає", Times.AtLeastOnce());

        var nonNullCount = streamPairs.Count(sp => sp != null);
        await monitor.StopAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
        
        Assert.True(
            nonNullCount >= 2,
            $"Ожидалось как минимум 2 не-null StreamPair, получено {nonNullCount}"
        );
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

        var pingCount = 0;
        JsonRpcClientMock
            .Setup(c => c.InvokeMethodAsync<VersionResponse, VersionParameters>(
                It.IsAny<string>(),
                It.IsAny<VersionParameters>(),
                It.IsAny<CancellationToken>()
            ))
            .Callback(() => pingCount++)
            .ThrowsAsync(new InvalidOperationException("RPC error"));

        var monitor = CreateMonitor(service);

        // Act
        await monitor.StartAsync(CancellationToken.None);
        
        // Ждём первого пинга и ошибки
        await Task.Delay(500);
        
        // Assert
        Assert.Equal(1, pingCount);
        VerifyWarningLog("Signal CLI не відповідає", Times.AtLeastOnce());
        VerifyErrorLog("PingCliAsync: пінг CLI невдалий", Times.AtLeastOnce());

        await monitor.StopAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task MonitorLoop_WhenNoStreamPair_ShouldTriggerRestart()
    {
        // Arrange
        var service = CreateHostedService();

        // Сымитируем ситуацию с null StreamPair, не останавливая сервис
        ProcessRunnerMock
            .Setup(r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => 
            {
                var pMock = new Mock<IProcess>();
                return (pMock.Object, null as StreamPair);
            });
        await service.StartAsync(CancellationToken.None);
        var monitor = CreateMonitor(service);

        // Act
        await monitor.StartAsync(CancellationToken.None);
        await Task.Delay(500);

        // Assert
        VerifyDebugLog("PingCliAsync: немає поточного StreamPair => CLI не готовий",Times.AtLeastOnce());
        VerifyWarningLog("Signal CLI не відповідає", Times.AtLeastOnce());

        await monitor.StopAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task MonitorLoop_ShouldRespectHealthCheckInterval()
    {
        // Arrange
        var pingTimes = new List<DateTime>();
        JsonRpcClientMock
            .Setup(c => c.InvokeMethodAsync<VersionResponse, VersionParameters>(
                It.IsAny<string>(),
                It.IsAny<VersionParameters>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => pingTimes.Add(DateTime.UtcNow))
            .ReturnsAsync(new VersionResponse("test-version"));

        ServiceConfig.HealthCheckIntervalSeconds = 1;
        
        var hostedService = CreateHostedService();
        await hostedService.StartAsync(CancellationToken.None);
        var monitor = CreateMonitor(hostedService);

        // Act
        await monitor.StartAsync(CancellationToken.None);
        await Task.Delay(3500); // Ждём ~3 пинга
        await monitor.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(pingTimes.Count >= 3, "Should have pinged at least 3 times");
        
        for (int i = 1; i < pingTimes.Count; i++)
        {
            var interval = pingTimes[i] - pingTimes[i-1];
            Assert.True(
                interval.TotalSeconds >= 0.9 && interval.TotalSeconds <= 1.1,
                $"Ping interval should be ~1 second, was {interval.TotalSeconds}"
            );
        }

        await hostedService.StopAsync(CancellationToken.None);
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