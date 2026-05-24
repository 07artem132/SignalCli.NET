using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.SignalCli;

namespace SignalCli.Tests.SignalCliHostedService;

[Trait("Category", "HostedService")]
[Collection("HostedService")]
public class SignalCliHostedServiceRestartTests : SignalCliHostedServiceTestsBase
{
    [Fact]
    [Trait("Category", "ProcessRestart")]
    public async Task ForceRestartAsync_WhenRunning_ShouldStopAndRestartProcess()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        var initialProcessId = service.CurrentProcessForTests?.Id;

        // Следим за порядком изменения состояний
        var stateChanges = new List<ProcessState>();
        using var stateSubscription = StateManager.ProcessState
            .Subscribe(info => stateChanges.Add(info.State));
        
        // Act
        await service.ForceRestartAsync(CancellationToken.None);

        // Assert
        var newProcessId = service.CurrentProcessForTests?.Id;
        Assert.NotEqual(initialProcessId, newProcessId);
        Assert.Equal(ProcessState.Running, StateManager.CurrentState);
        
        // Перевіряємо послідовність станів
        Assert.Contains(ProcessState.Stopping, stateChanges);
        Assert.Contains(ProcessState.Starting, stateChanges);
        Assert.Equal(ProcessState.Running, stateChanges.Last());

        // Перевіряємо, що процес було запущено двічі
        ProcessRunnerMock.Verify(
            r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    [Trait("Category", "ProcessRestart")]
    public async Task ForceRestartAsync_WhenExceedingMaxAttempts_ShouldNotRestart()
    {
        // Arrange
        Config.MaxRestartAttempts = 1;
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        var initialProcessId = service.CurrentProcessForTests?.Id;

        // Act
        // Перша спроба (дозволена)
        await service.ForceRestartAsync(CancellationToken.None);
        var firstRestartProcessId = service.CurrentProcessForTests?.Id;

        ProcessStartCallCount = 0;
        // Друга спроба (перевищує ліміт)
        await service.ForceRestartAsync(CancellationToken.None);
        var secondRestartProcessId = service.CurrentProcessForTests?.Id;

        // Assert
        Assert.NotEqual(initialProcessId, firstRestartProcessId);
        Assert.Equal(firstRestartProcessId, secondRestartProcessId); // Второй перезапуск не произошел
        Assert.Equal(0, ProcessStartCallCount);
        // B.5: текст логу тепер містить ще й розмір вікна стабільності.
        VerifyLog(LogLevel.Error, $"ForceRestartAsync: перевищено MaxRestartAttempts ({Config.MaxRestartAttempts})");
    }

    [Fact]
    [Trait("Category", "ProcessRestart")]
    public async Task OnProcessExited_WhenUnexpected_ShouldAutoRestart()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        var initialProcess = service.CurrentProcessForTests;
        var initialProcessMock = Mock.Get(initialProcess!);
        
        var stateChanges = new List<ProcessState>();
        using var stateSubscription = StateManager.ProcessState
            .Subscribe(info => stateChanges.Add(info.State));
        
        // Act
        initialProcessMock.Setup(p => p.HasExited).Returns(true);
        initialProcessMock.Raise(p => p.Exited += null, EventArgs.Empty);
        
        // Чекаємо автоперезапуск
        await Task.Delay(100);

        // Assert
        Assert.Equal(ProcessState.Running, StateManager.CurrentState);
        ProcessRunnerMock.Verify(
            r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
            
        Assert.Contains(ProcessState.Failed, stateChanges);
        Assert.Contains(ProcessState.Starting, stateChanges);
        Assert.Equal(ProcessState.Running, stateChanges.Last());
        
        // Перевіряємо, що це новий процес
        var newProcess = service.CurrentProcessForTests;
        Assert.NotNull(newProcess);
        Assert.NotSame(initialProcess, newProcess);
    }

    [Fact]
    [Trait("Category", "ProcessRestart")]
    public async Task OnProcessExited_WhenAutoRestartDisabled_ShouldRemainFailed()
    {
        // Arrange
        Config.MaxRestartAttempts = 0;
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        var process = service.CurrentProcessForTests;
        var processMock = Mock.Get(process!);
        
        // Act
        processMock.Setup(p => p.HasExited).Returns(true);
        processMock.Raise(p => p.Exited += null, EventArgs.Empty);
        
        await Task.Delay(100);

        // Assert
        Assert.Equal(ProcessState.Failed, StateManager.CurrentState);
        ProcessRunnerMock.Verify(
            r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyLog(LogLevel.Warning, "Автоперезапуск вимкнено (MaxRestartAttempts=0).");
    }

    [Fact]
    [Trait("Category", "ProcessRestart")]
    public async Task OnProcessExited_DuringShutdown_ShouldNotAutoRestart()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        var process = service.CurrentProcessForTests;
        var processMock = Mock.Get(process!);
        
        // Начинаем остановку
        var stopTask = service.StopAsync(CancellationToken.None);
        
        // Симулируем завершение процесса во время остановки
        processMock.Setup(p => p.HasExited).Returns(true);
        processMock.Raise(p => p.Exited += null, EventArgs.Empty);
        
        await stopTask;

        // Assert
        Assert.Equal(ProcessState.Stopped, StateManager.CurrentState);
        ProcessRunnerMock.Verify(
            r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()),
            Times.Once); // Тільки початковий запуск
    }

