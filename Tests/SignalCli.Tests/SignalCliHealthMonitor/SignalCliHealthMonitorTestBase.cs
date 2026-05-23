using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Interfaces.Rpc;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models;
using SignalCli.Models.SignalCli;
using SignalCli.Services.SignalCli;

namespace SignalCli.Tests.SignalCliHealthMonitor;

public abstract class SignalCliHealthMonitorTestBase : IDisposable
{
    // Моки для HealthMonitor
    protected readonly Mock<ILogger<Services.SignalCli.SignalCliHealthMonitor>> LoggerMonitorMock;
    private readonly Mock<IJsonRpcClientProvider> _clientProviderMock;
    protected readonly Mock<IJsonRpcClient> JsonRpcClientMock;

    // Для HostedService
    private readonly Mock<ILogger<Services.SignalCli.SignalCliHostedService>> _loggerServiceMock;
    protected readonly Mock<IProcessRunner> ProcessRunnerMock;
    protected readonly ProcessStateManager StateManager;
    protected readonly Config ServiceConfig;

    //todo:
    //❌ Тест Dispose (зокрема повторний виклик)
    //❌ Перевірка поведінки при різних CancellationToken (при пінгу)
    //❌ Тест на таймаут пінга
    //❌ Тест на обробку неочікуваних винятків у MonitorLoop

    protected SignalCliHealthMonitorTestBase()
    {
        // 1. Логгер для HealthMonitor
        LoggerMonitorMock = new Mock<ILogger<Services.SignalCli.SignalCliHealthMonitor>>();
        // C.8: source-generated [LoggerMessage] методи спершу перевіряють IsEnabled.
        LoggerMonitorMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        // 2. Моки для JSON-RPC
        JsonRpcClientMock = new Mock<IJsonRpcClient>();
        // За замовчуванням — успішний "Ping"
        JsonRpcClientMock
            .Setup(c => c.InvokeMethodAsync<VersionParameters, VersionResponse>(
                It.IsAny<string>(),
                It.IsAny<VersionParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VersionResponse("test-version"));

        _clientProviderMock = new Mock<IJsonRpcClientProvider>();
        _clientProviderMock
            .Setup(p => p.Client)
            .Returns(JsonRpcClientMock.Object);

        // 3. Моки для HostedService
        _loggerServiceMock = new Mock<ILogger<Services.SignalCli.SignalCliHostedService>>();
        _loggerServiceMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        ProcessRunnerMock = new Mock<IProcessRunner>();

        // Настраиваем возвращение мок-процесса и стримов при запуске
        ProcessRunnerMock
            .Setup(r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var pMock = new Mock<IProcess>();
                pMock.Setup(p => p.Start(It.IsAny<CancellationToken>())).Returns(true);

                var stdIn = new StreamWriter(new MemoryStream());
                var stdOut = new StreamReader(new MemoryStream());
                var stdErr = new StreamReader(new MemoryStream());

                pMock.Setup(p => p.StandardInput).Returns(stdIn);
                pMock.Setup(p => p.StandardOutput).Returns(stdOut);
                pMock.Setup(p => p.StandardError).Returns(stdErr);

                var streams = new StreamPair(stdIn, stdOut, stdErr);
                return (pMock.Object, streams);
            });

        var loggerSmMock = new Mock<ILogger<ProcessStateManager>>();
        loggerSmMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        StateManager = new ProcessStateManager(loggerSmMock.Object);

        // 4. Конфиг для SignalCliHostedService
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        ServiceConfig = new Config
        {
            AppHome = tempDir,
            LibDirectory = "lib",
            JavaExecutable = "java",
            MaxRestartAttempts = 2,
            RestartDelaySeconds = 0, // Прискорюємо тести
            HealthCheckIntervalSeconds = 0,
            HealthCheckTimeoutSeconds = 1
        };

        // Створюємо структуру директорій для тестів
        var libDir = Path.Combine(tempDir, "lib");
        Directory.CreateDirectory(libDir);
        var fakeJarPath = Path.Combine(libDir, "test.jar");
        File.WriteAllText(fakeJarPath, "fake jar content");
    }

    // Допоміжні методи для створення об'єктів, що тестуються
    protected Services.SignalCli.SignalCliHostedService CreateHostedService()
    {
        return new Services.SignalCli.SignalCliHostedService(
            _loggerServiceMock.Object,
            ProcessRunnerMock.Object,
            StateManager,
            ServiceConfig.ToIOptions()
        );
    }

    protected Services.SignalCli.SignalCliHealthMonitor CreateMonitor(
        Services.SignalCli.SignalCliHostedService hostedService,
        TimeProvider? timeProvider = null)
    {
        // G.14: дозволяє конкретним тестам підставити FakeTimeProvider — без нього
        // монітор лишається у реальному часі (значення за замовчуванням TimeProvider.System).
        return new Services.SignalCli.SignalCliHealthMonitor(
            LoggerMonitorMock.Object,
            _clientProviderMock.Object,
            hostedService,
            ServiceConfig.ToIOptions(),
            timeProvider
        );
    }

    // Допоміжні методи для перевірки логів
    protected void VerifyInfoLog(string messageContains, Times? times = null)
    {
        times ??= Times.Once();

        LoggerMonitorMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(messageContains)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            times.Value);
    }

    protected void VerifyWarningLog(string messageContains, Times? times = null)
    {
        times ??= Times.Once();

        LoggerMonitorMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(messageContains)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            times.Value);
    }

    protected void VerifyErrorLog(string messageContains, Times? times = null)
    {
        times ??= Times.Once();

        LoggerMonitorMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(messageContains)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            times.Value);
    }

    public void Dispose()
    {
        try
        {
            // Очищаем временную директорию
            if (Directory.Exists(ServiceConfig.AppHome))
            {
                Directory.Delete(ServiceConfig.AppHome, recursive: true);
            }
        }
        catch
        {
            // Ігноруємо помилки під час очищення
        }
    }
}