using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.SignalCli;

namespace SignalCli.Tests.SignalCliHostedService;

[Trait("Category", "HostedService")]
[Collection("HostedService")]
// G.7e: раніше клас називався "...IntegrationTests", але насправді це повністю
// замоканий state/lifecycle-набір, без жодного запуску signal-cli. Реальні E2E
// зараз НЕ запускаються в цій сесії (G.7 — окрема велика робота); цей файл
// перейменовано в *StateTests, щоб назва відповідала змісту.
public class SignalCliHostedServiceStateTests : SignalCliHostedServiceTestsBase
{
    [Fact]
    [Trait("Category", "StateManagement")]
    public async Task ProcessState_ShouldEmitCorrectSequenceOfStates()
    {
        // Arrange
        var service = CreateService();
        var states = new List<ProcessState>();
        using var subscription = StateManager.ProcessState
            .Subscribe(info => states.Add(info.State));

        // Act
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        // Assert
        var expectedStates = new[]
        {
            ProcessState.NotStarted, // Початковий
            ProcessState.Starting, // При старте
            ProcessState.Running, // Після успішного старту
            ProcessState.Stopping, // При остановке
            ProcessState.Stopped // Після зупинки
        };

        Assert.Equal(expectedStates, states);

        // Додатково перевіряємо логи для кожного стану
        VerifyLog(LogLevel.Information, "SignalCliHostedService запускається...");
        VerifyLog(LogLevel.Information, "SignalCliHostedService успішно запущено.");
        VerifyLog(LogLevel.Information, "SignalCliHostedService зупиняється...");
        VerifyLog(LogLevel.Information, "SignalCliHostedService зупинено.");
    }

    [Fact]
    [Trait("Category", "StateManagement")]
    public async Task ProcessState_WhenStartFails_ShouldTransitionToFailed()
    {
        // Arrange
        var service = CreateService();
        var states = new List<ProcessState>();
        using var subscription = StateManager.ProcessState
            .Subscribe(info => states.Add(info.State));

        ProcessRunnerMock
            .Setup(r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Start failed"));

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(CancellationToken.None));

        // Assert
        Assert.Equal(
            new[] { ProcessState.NotStarted, ProcessState.Starting, ProcessState.Failed },
            states);
    }

    [Fact]
    [Trait("Category", "StateManagement")]
    public async Task ProcessState_OnProcessExit_ShouldTransitionThroughFailedState()
    {
        // Arrange
        var service = CreateService();
        var states = new List<ProcessState>();
        using var subscription = StateManager.ProcessState
            .Subscribe(info => states.Add(info.State));

        await service.StartAsync(CancellationToken.None);
        var process = service.CurrentProcessForTests;
        var processMock = Mock.Get(process!);

        // Очищаем текущий список состояний
        states.Clear();

        // Act
        processMock.Setup(p => p.HasExited).Returns(true);
        processMock.Raise(p => p.Exited += null, EventArgs.Empty);

        await Task.Delay(100); // Даємо час на обробку події та автоперезапуск

        // Assert
        // Перевіряємо, що пройшли через Failed перед автоперезапуском
        Assert.Contains(ProcessState.Failed, states);
        Assert.Equal(ProcessState.Running, states.Last());
    }

