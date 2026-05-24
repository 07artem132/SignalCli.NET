using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.SignalCli;

namespace SignalCli.Tests.SignalCliHostedService;

/// <summary>
/// post-modernize-tuning §8a.8 (audit N4): <see cref="Services.SignalCli.SignalCliHostedService.StopProcessInternalAsyncNoLock"/>
/// використовує <c>new CancellationTokenSource(StopTimeoutSeconds, _timeProvider)</c>-overload.
/// FakeTimeProvider.Advance(StopTimeoutSeconds + 1) має тригерити kill-branch (через
/// timeout WaitForExitAsync), без wall-clock-залежності.
/// </summary>
public sealed class StopProcessTimeoutVirtualizationTests : SignalCliHostedServiceTestsBase
{
    [Fact]
    public async Task StopAsync_WhenWaitForExitTimesOut_KillsProcess_OnVirtualClock()
    {
        // Arrange
        Config.StopTimeoutSeconds = 5;
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var killCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // ProcessRunner-mock із підмінений: HasExited=false, WaitForExitAsync блокує
        // ДО скасування токена (= FakeTime.Advance тригерить timeoutCts).
        // Kill — has assertion that triggers TCS.
        ProcessRunnerMock.Reset();
        ProcessRunnerMock
            .Setup(r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                Interlocked.Increment(ref ProcessStartCallCount);
                var pMock = new Mock<IProcess>();
                pMock.Setup(p => p.Start(It.IsAny<CancellationToken>())).Returns(true);
                pMock.Setup(p => p.HasExited).Returns(false);
                pMock.Setup(p => p.StandardInput).Returns(new StreamWriter(new MemoryStream()));
                pMock.Setup(p => p.StandardOutput).Returns(new StreamReader(new MemoryStream()));
                pMock.Setup(p => p.StandardError).Returns(new StreamReader(new MemoryStream()));

                // WaitForExitAsync чекає на токен-cancel. CancellationToken.Register
                // — точкова реакція: коли FakeTime.Advance триггерить timeoutCts,
                // лінкований токен cancel'ить TCS — і WaitForExitAsync лине з OCE.
                pMock.Setup(p => p.WaitForExitAsync(It.IsAny<CancellationToken>()))
                    .Returns<CancellationToken>(ct =>
                    {
                        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        ct.Register(() => tcs.TrySetCanceled(ct));
                        return tcs.Task;
                    });

                // Kill сигналізує що Kill-branch виконано.
                pMock.Setup(p => p.Kill(It.IsAny<bool>())).Callback(() => killCalled.TrySetResult(true));

                var streams = new StreamPair(pMock.Object.StandardInput, pMock.Object.StandardOutput, pMock.Object.StandardError);
                return (pMock.Object, streams);
            });

        var service = CreateService(fakeTime);
        await service.StartAsync(CancellationToken.None);
        Assert.Equal(ProcessState.Running, StateManager.CurrentState);

        // Act: StopAsync в Task.Run — бо це blocking-call (WaitForExitAsync чекає token-cancel).
        var stopTask = Task.Run(() => service.StopAsync(CancellationToken.None));

        // Невелика затримка щоб StopAsync встиг дойти до await WaitForExitAsync і
        // зареєструвати timeoutCts на FakeTime'овому годиннику.
        await Task.Yield();
        await Task.Delay(50); // дозволяємо WaitForExitAsync-mock'у запустись

        // Провертаємо віртуальний годинник за StopTimeoutSeconds.
        fakeTime.Advance(TimeSpan.FromSeconds(Config.StopTimeoutSeconds + 1));

        // Assert: Kill викликано до 5с real-time (швидко після Advance).
        await killCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await stopTask.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
