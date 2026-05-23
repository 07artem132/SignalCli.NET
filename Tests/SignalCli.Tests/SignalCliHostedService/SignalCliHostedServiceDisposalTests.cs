using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.SignalCli;

namespace SignalCli.Tests.SignalCliHostedService;

[Trait("Category", "HostedService")]
[Collection("HostedService")]
public class SignalCliHostedServiceDisposalTests : SignalCliHostedServiceTestsBase
{
    [Fact]
    [Trait("Category", "Disposal")]
    public async Task Dispose_WhenRunningProcess_ShouldKillProcessAndCleanupResources()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        var process = GetPrivateField<IProcess>(service, "_currentProcess");
        var processMock = Mock.Get(process!);
        
        var streamPair = service.CurrentStreamPair;
        var memStream = (MemoryStream)streamPair!.StandardInput.BaseStream;

        // Act
        service.Dispose();

        // Assert
        // Перевіряємо, що процес було вбито
        processMock.Verify(p => p.Kill(true), Times.Once);

        // Перевіряємо, що процес і потоки занулено
        Assert.Null(GetPrivateField<IProcess>(service, "_currentProcess"));
        Assert.Null(GetPrivateField<StreamPair>(service, "_currentStreamPair"));

        // Перевіряємо, що потоки закрито
        Assert.Throws<ObjectDisposedException>(() => memStream.Position);

        // Перевіряємо логування
        VerifyLog(LogLevel.Information, "Disposing SignalCliHostedService...");
    }

    [Fact]
    [Trait("Category", "Disposal")]
    public async Task Dispose_WhenCalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        var process = GetPrivateField<IProcess>(service, "_currentProcess");
        var processMock = Mock.Get(process!);

        // Act
        service.Dispose();
        service.Dispose(); // Повторний виклик

        // Assert
        processMock.Verify(p => p.Kill(true), Times.Once);
        LoggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Disposing SignalCliHostedService...")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Disposal")]
    public async Task StartAsync_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var service = CreateService();
        service.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => service.StartAsync(CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "Disposal")]
    public async Task StopAsync_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var service = CreateService();
        service.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => service.StopAsync(CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "Disposal")]
    public async Task WaitForReadyAsync_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var service = CreateService();
        service.Dispose();

        // Act & Assert
      await  Assert.ThrowsAsync<ObjectDisposedException>(
            () => service.WaitForReadyAsync());
    }

    [Fact]
    [Trait("Category", "Disposal")]
    public async Task Dispose_ShouldCompleteStreamPairChangedObservable()
    {
        // Arrange
        var service = CreateService();
        var completed = false;
        using var subscription = service.StreamPairChanged
            .Subscribe(
                onNext: _ => { },
                onCompleted: () => completed = true);

        // Act
        service.Dispose();

        // Assert
        Assert.True(completed);
    }

    [Fact]
    [Trait("Category", "Disposal")]
    public async Task Dispose_WhenProcessIsNull_ShouldNotThrow()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        var exception = Record.Exception(() => service.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    [Trait("Category", "Disposal")]
    public async Task Dispose_WhenProcessKillFails_ShouldStillCleanupAndLog()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        var process = GetPrivateField<IProcess>(service, "_currentProcess");
        var processMock = Mock.Get(process!);
        
        processMock.Setup(p => p.Kill(It.IsAny<bool>()))
            .Throws(new InvalidOperationException("Kill failed"));

        // Act
        service.Dispose();

        // Assert
        Assert.Null(GetPrivateField<IProcess>(service, "_currentProcess"));
        Assert.Null(GetPrivateField<StreamPair>(service, "_currentStreamPair"));
        
        VerifyLog(LogLevel.Error, "Помилка при disposing SignalCliHostedService");
    }

    [Fact]
    [Trait("Category", "Disposal")]
    public async Task Dispose_DuringStartAsync_ShouldPreventProcessStart()
    {
        // Arrange
        var startTaskCompletionSource = new TaskCompletionSource<(IProcess, StreamPair)>();
    
        ProcessRunnerMock
            .Setup(r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()))
            // post-modernize-tuning §8a.4: IProcessRunner повертає ValueTask;
            // обгортаємо існуючий TCS-Task через `new ValueTask<T>(task)`.
            .Returns(new ValueTask<(IProcess, StreamPair)>(startTaskCompletionSource.Task));

        var service = CreateService();
        var startTask = service.StartAsync(CancellationToken.None);

        // Act
        service.Dispose();
    
        // Завершаем операцию старта процесса
        startTaskCompletionSource.SetException(
            new InvalidOperationException("Process start was interrupted"));

        // Assert
        // Після уніфікації помилка старту проростає як є (InvalidOperationException),
        // а не маскується ObjectDisposedException від OnNext на утилізованому Subject.
        await Assert.ThrowsAsync<InvalidOperationException>(() => startTask);
        Assert.Null(GetPrivateField<IProcess>(service, "_currentProcess"));
        Assert.Equal(ProcessState.Failed, StateManager.CurrentState);
    }

    [Fact]
    [Trait("Category", "Disposal")]
    public async Task Dispose_ShouldFailAllPendingWaitForReadyTasks()
    {
        // Arrange
        var service = CreateService();
    
        // Запускаємо WaitForReadyAsync, але не чекаємо їх завершення
        var readyTask1 = service.WaitForReadyAsync(CancellationToken.None);
        var readyTask2 = service.WaitForReadyAsync(CancellationToken.None);

        // Act
        service.Dispose();

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => readyTask1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => readyTask2);
    }

}