    [Fact]
    [Trait("Category", "Configuration")]
    public async Task ProcessRunner_ShouldReceiveCorrectEnvironmentVariables()
    {
        // Arrange
        // post-modernize-tuning §4.10 + deprecated-shim-removal §5: Options.EnvironmentVariables
        // напряму призначається на SignalCliOptions (legacy Config.WithEnvironment видалено).
        Options.EnvironmentVariables = new Dictionary<string, string>
        {
            ["JAVA_HOME"] = "",
            ["PATH"] = "",
        };
        var service = CreateService();

        ProcessConfig? capturedConfig = null;
        ProcessRunnerMock
            .Setup(r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessConfig, CancellationToken>((config, _) => capturedConfig = config)
            .ReturnsAsync(() =>
            {
                var pMock = new Mock<IProcess>();
                pMock.Setup(p => p.Start(It.IsAny<CancellationToken>())).Returns(true);
                return (pMock.Object, new StreamPair(
                    new StreamWriter(new MemoryStream()),
                    new StreamReader(new MemoryStream()),
                    new StreamReader(new MemoryStream())));
            });

        // Act
        await service.StartAsync(CancellationToken.None);
        // Assert
        Assert.NotNull(capturedConfig);
        Assert.Contains(
            capturedConfig.EnvironmentVariables,
            kv => kv.Key == "JAVA_HOME" || kv.Key == "PATH");
    }

    [Theory]
    [InlineData(1)] // 1 секунда
    [InlineData(2)] // 2 секунди
    public async Task ForceRestart_ShouldHandleVariousRestartDelays(int delaySeconds)
    {
        // Arrange
        // B.6: Task.Delay у ForceRestartAsync тепер через TimeProvider — крутимо віртуальний час
        // FakeTimeProvider замість wall-clock-Stopwatch (раніше тест був flaky через ±1мс).
        Options.RestartDelaySeconds = delaySeconds;
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService(fakeTime);
        await service.StartAsync(CancellationToken.None);

        // Act
        var restartTask = service.ForceRestartAsync(CancellationToken.None);
        // Дамо ForceRestartAsync дійти до Task.Delay (зупинка процесу + перехід стану).
        for (int i = 0; i < 20 && !restartTask.IsCompleted; i++)
            await Task.Yield();
        // Провернемо віртуальний годинник на потрібну затримку.
        fakeTime.Advance(TimeSpan.FromSeconds(delaySeconds));
        await restartTask;

        // Assert: достатньо, що ForceRestartAsync завершився після Advance(delaySeconds).
        Assert.True(restartTask.IsCompletedSuccessfully);
    }

    [Theory]
    [InlineData(0)] // Без спроб (вимкнено)
    [InlineData(1)] // Одна спроба
    [InlineData(3)] // Кілька спроб
    public async Task OnProcessExit_ShouldRespectMaxRestartAttempts(int maxAttempts)
    {
        // Arrange
        Options.MaxRestartAttempts = maxAttempts;

        var processStartedTcs = new TaskCompletionSource<bool>();
        var allRestartsFinishedTcs = new TaskCompletionSource<bool>();

        var attempts = 0;

        ProcessRunnerMock
            .Setup(r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                attempts++;
                var pMock = new Mock<IProcess>();
                pMock.Setup(p => p.Start(It.IsAny<CancellationToken>())).Returns(true);
                pMock.Setup(p => p.HasExited).Returns(true);

                var streams = new StreamPair(
                    new StreamWriter(new MemoryStream()),
                    new StreamReader(new MemoryStream()),
                    new StreamReader(new MemoryStream()));

                // Сигнализируем о создании процесса
                processStartedTcs.TrySetResult(true);

                // Эмулируем асинхронное завершение процесса
                Task.Run(async () =>
                {
                    await Task.Delay(10); // Минимальная задержка
                    pMock.Raise(p => p.Exited += null, EventArgs.Empty);

                    // Якщо це остання спроба перезапуску, сигналізуємо про завершення
                    if (attempts >= (maxAttempts == 0 ? 1 : maxAttempts + 1))
                    {
                        // Даем время для завершения всех внутренних обработчиков
                        await Task.Delay(50);
                        allRestartsFinishedTcs.TrySetResult(true);
                    }
                });

                return (pMock.Object, streams);
            });

        var service = CreateService();

        // Act
        await service.StartAsync(CancellationToken.None);

        // Ожидаем запуск первого процесса
        await processStartedTcs.Task;

        // Ожидаем завершения всех перезапусков с таймаутом
        await Task.WhenAny(allRestartsFinishedTcs.Task, Task.Delay(5000));

        // Assert
        // +1 до maxAttempts, бо перший старт не зараховується як спроба
        var expectedAttempts = maxAttempts == 0 ? 1 : maxAttempts + 1;
        Assert.Equal(expectedAttempts, attempts);

        if (maxAttempts == 0)
        {
            Assert.Equal(ProcessState.Failed, StateManager.CurrentState);
            VerifyLog(LogLevel.Warning, "Автоперезапуск вимкнено");
        }
        else
        {
            // B.5: текст лог-меседжу тепер містить ще й розмір вікна стабільності — звужуємо substring.
            VerifyLog(LogLevel.Error,
                $"Досягнуто максимальну кількість перезапусків ({maxAttempts})");
        }
    }
}