using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using SignalCli.Models.SignalCli;

namespace SignalCli.Tests.SignalCliHealthMonitor;

/// <summary>
/// post-modernize-tuning §8a.6: Регресія-захист — переконуємось, що
/// <see cref="Microsoft.Extensions.Hosting.BackgroundService"/>-обгортка для
/// <see cref="Services.SignalCli.SignalCliHealthMonitor"/> належно інтегрована:
/// <c>StopAsync</c> блокує до тих пір, доки <c>ExecuteAsync</c> не побачить
/// cancellation і не вийде з циклу.
/// </summary>
public sealed class BackgroundServiceLifecycleTests : SignalCliHealthMonitorTestBase
{
    [Fact]
    public async Task StopAsync_BlocksUntilExecuteAsync_ObservesCancellation()
    {
        // Arrange — fakeTime гарантує, що PeriodicTimer-tick не спрацьовує сам по собі.
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        ServiceConfig.HealthCheckIntervalSeconds = 1; // 1с-interval; tick'и керовані через fakeTime.Advance.

        // Ping-callback фіксує що ExecuteAsync дійсно крутився.
        var inLoopSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        JsonRpcClientMock
            .Setup(c => c.InvokeMethodAsync<VersionParameters, VersionResponse>(
                It.IsAny<string>(),
                It.IsAny<VersionParameters>(),
                It.IsAny<JsonTypeInfo<VersionParameters>>(),
                It.IsAny<JsonTypeInfo<VersionResponse>>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => inLoopSeen.TrySetResult(true))
            .ReturnsAsync(new VersionResponse("ok"));

        var hostedService = CreateHostedService();
        await hostedService.StartAsync(CancellationToken.None);
        var monitor = CreateMonitor(hostedService, fakeTime);

        // Act
        await monitor.StartAsync(CancellationToken.None);

        // Маленька real-time пауза, щоб ExecuteAsync встиг увійти в `WaitForNextTickAsync`
        // і зареєструвати PeriodicTimer на віртуальному годиннику; інакше Advance "у порожнечу".
        await Task.Delay(50);
        fakeTime.Advance(TimeSpan.FromSeconds(ServiceConfig.HealthCheckIntervalSeconds));
        await inLoopSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Тепер запитуємо StopAsync — він має:
        //  (а) тригернути stoppingToken-cancel
        //  (б) дочекатися завершення ExecuteAsync (через base.StopAsync logic у BackgroundService)
        // Real-time fallback 5с — якщо StopAsync завис понад нього, ExecuteAsync НЕ підхопив cancel.
        await monitor.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert — ExecuteTask має бути або completed, або canceled-faulted.
        Assert.True(monitor.ExecuteTask is null
                    || monitor.ExecuteTask.IsCompleted,
            "Після StopAsync.WaitAsync ExecuteTask має бути завершений (Completed/Canceled/Faulted).");

        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_WithoutStart_DoesNotThrow()
    {
        // Sanity: StopAsync без StartAsync — no-op (per BackgroundService contract).
        var hostedService = CreateHostedService();
        var monitor = CreateMonitor(hostedService);

        await monitor.StopAsync(CancellationToken.None);
        // Дотягуємо хост в clean state.
        await hostedService.StopAsync(CancellationToken.None);
    }
}