    [Fact]
    [Trait("Category", "ProcessRestart")]
    public async Task ForceRestartAsync_ShouldRespectRestartDelay()
    {
        // Arrange
        // B.6: Task.Delay у ForceRestartAsync — через TimeProvider. Тест перевіряє, що рестарт
        // НЕ завершиться, поки віртуальний час не зрушено на RestartDelaySeconds (а потім — завершиться).
        Config.RestartDelaySeconds = 1;
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService(fakeTime);
        await service.StartAsync(CancellationToken.None);

        // Act
        var restartTask = service.ForceRestartAsync(CancellationToken.None);
        // Даємо ForceRestartAsync дійти до Task.Delay (stop → state-update → delay).
        for (int i = 0; i < 20 && !restartTask.IsCompleted; i++)
            await Task.Yield();
        Assert.False(restartTask.IsCompleted, "ForceRestartAsync завершився до Advance — затримка не дотримана");

        fakeTime.Advance(TimeSpan.FromSeconds(1));
        await restartTask;

        // Assert
        Assert.True(restartTask.IsCompletedSuccessfully);
    }

    [Fact]
    [Trait("Category", "ProcessRestart")]
    public async Task ForceRestartAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        // post-modernize-tuning §6.2: після переходу з Nito.AsyncEx.AsyncLock на SemaphoreSlim
        // SemaphoreSlim.WaitAsync(cancelled-token) кидає TaskCanceledException (підклас
        // OperationCanceledException). Використовуємо ThrowsAnyAsync, щоб ловити обидва.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ForceRestartAsync(cts.Token));
    }

    [Fact]
    [Trait("Category", "ProcessRestart")]
    public async Task OnProcessExited_ShouldResetRestartCountOnSuccessfulStart()
    {
        // Arrange
        Config.MaxRestartAttempts = 2;
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        
        // Симулюємо один успішний перезапуск
        var process1 = service.CurrentProcessForTests;
        var processMock1 = Mock.Get(process1!);
        processMock1.Setup(p => p.HasExited).Returns(true);
        processMock1.Raise(p => p.Exited += null, EventArgs.Empty);
        
        await Task.Delay(100); // Чекаємо перезапуск
        Assert.Equal(ProcessState.Running, StateManager.CurrentState);
        
        // Другий перезапуск має бути можливим, оскільки лічильник скинувся
        var process2 = service.CurrentProcessForTests;
        var processMock2 = Mock.Get(process2!);
        processMock2.Setup(p => p.HasExited).Returns(true);
        processMock2.Raise(p => p.Exited += null, EventArgs.Empty);
        
        await Task.Delay(100);

        // Assert
        Assert.Equal(ProcessState.Running, StateManager.CurrentState);
        ProcessRunnerMock.Verify(
            r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3)); // Початковий + 2 перезапуски
    }
}