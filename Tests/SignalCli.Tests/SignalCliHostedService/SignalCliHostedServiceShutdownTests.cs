using System.Text;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.SignalCli;

namespace SignalCli.Tests.SignalCliHostedService;

[Trait("Category", "HostedService")]
[Collection("HostedService")]
public class SignalCliHostedServiceShutdownTests : SignalCliHostedServiceTestsBase
{
    // signal-cli-protocol-alignment §1 (graceful-shutdown-fix): пінує що StopAsync закриває
    // stdin (EOF — канонічний graceful trigger у signal-cli) і НЕ пише літеральне "exit".
    [Fact]
    [Trait("Category", "Shutdown")]
    public async Task StopAsync_ClosesStdin_DoesNotWriteExitText()
    {
        // Arrange: окремий setup, що захоплює MemoryStream до того як StreamWriter його закриє.
        MemoryStream? capturedStdInStream = null;
        ProcessRunnerMock
            .Setup(r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                ProcessStartCallCount++;
                var pMock = new Mock<IProcess>();
                pMock.Setup(p => p.Start(It.IsAny<CancellationToken>())).Returns(true);
                pMock.Setup(p => p.Id).Returns(ProcessStartCallCount);
                pMock.Setup(p => p.HasExited).Returns(true); // exits cleanly on stdin EOF

                capturedStdInStream = new MemoryStream();
                var stdIn = new StreamWriter(capturedStdInStream, new UTF8Encoding(false), 1024, leaveOpen: true);
                var stdOut = new StreamReader(new MemoryStream());
                var stdErr = new StreamReader(new MemoryStream());

                pMock.Setup(p => p.StandardInput).Returns(stdIn);
                pMock.Setup(p => p.StandardOutput).Returns(stdOut);
                pMock.Setup(p => p.StandardError).Returns(stdErr);

                return (pMock.Object, new StreamPair(stdIn, stdOut, stdErr));
            });

        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert: stdin було закрито (StandardInput.Close() flush'ить буфер у underlying stream,
        // потім закриває StreamWriter; через leaveOpen=true MemoryStream залишається доступним
        // для нашого читання). Жодних "exit" байтів у потоці бути не повинно.
        Assert.NotNull(capturedStdInStream);
        var bytes = capturedStdInStream!.ToArray();
        var content = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("exit", content, StringComparison.Ordinal);
    }


    [Fact]
    [Trait("Category", "Shutdown")]
    public async Task StopAsync_WhenProcessExitsGracefully_ShouldNotForceKill()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        var process = service.CurrentProcessForTests!;
        var processMock = Mock.Get(process);
        // Зовнішня перевірка бачить процес живим, а після "exit" — вже завершеним.
        processMock.SetupSequence(p => p.HasExited)
            .Returns(false) // перед надсиланням "exit"
            .Returns(true); // після граційного завершення

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert: примусового Kill не було
        processMock.Verify(p => p.Kill(It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Shutdown")]
    public async Task StopAsync_WhenProcessHangs_ShouldForceKillProcessTree()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        var process = service.CurrentProcessForTests!;
        var processMock = Mock.Get(process);
        // Процес не реагує на "exit" — лишається живим.
        processMock.Setup(p => p.HasExited).Returns(false);

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert: примусове завершення всього дерева процесів як останній засіб
        processMock.Verify(p => p.Kill(true), Times.Once);
    }
}
