using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.SignalCli;

namespace SignalCli.Tests.SignalCliHostedService;

/// <summary>
/// Тести, прив'язані до remediation-плану <c>address-audit-findings-2</c>:
/// B.3 (F2 race на Exited+Stop), B.6 (F3 віконний бюджет рестартів).
/// </summary>
[Trait("Category", "AuditFindings2")]
[Collection("HostedService")]
public class SignalCliHostedServiceAuditTests : SignalCliHostedServiceTestsBase
{
    [Fact]
    public async Task B3_IntentionalStop_ConcurrentWithUnexpectedExit_NoRestartAfterStop()
    {
        // B.3 (F2): свідома StopAsync, поки одночасно випадає Exited — має бути рівно одна
        // зупинка, без авто-перезапуску після intentional-stop. Лічимо кількість запусків
        // процесу (ProcessRunner.StartProcessWithHandle) — після Stop стартів бути не повинно.
        Options.MaxRestartAttempts = 3;
        Options.RestartWindowSeconds = 60;
        var service = CreateService();

        await service.StartAsync(CancellationToken.None);
        var initial = ProcessStartCallCount;
        Assert.Equal(1, initial);

        var process = service.CurrentProcessForTests;
        var processMock = Mock.Get(process!);

        // Намагаємось викликати OnProcessExited та StopAsync «майже одночасно».
        // OnProcessExitedAsync завжди серіалізується через _operationLock, тож хоч хто з них
        // зайде в лок другим — побачить _stopping=true або state=Stopped і вийде, не рестартуючи.
        var exitTask = Task.Run(() => processMock.Raise(p => p.Exited += null, EventArgs.Empty));
        var stopTask = service.StopAsync(CancellationToken.None);
        await Task.WhenAll(exitTask, stopTask);

        // Чекаємо ще трохи на можливі залишкові посттригери.
        await Task.Delay(200);

        // Жодного нового запуску процесу — це і є відсутність restart-after-stop.
        Assert.Equal(initial, ProcessStartCallCount);
        Assert.Equal(ProcessState.Stopped, StateManager.CurrentState);
    }

    [Fact]
    public async Task B6_WindowedRestartBudget_RecoversAfterStableRunWindow()
    {
        // B.6 (F3): процес, що "переживає" RestartWindow у Running, має мати лічильник
        // рестартів скинутий — тож наступний крах знову триггерить рестарт, навіть якщо
        // раніше "бюджет" був витрачений. Перевіряємо це на короткому вікні 1с.
        Options.MaxRestartAttempts = 1;
        Options.RestartWindowSeconds = 1;
        Options.RestartDelaySeconds = 0;

        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        // Перший крах -> авто-рестарт (1 of 1)
        var first = service.CurrentProcessForTests;
        Mock.Get(first!).Raise(p => p.Exited += null, EventArgs.Empty);

        // Чекаємо стабілізації нового процесу понад RestartWindow — лічильник скинеться.
        await Task.Delay(TimeSpan.FromSeconds(Options.RestartWindowSeconds + 1));

        // Другий крах — за старою (лінійною) логікою цей рестарт уже був би заборонений
        // (бюджет вичерпано). Із віконним скиданням рестарт має відбутися.
        var afterFirstRestartCount = ProcessStartCallCount;
        var second = service.CurrentProcessForTests;
        Assert.NotNull(second);
        Mock.Get(second!).Raise(p => p.Exited += null, EventArgs.Empty);

        // Даємо час на async-рестарт всередині OnProcessExitedAsync.
        await Task.Delay(500);

        Assert.True(ProcessStartCallCount > afterFirstRestartCount,
            $"Очікувався додатковий рестарт після скидання вікна; стартів: {ProcessStartCallCount}");

        await service.StopAsync(CancellationToken.None);
    }
}
