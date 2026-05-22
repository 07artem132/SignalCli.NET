using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Interfaces.Rpc;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models;
using SignalCli.Models.SignalCli;
using SignalCli.Services.Rpc;
using SignalCli.Services.SignalCli;

namespace SignalCli.Tests;

public class JsonRpcClientHostedServiceTests
{
    // Моки для JsonRpcClientHostedService
    private readonly Mock<ILogger<JsonRpcClientHostedService>> _loggerMock;
    private readonly Mock<IJsonRpcClientFactory> _clientFactoryMock;
    private readonly Mock<IJsonRpcClient> _clientMock;

    // Для реального SignalCliHostedService
    private readonly Mock<ILogger<Services.SignalCli.SignalCliHostedService>> _scsLoggerMock;
    private readonly Mock<IProcessRunner> _processRunnerMock;
    private readonly ProcessStateManager _stateManager;
    private readonly Config _config;

    public JsonRpcClientHostedServiceTests()
    {
        // 1) Моки для JsonRpcClientHostedService
        _loggerMock = new Mock<ILogger<JsonRpcClientHostedService>>();
        _clientFactoryMock = new Mock<IJsonRpcClientFactory>();
        _clientMock = new Mock<IJsonRpcClient>();

        // По умолчанию при создании фабрика отдаёт _clientMock.Object
        _clientFactoryMock
            .Setup(f => f.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_clientMock.Object);

        // Когда вызываем InvokeMethodAsync("version", ...)
        _clientMock
            .Setup(c => c.InvokeMethodAsync<VersionResponse, VersionParameters>(
                "version",
                It.IsAny<VersionParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VersionResponse("1.2.3-test"));

        // 2) Для реального SignalCliHostedService
        _scsLoggerMock = new Mock<ILogger<Services.SignalCli.SignalCliHostedService>>();
        _processRunnerMock = new Mock<IProcessRunner>();

        // Настраиваем, чтобы при запуске возвращался «фейковый» IProcess и StreamPair
        _processRunnerMock
            .Setup(r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var procMock = new Mock<IProcess>();
                procMock.Setup(p => p.Start(It.IsAny<CancellationToken>())).Returns(true);

                var stdIn = new StreamWriter(new MemoryStream());
                var stdOut = new StreamReader(new MemoryStream());
                var stdErr = new StreamReader(new MemoryStream());

                procMock.Setup(p => p.StandardInput).Returns(stdIn);
                procMock.Setup(p => p.StandardOutput).Returns(stdOut);
                procMock.Setup(p => p.StandardError).Returns(stdErr);

                var streams = new StreamPair(stdIn, stdOut, stdErr);
                return (procMock.Object, streams);
            });

        var loggerPsMock = new Mock<ILogger<ProcessStateManager>>();
        _stateManager = new ProcessStateManager(loggerPsMock.Object);

        // Конфиг, чтобы не упасть при BuildClasspath
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        _config = new Config
        {
            AppHome = tempDir,
            LibDirectory = "lib",
            JavaExecutable = "java",
            // Чтобы не ждали реальных секунд...
            RestartDelaySeconds = 0,
        };

        // Создадим папку lib + фейковый jar
        var libDir = Path.Combine(tempDir, "lib");
        Directory.CreateDirectory(libDir);
        File.WriteAllText(Path.Combine(libDir, "test1.jar"), "fake jar content");
    }

    /// <summary>
    /// Создаёт реальный SignalCliHostedService с моками внутри.
    /// </summary>
    private Services.SignalCli.SignalCliHostedService CreateSignalCliService()
    {
        return new Services.SignalCli.SignalCliHostedService(
            _scsLoggerMock.Object,
            _processRunnerMock.Object,
            _stateManager,
            _config
        );
    }

    /// <summary>
    /// Создаёт JsonRpcClientHostedService, принимая реальный SCS.
    /// </summary>
    private JsonRpcClientHostedService CreateJsonRpcClientHostedService(Services.SignalCli.SignalCliHostedService scs)
    {
        return new JsonRpcClientHostedService(
            _loggerMock.Object,
            _clientFactoryMock.Object,
            scs // Реальный SCS (не мок!)
        );
    }

    [Fact]
    public async Task StartAsync_ShouldWaitSCSReady_CreateClient_AndInvokeVersion()
    {
        // Arrange
        var scs = CreateSignalCliService();
        // Запускаем SCS → он создаст процесс и станет "Running"
        await scs.StartAsync(CancellationToken.None);

        var rpcService = CreateJsonRpcClientHostedService(scs);

        // Act
        // Запускаем JsonRpcClientHostedService -> подождёт WaitForReadyAsync
        await rpcService.StartAsync(CancellationToken.None);

        // Assert
        // 1) Проверим, что фабрика создала клиент
        _clientFactoryMock.Verify(f => f.CreateAsync(It.IsAny<CancellationToken>()), Times.Once);

        // 2) Проверим вызов "version"
        _clientMock.Verify(c => c.InvokeMethodAsync<VersionResponse, VersionParameters>(
                "version",
                It.IsAny<VersionParameters>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // 3) Убедимся, что свойство Client теперь не null
        Assert.NotNull(rpcService.Client);

        // Также можно проверить логи
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>(
                    (v, t) => v.ToString().Contains("JsonRpcClientHostedService запущено - клієнт готовий")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once
        );
    }

    [Fact]
    public async Task StopAsync_ShouldDisposeClient_AndMakeItNull()
    {
        // Arrange
        var scs = CreateSignalCliService();
        await scs.StartAsync(CancellationToken.None);

        var rpcService = CreateJsonRpcClientHostedService(scs);
        await rpcService.StartAsync(CancellationToken.None);

        // Act
        await rpcService.StopAsync(CancellationToken.None);

        // Assert
        // Клиент должен освободиться
        _clientMock.Verify(c => c.Dispose(), Times.Once);

        // Попытка получить rpcService.Client после StopAsync -> InvalidOperationException
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            // При обращении к Client выбросит
            var _ = rpcService.Client;
            await Task.Yield();
        });
    }

    [Fact]
    public async Task StartAsync_IfServiceDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var scs = CreateSignalCliService();
        await scs.StartAsync(CancellationToken.None);

        var rpcService = CreateJsonRpcClientHostedService(scs);
        // Асинхронный Dispose
        await rpcService.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            rpcService.StartAsync(CancellationToken.None)
        );
    }

    [Fact]
    public async Task StopAsync_IfServiceDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var scs = CreateSignalCliService();
        await scs.StartAsync(CancellationToken.None);

        var rpcService = CreateJsonRpcClientHostedService(scs);
        await rpcService.StartAsync(CancellationToken.None);

        // Dispose
        await rpcService.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            rpcService.StopAsync(CancellationToken.None)
        );
    }
}