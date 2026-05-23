using Microsoft.Extensions.Logging;
using Moq;

namespace SignalCli.Tests.SignalCliHealthMonitor;

public class SignalCliHealthMonitorStartStopTests : SignalCliHealthMonitorTestBase
{
    [Fact]
    public async Task StartAsync_ShouldStartMonitoring()
    {
        // Arrange
        var hostedService = CreateHostedService();
        var monitor = CreateMonitor(hostedService);

        // Act
        await monitor.StartAsync(CancellationToken.None);

        // Assert
        VerifyInfoLog("SignalCliHealthMonitor запускається...");
        VerifyInfoLog("SignalCliHealthMonitor запущено.");
        
        await Task.Delay(100); // Даємо час на запуск MonitorLoop
        await monitor.StopAsync(CancellationToken.None); // Чистимо ресурси
    }

    [Fact]
    public async Task StartAsync_WhenCancellationRequested_ShouldNotStartMonitoring()
    {
        // Arrange
        var hostedService = CreateHostedService();
        var monitor = CreateMonitor(hostedService);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => monitor.StartAsync(cts.Token)
        );

        // Перевіряємо, що моніторинг не запустився
        VerifyInfoLog("MonitorLoop started", Times.Never());
    }

    [Fact]
    public async Task StopAsync_ShouldStopMonitoring()
    {
        // Arrange
        var hostedService = CreateHostedService();
        var monitor = CreateMonitor(hostedService);
    
        // Додамо TaskCompletionSource для відстеження подій
        var monitoringStartedTcs = new TaskCompletionSource<bool>();
        var monitoringStoppedTcs = new TaskCompletionSource<bool>();

        // Налаштуємо мок-логер для сигналізації про події
        LoggerMonitorMock
            .Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Цикл моніторингу запущено")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()))
            .Callback(() => monitoringStartedTcs.TrySetResult(true));
        
        LoggerMonitorMock
            .Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Цикл моніторингу завершено")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()))
            .Callback(() => monitoringStoppedTcs.TrySetResult(true));
    
        // Act
        await monitor.StartAsync(CancellationToken.None);
    
        // Чекаємо на запуск моніторингу з таймаутом
        await monitoringStartedTcs.Task.WaitAsync(TimeSpan.FromSeconds(1));

        // Act — зупиняємо монітор
        await monitor.StopAsync(CancellationToken.None);

        // Чекаємо на завершення з таймаутом
        await monitoringStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
    
        // Assert
        VerifyInfoLog("SignalCliHealthMonitor зупиняється...");
        VerifyInfoLog("SignalCliHealthMonitor зупинено.");
        VerifyInfoLog("Цикл моніторингу завершено в SignalCliHealthMonitor");
    }
    
 
    [Fact]
    public async Task StopAsync_WhenNotStarted_ShouldNotThrow()
    {
        // Arrange
        var hostedService = CreateHostedService();
        var monitor = CreateMonitor(hostedService);

        // Act & Assert
        await monitor.StopAsync(CancellationToken.None);
        // Винятків бути не повинно
    }

    [Fact]
    public async Task StartStop_MultipleInvocations_ShouldBeIdempotent()
    {
        // Arrange
        var hostedService = CreateHostedService();
        var monitor = CreateMonitor(hostedService);

        // Act & Assert
        await monitor.StartAsync(CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => monitor.StartAsync(CancellationToken.None)
        );
        
        await monitor.StopAsync(CancellationToken.None);
        await monitor.StopAsync(CancellationToken.None);  // Повторний виклик

        VerifyInfoLog("SignalCliHealthMonitor запускається...", Times.Once());
        VerifyInfoLog("SignalCliHealthMonitor зупиняється...", Times.AtMost(2));
    }
}