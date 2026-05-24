using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Models.SignalCli;
using SignalCli.Services.SignalCli;

namespace SignalCli.Tests.SignalCliHostedService;

/// <summary>
/// post-modernize-tuning §2.5 / §7.5: захист від reentrancy-deadlock'у у
/// <see cref="ProcessStateManager.UpdateState"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="System.Threading.Lock"/> (C# 13 / .NET 9+) — НЕ реентрантний (на відміну від
/// `Monitor`). Якщо synchronous Rx-підписник на <c>ProcessState</c>-стрімі викличе
/// <c>UpdateState</c> повторно з обробника `OnNext`, і lock ще не звільнено — ми отримаємо
/// миттєвий deadlock.
/// </para>
/// <para>
/// Фікс (вже в коді з §2.1): snapshot-then-emit — поки тримаємо lock, лише оновлюємо
/// `_currentStateInfo`, потім ВИХОДИМО з lock'у, і лише потім викликаємо `OnNext` поза
/// lock'ом. Reentrant `UpdateState` бере lock з нуля — успішно.
/// </para>
/// <para>
/// Цей тест запускає reentrant-сценарій на real-time; якщо завис &gt; 2 секунд — тест фейлиться.
/// Жодного <c>Task.Delay</c>-вікна; deadlock detected by timeout-on-task.
/// </para>
/// </remarks>
public sealed class StateManagerReentrancyTests
{
    [Fact]
    public async Task UpdateState_WhenSubscriberReentersWithUpdateState_DoesNotDeadlock()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<ProcessStateManager>>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        using var stateManager = new ProcessStateManager(loggerMock.Object);

        var observedStates = new List<ProcessState>();
        var reentryDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Synchronous Rx-підписник, який З RECEIVED-callback'а викликає UpdateState знову.
        // Якщо UpdateState тримав би _lock під час OnNext — це deadlock'нулося б миттєво.
        // BehaviorSubject емітить поточний (NotStarted) стан негайно при Subscribe — фільтруємо.
        using var sub = stateManager.ProcessState.Subscribe(info =>
        {
            lock (observedStates) { observedStates.Add(info.State); }
            // Реентрантний виклик: переходимо у Stopping якщо щойно прийшли в Starting.
            if (info.State == ProcessState.Starting)
            {
                stateManager.UpdateState(ProcessState.Stopping);
            }
            else if (info.State == ProcessState.Stopping)
            {
                reentryDone.TrySetResult(true);
            }
        });

        // Act: ініціюємо ланцюжок Starting → (reenter) → Stopping.
        // Виконуємо в Task.Run щоб мати 2с-deadlock-window-monitoring через WaitAsync.
        var driveTask = Task.Run(() => stateManager.UpdateState(ProcessState.Starting));

        // Assert: ланцюг має дозавершитися до 2с — інакше deadlock.
        await reentryDone.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await driveTask.WaitAsync(TimeSpan.FromSeconds(2));

        List<ProcessState> snapshot;
        lock (observedStates) { snapshot = [.. observedStates]; }
        // NotStarted емітить BehaviorSubject при Subscribe; далі — реентрантний ланцюг.
        Assert.Equal([ProcessState.NotStarted, ProcessState.Starting, ProcessState.Stopping], snapshot);
        Assert.Equal(ProcessState.Stopping, stateManager.CurrentState);
    }

    [Fact]
    public async Task UpdateState_TwoConcurrentCallers_AllSnapshotsDelivered_NoDeadlock()
    {
        // Захист від іншої форми reentrancy: дві паралельні UpdateState-нитки, кожна
        // конкурує за lock. snapshot-then-emit гарантує що OnNext поза lock'ом — тож
        // друга нитка може зайти у lock поки перша робить OnNext.
        var loggerMock = new Mock<ILogger<ProcessStateManager>>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        using var stateManager = new ProcessStateManager(loggerMock.Object);

        var observed = new List<ProcessState>();
        using var sub = stateManager.ProcessState.Subscribe(info =>
        {
            lock (observed) { observed.Add(info.State); }
        });

        var task1 = Task.Run(() => stateManager.UpdateState(ProcessState.Starting));
        var task2 = Task.Run(() => stateManager.UpdateState(ProcessState.Running));

        await Task.WhenAll(task1, task2).WaitAsync(TimeSpan.FromSeconds(2));

        List<ProcessState> snapshot;
        lock (observed) { snapshot = [.. observed]; }
        // BehaviorSubject емітить NotStarted при Subscribe + обидва Update-snapshot'и.
        // Порядок Starting/Running не гарантований через race-вікно між двома Task.Run'ами.
        Assert.Contains(ProcessState.NotStarted, snapshot);
        Assert.Contains(ProcessState.Starting, snapshot);
        Assert.Contains(ProcessState.Running, snapshot);
        Assert.Equal(3, snapshot.Count);
    }
}
