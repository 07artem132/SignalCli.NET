using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.SignalCli;

namespace SignalCli.Tests.SignalCliHostedService;

[Trait("Category", "HostedService")]
[Collection("HostedService")]
public class SignalCliHostedServiceStreamPairTests : SignalCliHostedServiceTestsBase
{
    [Fact]
    [Trait("Category", "StreamPair")]
    public async Task StreamPairChanged_ShouldNotifySubscribersOfStateChanges()
    {
        // Arrange
        var service = CreateService();
        var streamPairs = new List<StreamPair?>();
        using var subscription = service.StreamPairChanged.Subscribe(streamPairs.Add);

        // Act
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(3, streamPairs.Count);
        Assert.Null(streamPairs[0]); // Начальное состояние
        Assert.NotNull(streamPairs[1]); // После старта
        Assert.Null(streamPairs[2]); // После остановки
    }

    [Fact]
    [Trait("Category", "StreamPair")]
    public async Task StreamPair_ShouldBeUpdatedOnRestart()
    {
        // Arrange
        var service = CreateService();
        var streamPairs = new List<StreamPair?>();
        using var subscription = service.StreamPairChanged.Subscribe(streamPairs.Add);

        // Act
        await service.StartAsync(CancellationToken.None);
        var initialStreamPair = service.CurrentStreamPair;
        
        await service.ForceRestartAsync(CancellationToken.None);
        var newStreamPair = service.CurrentStreamPair;

        // Assert
        Assert.NotNull(initialStreamPair);
        Assert.NotNull(newStreamPair);
        Assert.NotSame(initialStreamPair, newStreamPair);
        
        // Проверяем последовательность уведомлений
        Assert.True(streamPairs.Count >= 4);
        Assert.Null(streamPairs[0]); // Начальное состояние
        Assert.Same(initialStreamPair, streamPairs[1]); // После первого старта
        Assert.Null(streamPairs[2]); // После остановки при рестарте
        Assert.Same(newStreamPair, streamPairs[3]); // После перезапуска
    }


    [Fact]
    [Trait("Category", "StreamPair")]
    public async Task StreamPairChanged_ShouldCompleteOnDispose()
    {
        // Arrange
        var service = CreateService();
        var completed = false;
        using var subscription = service.StreamPairChanged.Subscribe(
            _ => { },
            () => completed = true);

        // Act
        service.Dispose();

        // Assert
        Assert.True(completed);
    }

    [Fact]
    [Trait("Category", "ReadyState")]
    public async Task WaitForReadyAsync_ShouldCompleteWhenServiceStarts()
    {
        // Arrange
        var service = CreateService();
        var readyTask = service.WaitForReadyAsync();

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        await readyTask; // Should complete without exception
        Assert.True(readyTask.IsCompletedSuccessfully);
        Assert.NotNull(service.CurrentStreamPair);
    }

    [Fact]
    [Trait("Category", "ReadyState")]
    public async Task WaitForReadyAsync_WhenAlreadyReady_ShouldCompleteImmediately()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        // Act
        var readyTask = service.WaitForReadyAsync();

        // Assert
        Assert.True(readyTask.IsCompleted);
        await readyTask; // Should complete immediately
    }

    [Fact]
    [Trait("Category", "ReadyState")]
    public async Task WaitForReadyAsync_WhenStartFails_ShouldThrowException()
    {
        // Arrange
        var service = CreateService();
        var expectedException = new InvalidOperationException("Test start failure");
        ProcessRunnerMock
            .Setup(r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var readyTask = service.WaitForReadyAsync();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(CancellationToken.None));

        // Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => readyTask);
        Assert.Equal("Test start failure", ex.Message);
    }

    [Fact]
    [Trait("Category", "ReadyState")]
    public async Task WaitForReadyAsync_WithTimeout_ShouldRespectCancellation()
    {
        // Arrange
        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => service.WaitForReadyAsync(cts.Token));
    }

    [Fact]
    [Trait("Category", "ReadyState")]
    public async Task WaitForReadyAsync_MultipleWaiters_ShouldAllComplete()
    {
        // Arrange
        var service = CreateService();
        var task1 = service.WaitForReadyAsync();
        var task2 = service.WaitForReadyAsync();
        var task3 = service.WaitForReadyAsync();

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        await Task.WhenAll(task1, task2, task3);
        Assert.True(task1.IsCompletedSuccessfully);
        Assert.True(task2.IsCompletedSuccessfully);
        Assert.True(task3.IsCompletedSuccessfully);
    }

    [Fact]
    [Trait("Category", "StreamPair")]
    public async Task StreamPairChanged_ShouldEmitCorrectSequenceOnFailedStart()
    {
        // Arrange
        var service = CreateService();
        var streamPairs = new List<StreamPair?>();
        using var subscription = service.StreamPairChanged.Subscribe(streamPairs.Add);

        ProcessRunnerMock
            .Setup(r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test failure"));

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(CancellationToken.None));

        // Assert
        Assert.Equal(2, streamPairs.Count);
        Assert.Null(streamPairs[0]); // Начальное состояние
        Assert.Null(streamPairs[1]); // После неудачного старта
    }

    [Fact]
    [Trait("Category", "StreamPair")]
    public async Task StreamPairChanged_ShouldEmitCorrectSequenceOnProcessExit()
    {
        // Arrange
        var service = CreateService();
        var streamPairs = new List<StreamPair?>();
        using var subscription = service.StreamPairChanged.Subscribe(streamPairs.Add);

        await service.StartAsync(CancellationToken.None);
        var process = GetPrivateField<IProcess>(service, "_currentProcess");
        var processMock = Mock.Get(process!);

        // Act
        processMock.Setup(p => p.HasExited).Returns(true);
        processMock.Raise(p => p.Exited += null, EventArgs.Empty);

        await Task.Delay(100); // Даем время на обработку события

        // Assert
        Assert.True(streamPairs.Count >= 4);
        Assert.Null(streamPairs[0]); // Начальное состояние
        Assert.NotNull(streamPairs[1]); // После первого старта
        Assert.Null(streamPairs[2]); // После краха процесса
        Assert.NotNull(streamPairs[3]); // После автоматического перезапуска
    }
}