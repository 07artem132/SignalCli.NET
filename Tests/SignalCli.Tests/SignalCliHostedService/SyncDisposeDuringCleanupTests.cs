using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models;
using SignalCli.Models.SignalCli;
using SignalCli.Services.SignalCli;

namespace SignalCli.Tests.SignalCliHostedService;

/// <summary>
/// sync-dispose-during-cleanup race-prober (CHANGELOG [4.0] pending follow-up): пінує
/// що <c>SignalCliHostedService.Dispose()</c> sync-path дренує <c>_operationLock</c>
/// з коротким fallback'ом і не дедлокає, навіть якщо в момент Dispose() лок все ще
/// тримається іншою in-flight операцією. Перевіряє <c>field-barrier-hardening</c>
/// інваріант з SignalCliHostedService.cs:631-651.
/// </summary>
public sealed class SyncDisposeDuringCleanupTests : IDisposable
{
    private readonly string _tempDir;

    public SyncDisposeDuringCleanupTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "lib"));
        File.WriteAllText(Path.Combine(_tempDir, "lib", "signal-cli.jar"), "fake");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { /* best-effort */ }
    }

    private SignalCli.Services.SignalCli.SignalCliHostedService CreateService(
        Mock<IProcessRunner> runnerMock,
        SignalCli.Services.SignalCli.ProcessStateManager stateManager)
    {
        var loggerMock = new Mock<ILogger<SignalCli.Services.SignalCli.SignalCliHostedService>>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var opts = new SignalCliOptions
        {
            AppHome = _tempDir,
            LibDirectory = "lib",
            JavaExecutable = "java",
            MaxRestartAttempts = 1,
            RestartDelaySeconds = 0,
            StopTimeoutSeconds = 0,
            HealthCheckIntervalSeconds = 1,
            HealthCheckTimeoutSeconds = 1,
        };
        return new SignalCli.Services.SignalCli.SignalCliHostedService(
            loggerMock.Object,
            runnerMock.Object,
            stateManager,
            Options.Create(opts),
            null);
    }

    [Fact]
    public void Dispose_DuringHeldOperationLock_DoesNotDeadlock_AndCompletesUnderFallback()
    {
        // Arrange — стандартний runner, нормальна StartAsync.
        var startCount = 0;
        var runnerMock = new Mock<IProcessRunner>();
        runnerMock.Setup(r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                startCount++;
                var pMock = new Mock<IProcess>();
                pMock.Setup(p => p.Start(It.IsAny<CancellationToken>())).Returns(true);
                pMock.Setup(p => p.Id).Returns(startCount);
                var streams = new StreamPair(
                    new StreamWriter(new MemoryStream()),
                    new StreamReader(new MemoryStream()),
                    new StreamReader(new MemoryStream()));
                pMock.Setup(p => p.StandardInput).Returns(streams.StandardInput);
                pMock.Setup(p => p.StandardOutput).Returns(streams.StandardOutput);
                pMock.Setup(p => p.StandardError).Returns(streams.StandardError);
                return (pMock.Object, streams);
            });

        var smLogger = new Mock<ILogger<SignalCli.Services.SignalCli.ProcessStateManager>>();
        smLogger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var stateManager = new SignalCli.Services.SignalCli.ProcessStateManager(smLogger.Object);
        var service = CreateService(runnerMock, stateManager);

        service.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

        // Тримаємо _operationLock через reflection — імітуємо in-flight операцію.
        var lockField = typeof(SignalCli.Services.SignalCli.SignalCliHostedService)
            .GetField("_operationLock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(lockField);
        var sem = (SemaphoreSlim)lockField!.GetValue(service)!;
        sem.Wait(); // повний lock — Dispose'у доведеться fallback'нутися через 50ms.

        try
        {
            // Act — sync Dispose() з ще утримуваним локом. Перевіряємо що:
            //   1) Метод повертається без deadlock.
            //   2) Час очікування ~50ms fallback (а не нескінченність).
            var sw = System.Diagnostics.Stopwatch.StartNew();
            service.Dispose();
            sw.Stop();

            // Fallback drain == 50ms; з jitter'ом на test machine та проходом
            // через DisposeCore — даємо просторий cap 5 секунд (точніше неможливо
            // без слабкого assert). Головне — НЕ deadlock (тест-runner не висить).
            Assert.True(sw.ElapsedMilliseconds < 5000,
                $"Dispose() висів {sw.ElapsedMilliseconds}ms — fallback drain зламано.");
        }
        finally
        {
            sem.Release();
        }
    }

    [Fact]
    public void Dispose_WhenLockFree_AcquiresImmediately_AndReleases()
    {
        var runnerMock = new Mock<IProcessRunner>();
        runnerMock.Setup(r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var pMock = new Mock<IProcess>();
                pMock.Setup(p => p.Start(It.IsAny<CancellationToken>())).Returns(true);
                pMock.Setup(p => p.Id).Returns(1);
                var streams = new StreamPair(
                    new StreamWriter(new MemoryStream()),
                    new StreamReader(new MemoryStream()),
                    new StreamReader(new MemoryStream()));
                pMock.Setup(p => p.StandardInput).Returns(streams.StandardInput);
                pMock.Setup(p => p.StandardOutput).Returns(streams.StandardOutput);
                pMock.Setup(p => p.StandardError).Returns(streams.StandardError);
                return (pMock.Object, streams);
            });

        var smLogger = new Mock<ILogger<SignalCli.Services.SignalCli.ProcessStateManager>>();
        smLogger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var stateManager = new SignalCli.Services.SignalCli.ProcessStateManager(smLogger.Object);
        var service = CreateService(runnerMock, stateManager);

        service.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        service.StopAsync(CancellationToken.None).GetAwaiter().GetResult();

        // Lock free → Dispose() має взяти його і відпустити; після цього інший
        // consumer семафора має змогу взяти лок без очікування.
        service.Dispose();

        var lockField = typeof(SignalCli.Services.SignalCli.SignalCliHostedService)
            .GetField("_operationLock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var sem = (SemaphoreSlim)lockField!.GetValue(service)!;
        Assert.Equal(1, sem.CurrentCount);
    }
}
