using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.SignalCli;
using SignalCli.Services.SignalCli;

namespace SignalCli.Tests;

public class ProcessRunnerTests
{
    [Fact]
    public async Task StartProcessWithHandle_ConfigIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<ProcessRunner>>();
        var processFactoryMock = new Mock<IProcessFactory>();
        var runner = new ProcessRunner(loggerMock.Object, processFactoryMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            runner.StartProcessWithHandle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task StartProcessWithHandle_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<ProcessRunner>>();
        var processFactoryMock = new Mock<IProcessFactory>();
        var runner = new ProcessRunner(loggerMock.Object, processFactoryMock.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Чтобы сразу был requested

        var config = new ProcessConfig
        {
            Executable = "echo",
            Arguments = "test"
        };

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            runner.StartProcessWithHandle(config, cts.Token));
    }

    [Fact]
    public async Task StartProcessWithHandle_ProcessStartFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<ProcessRunner>>();

        var mockProcess = new Mock<IProcess>();
        // При вызове Start() вернём false
        mockProcess.Setup(p => p.Start(It.IsAny<CancellationToken>())).Returns(false);

        var processFactoryMock = new Mock<IProcessFactory>();
        processFactoryMock
            .Setup(f => f.CreateProcess(It.IsAny<ProcessStartInfo>()))
            .Returns(mockProcess.Object);

        var runner = new ProcessRunner(loggerMock.Object, processFactoryMock.Object);

        var config = new ProcessConfig
        {
            Executable = "someExecutable",
            Arguments = "someArgs"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await runner.StartProcessWithHandle(config)
        );

        // Дополнительно можно проверить, что Dispose был вызван в случае ошибки
        mockProcess.Verify(p => p.Dispose(), Times.Once);
    }

    [Fact]
    public async Task StartProcessWithHandle_StartsProcess_ReturnsProcessAndStreams()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<ProcessRunner>>();

        var mockProcess = new Mock<IProcess>();
        // Допустим, Start() вернёт true.
        mockProcess.Setup(p => p.Start(It.IsAny<CancellationToken>())).Returns(true);
        // Имитация PID
        mockProcess.Setup(p => p.Id).Returns(123);

        // В реальном процессе это будут реальные stream'ы, а тут можно в тестах подделать
        // чтобы проверить сам факт передачи. Например, создать MemoryStream или оставить null.
        var fakeStdIn = new StreamWriter(new MemoryStream());
        var fakeStdOut = new StreamReader(new MemoryStream());
        var fakeStdErr = new StreamReader(new MemoryStream());

        mockProcess.Setup(p => p.StandardInput).Returns(fakeStdIn);
        mockProcess.Setup(p => p.StandardOutput).Returns(fakeStdOut);
        mockProcess.Setup(p => p.StandardError).Returns(fakeStdErr);

        var processFactoryMock = new Mock<IProcessFactory>();
        processFactoryMock
            .Setup(f => f.CreateProcess(It.IsAny<ProcessStartInfo>()))
            .Returns(mockProcess.Object);

        var runner = new ProcessRunner(loggerMock.Object, processFactoryMock.Object);

        var config = new ProcessConfig
        {
            Executable = "someExecutable",
            Arguments = "someArgs",
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["VAR1"] = "VALUE1"
            },
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // Act
        var (process, streams) = await runner.StartProcessWithHandle(config);

        // Assert
        Assert.NotNull(process);
        Assert.NotNull(streams);
        Assert.Equal(fakeStdIn, streams.StandardInput);
        Assert.Equal(fakeStdOut, streams.StandardOutput);
        Assert.Equal(fakeStdErr, streams.StandardError);

        // Проверим, что метод Start() действительно вызван
        mockProcess.Verify(p => p.Start(It.IsAny<CancellationToken>()), Times.Once);

        // Проверим, что логгер был вызван с нужным сообщением
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Запуск процесу")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once
        );

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Процес someExecutable запущено з PID 123")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once
        );
    }

    [Fact]
    public async Task StartProcessWithHandle_SetsEnvironmentVariables_CreatesProcessWithCorrectEnv_AndRedirects()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<ProcessRunner>>();

        var mockProcess = new Mock<IProcess>();
        mockProcess.Setup(p => p.Start(It.IsAny<CancellationToken>())).Returns(true);

        // Подделываем стримы, которые не будут null
        var fakeStdIn = new StreamWriter(new MemoryStream());
        var fakeStdOut = new StreamReader(new MemoryStream());
        var fakeStdErr = new StreamReader(new MemoryStream());

        mockProcess.Setup(p => p.StandardInput).Returns(fakeStdIn);
        mockProcess.Setup(p => p.StandardOutput).Returns(fakeStdOut);
        mockProcess.Setup(p => p.StandardError).Returns(fakeStdErr);

        ProcessStartInfo capturedPsi = null!;
        var processFactoryMock = new Mock<IProcessFactory>();
        processFactoryMock
            .Setup(f => f.CreateProcess(It.IsAny<ProcessStartInfo>()))
            .Callback<ProcessStartInfo>(psi => capturedPsi = psi)
            .Returns(mockProcess.Object);

        var runner = new ProcessRunner(loggerMock.Object, processFactoryMock.Object);

        // Здесь включаем редирект, чтобы Streams не были null
        var config = new ProcessConfig
        {
            Executable = "someExecutable",
            Arguments = "someArgs",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["VAR1"] = "VALUE1"
            }
        };

        // Act
        var (process, streams) = await runner.StartProcessWithHandle(config);

        // Assert: Проверяем Environment
        Assert.Equal("VALUE1", capturedPsi.Environment["VAR1"]);

        // Проверяем, что потоки не null
        Assert.NotNull(streams);
        Assert.Same(fakeStdIn, streams.StandardInput);
        Assert.Same(fakeStdOut, streams.StandardOutput);
        Assert.Same(fakeStdErr, streams.StandardError);
    }
}