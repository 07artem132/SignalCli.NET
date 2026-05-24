using Microsoft.Extensions.Hosting;

namespace SignalCli.Tests.SignalCliHostedService;

/// <summary>
/// post-modernize-tuning §8a.2 / §8a.3 (audit B3/C1): сервіс implementiує
/// <see cref="IHostedLifecycleService"/> (4 phase-методи) і <see cref="IAsyncDisposable"/>.
/// DI-контейнер preferр'ить <see cref="IAsyncDisposable.DisposeAsync"/> при tear-down'і.
/// </summary>
public sealed class AsyncDisposalLifecycleTests : SignalCliHostedServiceTestsBase
{
    [Fact]
    public void SignalCliHostedService_ImplementsIHostedLifecycleService()
    {
        // Public-contract assertion: тип реалізує phase-інтерфейс. DI-контейнер
        // (Microsoft.Extensions.Hosting) детектить через is-pattern і викликає всі 4 phase.
        var service = CreateService();
        Assert.IsAssignableFrom<IHostedLifecycleService>(service);
    }

    [Fact]
    public void SignalCliHostedService_ImplementsBothDisposableInterfaces()
    {
        var service = CreateService();
        Assert.IsAssignableFrom<IDisposable>(service);
        Assert.IsAssignableFrom<IAsyncDisposable>(service);
    }

    [Fact]
    public async Task LifecyclePhases_Returning_CompletedTask_DoNotThrow()
    {
        var service = CreateService();
        // No-op phase методи мають повертати CompletedTask без винятків.
        await service.StartingAsync(CancellationToken.None);
        await service.StartedAsync(CancellationToken.None);
        await service.StoppingAsync(CancellationToken.None);
        await service.StoppedAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DisposeAsync_AfterClean_RunIsIdempotent()
    {
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        // Перший DisposeAsync дренує lock (порожньо, бо StopAsync уже виконано) + cleanup.
        await service.DisposeAsync();
        // Повторний DisposeAsync — no-op через _disposedFlag interlock.
        await service.DisposeAsync();
        // Сторонній Dispose теж no-op.
        service.Dispose();
    }

    [Fact]
    public async Task DisposeAsync_DrainsInFlightOperation_BeforeKillingProcess()
    {
        // Якщо StartAsync ЩЕ ВИКОНУЄТЬСЯ (тримає _operationLock), DisposeAsync чекає до 2с
        // на завершення; потім приступає до sync-cleanup. Цей тест перевіряє нормальний шлях:
        // після Stop'у lock вільний, DisposeAsync завершується швидко.
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await service.DisposeAsync();
        stopwatch.Stop();

        // Lock вільний — DisposeAsync не повинен наближатися до 2с-drain-timeout.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1),
            $"DisposeAsync мав завершитися швидко (lock вільний), а зайняв {stopwatch.Elapsed}");
    }
}
