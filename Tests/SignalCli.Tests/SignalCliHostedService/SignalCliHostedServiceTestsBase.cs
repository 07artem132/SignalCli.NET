using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models;
using SignalCli.Models.SignalCli;
using SignalCli.Services.SignalCli;

namespace SignalCli.Tests.SignalCliHostedService;

/// <summary>
/// Базовий клас для тестів SignalCliHostedService — спільна тестова інфраструктура.
/// </summary>
public abstract class SignalCliHostedServiceTestsBase : IDisposable
{
    protected readonly Mock<ILogger<Services.SignalCli.SignalCliHostedService>> LoggerMock;
    protected readonly Mock<IProcessRunner> ProcessRunnerMock;
    protected readonly ProcessStateManager StateManager;
    // deprecated-shim-removal §5: Config-shim видалено; працюємо напряму з SignalCliOptions.
    protected readonly SignalCliOptions Options;
    protected readonly string TempDir;
    protected int ProcessStartCallCount;

    protected SignalCliHostedServiceTestsBase()
    {
        LoggerMock = new Mock<ILogger<Services.SignalCli.SignalCliHostedService>>();
        // C.8: source-generated [LoggerMessage] методи спершу перевіряють IsEnabled —
        // інакше Verify(...x.Log...) показав би 0 викликів. У тестах вмикаємо всі рівні.
        LoggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        ProcessRunnerMock = new Mock<IProcessRunner>();

        // Подготовка ProcessRunner
        SetupProcessRunner();

        // Настройка ProcessStateManager
        var loggerSm = new Mock<ILogger<ProcessStateManager>>();
        loggerSm.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        StateManager = new ProcessStateManager(loggerSm.Object);

        // Створення тимчасової директорії для тестів
        TempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(TempDir);
        
        // Базовая конфигурация
        Options = CreateTestOptions();
        
        // Подготовка тестового окружения
        SetupTestEnvironment();
    }

    #region Setup Methods

    private void SetupProcessRunner()
    {
        ProcessRunnerMock
            .Setup(r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                ProcessStartCallCount++;

                var pMock = new Mock<IProcess>();
                pMock.Setup(p => p.Start(It.IsAny<CancellationToken>())).Returns(true);
                pMock.Setup(p => p.Id).Returns(ProcessStartCallCount);

                var stdIn = new StreamWriter(new MemoryStream());
                var stdOut = new StreamReader(new MemoryStream());
                var stdErr = new StreamReader(new MemoryStream());

                pMock.Setup(p => p.StandardInput).Returns(stdIn);
                pMock.Setup(p => p.StandardOutput).Returns(stdOut);
                pMock.Setup(p => p.StandardError).Returns(stdErr);

                var streams = new StreamPair(stdIn, stdOut, stdErr);
                return (pMock.Object, streams);
            });
    }

    private SignalCliOptions CreateTestOptions()
    {
        return new SignalCliOptions
        {
            AppHome = TempDir,
            LibDirectory = "lib",
            JavaExecutable = "java",
            MaxRestartAttempts = 2,
            RestartDelaySeconds = 0,
            StopTimeoutSeconds = 0,
            HealthCheckIntervalSeconds = 1,
            HealthCheckTimeoutSeconds = 1
        };
    }

    private void SetupTestEnvironment()
    {
        var libDir = Path.Combine(TempDir, "lib");
        Directory.CreateDirectory(libDir);
        File.WriteAllText(Path.Combine(libDir, "signal-cli.jar"), "fake jar content");
    }

    protected Services.SignalCli.SignalCliHostedService CreateService(TimeProvider? timeProvider = null)
    {
        // B.5/B.6: дозволяє тесту підставити FakeTimeProvider для віртуального часу.
        // deprecated-shim-removal §5: Options.Create() напряму на SignalCliOptions.
        return new Services.SignalCli.SignalCliHostedService(
            LoggerMock.Object,
            ProcessRunnerMock.Object,
            StateManager,
            Microsoft.Extensions.Options.Options.Create(Options),
            timeProvider
        );
    }

    // post-modernize-tuning §7.3 (T3): `GetPrivateField`/`SetPrivateField` reflection-helpers
    // прибрано — заміщені typed test-seam'ами `SignalCliHostedService.CurrentProcessForTests`
    // та `.CurrentStreamPairForTests` (internal-properties, видимі через InternalsVisibleTo).
    // Reflection-доступ був opaque: rename'и приватних полів мовчки повертали null замість
    // compile-error'у. Тепер контракт типовий.

    protected void VerifyLog(LogLevel level, string containsMessage)
    {
        LoggerMock.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(containsMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    #endregion

    public virtual void Dispose()
    {
        try
        {
            if (Directory.Exists(TempDir))
            {
                Directory.Delete(TempDir, true);
            }
        }
        catch (Exception)
        {
            // Ігноруємо помилки під час очищення у тестах
        }
    }
}