using Moq;
using SignalCli.Models.SignalCli;

namespace SignalCli.Tests.SignalCliHealthMonitor;

public class SignalCliHealthMonitorEdgeCaseTests : SignalCliHealthMonitorTestBase
{
    [Fact]
    public async Task Dispose_ShouldCleanupResourcesAndNotThrow()
    {
        // Arrange
        var service = CreateHostedService();
        await service.StartAsync(CancellationToken.None);
        var monitor = CreateMonitor(service);
        await monitor.StartAsync(CancellationToken.None);
        await Task.Delay(100);  // Даємо час на запуск

        // Act & Assert
        monitor.Dispose(); // Перший виклик
        monitor.Dispose(); // Повторний виклик не повинен кидати винятку

        await service.StopAsync(CancellationToken.None);
    }

     

    [Fact]
    public async Task PingCliAsync_WhenTimeout_ShouldReturnFalse()
    {
        // Arrange
        ServiceConfig.HealthCheckTimeoutSeconds = 1;
        var service = CreateHostedService();
        await service.StartAsync(CancellationToken.None);

        var pingCount = 0;
        JsonRpcClientMock
            .Setup(c => c.InvokeMethodAsync<VersionResponse, VersionParameters>(
                It.IsAny<string>(),
                It.IsAny<VersionParameters>(),
                It.IsAny<CancellationToken>()
            ))
            .Returns<string, VersionParameters, CancellationToken>(async (m, p, ct) => 
            {
                pingCount++;
                await Task.Delay(TimeSpan.FromSeconds(2), ct); // Використовуємо переданий токен
                return new VersionResponse("test");
            });

        var monitor = CreateMonitor(service);

        // Act
        await monitor.StartAsync(CancellationToken.None);
        
        // Чекаємо, доки не побачимо Warning у логах або не вичерпається таймаут
        var timeout = DateTime.UtcNow.AddSeconds(ServiceConfig.HealthCheckTimeoutSeconds*3);
        while (DateTime.UtcNow < timeout)
        {
            try
            {
                VerifyErrorLog("Signal CLI not responding", Times.AtLeastOnce());
                break;
            }
            catch
            {
                await Task.Delay(100);
            }
        }

        // Assert
        Assert.True(pingCount > 0, "Ping має бути викликаний хоча б раз");
        VerifyErrorLog("Signal CLI не відповідає", Times.AtLeastOnce());

        await monitor.StopAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task MonitorLoop_WhenUnexpectedError_ShouldLogAndContinue()
    {
        // Arrange
        var service = CreateHostedService();
        await service.StartAsync(CancellationToken.None);

        var errorThrown = false;
        JsonRpcClientMock
            .Setup(c => c.InvokeMethodAsync<VersionResponse, VersionParameters>(
                It.IsAny<string>(),
                It.IsAny<VersionParameters>(),
                It.IsAny<CancellationToken>()
            ))
            .Returns<string, VersionParameters, CancellationToken>((m, p, ct) => 
            {
                if (errorThrown) return Task.FromResult(new VersionResponse("test-version"));
                errorThrown = true;
                throw new Exception("Неочікувана помилка в циклі моніторингу");
            });

        var monitor = CreateMonitor(service);
        await monitor.StartAsync(CancellationToken.None);
        await Task.Delay(1500); // Даємо час на перший пінг

        // Assert
        VerifyErrorLog("PingCliAsync: пінг CLI невдалий", Times.Once());
        
        await monitor.StopAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }
}