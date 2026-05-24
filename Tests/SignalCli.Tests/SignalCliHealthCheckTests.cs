using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SignalCli.HealthChecks;
using SignalCli.Interfaces.Rpc;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models;
using SignalCli.Models.SignalCli;
// Alias to avoid collision with the `SignalCli.Tests.SignalCliHealthMonitor` folder-namespace.
using HealthMonitor = SignalCli.Services.SignalCli.SignalCliHealthMonitor;
using HostedService = SignalCli.Services.SignalCli.SignalCliHostedService;
using StateManager = SignalCli.Services.SignalCli.ProcessStateManager;

namespace SignalCli.Tests;

/// <summary>
/// post-modernize-tuning §11.C.6 (audit N3): SignalCliHealthCheck — поведінка по 4
/// фундаментальних шляхах: Running+ok-ping → Healthy, Running без ping → Degraded,
/// Stopping/Starting → Degraded, Failed/Stopped → Unhealthy (failureStatus).
/// </summary>
public class SignalCliHealthCheckTests
{
    private static (SignalCliHealthCheck check, StateManager state, HealthMonitor monitor) Build()
    {
        var stateLogger = Mock.Of<ILogger<StateManager>>();
        var state = new StateManager(stateLogger);

        var monitorLogger = Mock.Of<ILogger<HealthMonitor>>();
        var rpcProvider = new Mock<IJsonRpcClientProvider>();
        var hostedService = new HostedService(
            Mock.Of<ILogger<HostedService>>(),
            Mock.Of<IProcessRunner>(),
            state,
            Options.Create(new SignalCliOptions
            {
                AppHome = "/tmp", LibDirectory = "lib", JavaExecutable = "/usr/bin/java",
            }));
        var monitor = new HealthMonitor(
            monitorLogger,
            rpcProvider.Object,
            hostedService,
            Options.Create(new SignalCliOptions
            {
                AppHome = "/tmp", LibDirectory = "lib", JavaExecutable = "/usr/bin/java",
            }));

        var check = new SignalCliHealthCheck(state, monitor);
        return (check, state, monitor);
    }

    private static HealthCheckContext ContextWith(HealthStatus failureStatus = HealthStatus.Unhealthy)
    {
        return new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("signal-cli", Mock.Of<IHealthCheck>(), failureStatus, tags: null),
        };
    }

    [Fact]
    public async Task Running_NoPingYet_ReturnsDegraded()
    {
        var (check, state, _) = Build();
        state.UpdateState(ProcessState.Running);

        var result = await check.CheckHealthAsync(ContextWith());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("no successful ping", result.Description);
        Assert.Equal("Running", result.Data["state"]);
        Assert.Equal(false, result.Data["last_ping_ok"]);
        Assert.Equal("never", result.Data["last_ping_at"]);
    }

    [Fact]
    public async Task Failed_ReturnsUnhealthyByDefault()
    {
        var (check, state, _) = Build();
        state.UpdateState(ProcessState.Failed, error: new InvalidOperationException("test"));

        var result = await check.CheckHealthAsync(ContextWith());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Failed", result.Data["state"]);
    }

    [Fact]
    public async Task Stopped_RespectsFailureStatus()
    {
        var (check, state, _) = Build();
        state.UpdateState(ProcessState.Stopped);

        var result = await check.CheckHealthAsync(ContextWith(HealthStatus.Degraded));

        // Consumer choose Degraded as failure → Stopped reports Degraded, not Unhealthy.
        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task NotStarted_ReturnsDegraded()
    {
        var (check, _, _) = Build();
        // initial state is NotStarted

        var result = await check.CheckHealthAsync(ContextWith());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("NotStarted", result.Data["state"]);
    }

    [Fact]
    public async Task DataBag_DoesNotContainPii()
    {
        var (check, state, _) = Build();
        state.UpdateState(ProcessState.Running);

        var result = await check.CheckHealthAsync(ContextWith());

        // post-modernize-tuning §11.C.2 / §11.D.1: privacy invariant — data-bag має
        // лише process state + ping outcome + timestamp. Перевіряємо явно, що відомі
        // PII-маркери (phone, message body fragment, file path) ніде не з'являються.
        foreach (var kv in result.Data)
        {
            var value = kv.Value?.ToString() ?? "";
            Assert.DoesNotContain("+", value); // phone number marker
            Assert.DoesNotContain("@", value); // email marker
            Assert.DoesNotContain("/home/", value); // path marker (Linux)
            Assert.DoesNotContain("C:\\", value); // path marker (Windows)
        }
    }
}
