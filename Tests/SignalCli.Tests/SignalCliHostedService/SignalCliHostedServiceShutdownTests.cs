using Moq;
using SignalCli.Interfaces.SignalCli;

namespace SignalCli.Tests.SignalCliHostedService;

[Trait("Category", "HostedService")]
[Collection("HostedService")]
public class SignalCliHostedServiceShutdownTests : SignalCliHostedServiceTestsBase
{
    [Fact]
    [Trait("Category", "Shutdown")]
    public async Task StopAsync_WhenProcessExitsGracefully_ShouldNotForceKill()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        var process = GetPrivateField<IProcess>(service, "_currentProcess")!;
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

        var process = GetPrivateField<IProcess>(service, "_currentProcess")!;
        var processMock = Mock.Get(process);
        // Процес не реагує на "exit" — лишається живим.
        processMock.Setup(p => p.HasExited).Returns(false);

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert: примусове завершення всього дерева процесів як останній засіб
        processMock.Verify(p => p.Kill(true), Times.Once);
    }
}
