using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.SignalCli;

namespace SignalCli.Tests.SignalCliHostedService;

[Trait("Category", "HostedService")]
[Collection("HostedService")]
public class SignalCliHostedServiceLifecycleTests : SignalCliHostedServiceTestsBase
{
    [Fact]
    [Trait("Category", "ProcessLifecycle")]
    public async Task StartAsync_WhenNotStarted_ShouldStartProcessAndSetStateToRunning()
    {
        // Arrange
        var service = CreateService();
        Assert.Equal(ProcessState.NotStarted, StateManager.CurrentState);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        ProcessRunnerMock.Verify(r =>
            r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Equal(ProcessState.Running, StateManager.CurrentState);
        Assert.NotNull(service.CurrentStreamPair);
        Assert.NotNull(GetPrivateField<IProcess>(service, "_currentProcess"));

        // Перевіряємо логи
        VerifyLog(LogLevel.Information, "SignalCliHostedService запускається...");
        VerifyLog(LogLevel.Information, "SignalCliHostedService успішно запущено.");
    }

    [Fact]
    [Trait("Category", "ProcessLifecycle")]
    public async Task StartAsync_WhenAlreadyRunning_ShouldNotStartNewProcess()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        var initialProcessId = GetPrivateField<IProcess>(service, "_currentProcess")?.Id;
        ProcessStartCallCount = 0;

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, ProcessStartCallCount);
        Assert.Equal(ProcessState.Running, StateManager.CurrentState);
        var currentProcessId = GetPrivateField<IProcess>(service, "_currentProcess")?.Id;
        Assert.Equal(initialProcessId, currentProcessId);
    }

    [Fact]
    [Trait("Category", "ProcessLifecycle")]
    public async Task StartAsync_WhenProcessStartFails_ShouldSetFailedStateAndThrow()
    {
        // Arrange
        var service = CreateService();
        var expectedException = new InvalidOperationException("Failed to start process");
        ProcessRunnerMock
            .Setup(r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(CancellationToken.None));
        
        Assert.Equal(ProcessState.Failed, StateManager.CurrentState);
        Assert.Same(expectedException, ex);
        VerifyLog(LogLevel.Error, "Помилка запуску SignalCliHostedService");
    }

    [Fact]
    [Trait("Category", "ProcessLifecycle")]
    public async Task StopAsync_WhenRunning_ShouldStopProcessAndSetStateToStopped()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        var process = GetPrivateField<IProcess>(service, "_currentProcess");
        var processMock = Mock.Get(process!);
        var streamPair = service.CurrentStreamPair;
    
        // Отримуємо базовий потік зі StreamWriter для подальшої перевірки
        var memStream = (MemoryStream)streamPair!.StandardInput.BaseStream;

        // Act
        await service.StopAsync(CancellationToken.None);

        // MemoryStream.ToArray() працює навіть після закриття потоку — детермінована
        // перевірка без гонки з утилізацією (на відміну від читання живого потоку).
        var streamContent = System.Text.Encoding.UTF8.GetString(memStream.ToArray());

        // Assert
        Assert.Equal(ProcessState.Stopped, StateManager.CurrentState);
        Assert.Null(GetPrivateField<IProcess>(service, "_currentProcess"));
        Assert.Null(GetPrivateField<StreamPair>(service, "_currentStreamPair"));
        Assert.Contains("exit", streamContent);
    
        VerifyLog(LogLevel.Information, "SignalCliHostedService зупинено.");
    }

    [Fact]
    [Trait("Category", "ProcessLifecycle")]
    public async Task StopAsync_WhenProcessHangs_ShouldKillProcess()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        var process = GetPrivateField<IProcess>(service, "_currentProcess");
        var processMock = Mock.Get(process!);
        processMock.Setup(p => p.HasExited).Returns(false);

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert
        processMock.Verify(p => p.Kill(true), Times.Once);
        Assert.Equal(ProcessState.Stopped, StateManager.CurrentState);
        // B.9: тепер чекаємо реального exit через WaitForExitAsync(timeout); якщо процес висить,
        // на таймаут пише Warning з "Процес не завершився за …c, примусово завершуємо його".
        VerifyLog(LogLevel.Warning, "Процес не завершився за");
    }

    [Fact]
    [Trait("Category", "ProcessLifecycle")]
    public async Task StopAsync_WhenNotRunning_ShouldDoNothing()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(ProcessState.NotStarted, StateManager.CurrentState);
    }

    [Fact]
    [Trait("Category", "ProcessLifecycle")]
    public async Task StopAsync_WhenCalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        
        // Act
        await service.StopAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(ProcessState.Stopped, StateManager.CurrentState);
        // Перевіряємо, що логи про зупинку з'явилися лише один раз
        LoggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SignalCliHostedService зупинено.")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "ProcessLifecycle")]
    public async Task StartAsync_WhenCancellationRequested_ShouldThrowOperationCanceled()
    {
        // Arrange
        var service = CreateService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.StartAsync(cts.Token));
    }

    [Fact]
    [Trait("Category", "ProcessLifecycle")]
    public async Task StopAsync_ShouldRespectCancellationToken()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.StopAsync(cts.Token));
    }
}