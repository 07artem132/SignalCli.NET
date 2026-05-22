using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models;
using SignalCli.Models.SignalCli;
using SignalCli.Services.SignalCli;

namespace SignalCli.Tests.SignalCliHostedService;

/// <summary>
/// Базовый класс для тестов SignalCliHostedService, содержащий общую инфраструктуру
/// </summary>
public abstract class SignalCliHostedServiceTestsBase : IDisposable
{
    protected readonly Mock<ILogger<Services.SignalCli.SignalCliHostedService>> LoggerMock;
    protected readonly Mock<IProcessRunner> ProcessRunnerMock;
    protected readonly ProcessStateManager StateManager;
    protected readonly Config Config;
    protected readonly string TempDir;
    protected int ProcessStartCallCount;

    protected SignalCliHostedServiceTestsBase()
    {
        LoggerMock = new Mock<ILogger<Services.SignalCli.SignalCliHostedService>>();
        ProcessRunnerMock = new Mock<IProcessRunner>();

        // Подготовка ProcessRunner
        SetupProcessRunner();

        // Настройка ProcessStateManager
        var loggerSm = new Mock<ILogger<ProcessStateManager>>();
        StateManager = new ProcessStateManager(loggerSm.Object);

        // Создание временной директории для тестов
        TempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(TempDir);
        
        // Базовая конфигурация
        Config = CreateTestConfig();
        
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

    private Config CreateTestConfig()
    {
        return new Config
        {
            AppHome = TempDir,
            LibDirectory = "lib",
            JavaExecutable = "java",
            MaxRestartAttempts = 2,
            RestartDelaySeconds = 0,
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

    protected Services.SignalCli.SignalCliHostedService CreateService()
    {
        return new Services.SignalCli.SignalCliHostedService(
            LoggerMock.Object,
            ProcessRunnerMock.Object,
            StateManager,
            Config
        );
    }

    protected static T? GetPrivateField<T>(object obj, string fieldName) where T : class
    {
        var fieldInfo = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        return fieldInfo?.GetValue(obj) as T;
    }

    protected static async Task SetPrivateField(object obj, string fieldName, object value)
    {
        var fieldInfo = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        fieldInfo?.SetValue(obj, value);
    }

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
            // Игнорируем ошибки при очистке в тестах
        }
    }
}