using Microsoft.Extensions.Diagnostics.HealthChecks;
using SignalCli.Models.SignalCli;
using SignalCli.Services.SignalCli;

namespace SignalCli.HealthChecks;

/// <summary>
/// post-modernize-tuning §11.C (audit N3): <see cref="IHealthCheck"/>-адаптер для
/// signal-cli process state. Підключається через
/// <see cref="SignalCliHealthCheckServiceCollectionExtensions.AddSignalCliHealthCheck"/>;
/// сам не тягне залежності у core <c>SignalCli.NET</c> — це окремий optional package.
/// </summary>
/// <remarks>
/// <para>
/// <b>Кожен стан → HealthStatus.</b>
/// </para>
/// <list type="table">
///   <listheader><term><see cref="ProcessState"/></term><description>Result</description></listheader>
///   <item><term><c>Running</c> + останній ping ≤ 2× HealthCheckIntervalSeconds тому</term><description><see cref="HealthStatus.Healthy"/></description></item>
///   <item><term><c>Running</c>, але ping ще не виконувався або стрейлий</term><description><see cref="HealthStatus.Degraded"/></description></item>
///   <item><term><c>Starting</c> / <c>Stopping</c> / <c>NotStarted</c></term><description><see cref="HealthStatus.Degraded"/></description></item>
///   <item><term><c>Failed</c> / <c>Stopped</c></term><description><see cref="HealthStatus.Unhealthy"/></description></item>
/// </list>
/// <para>
/// <b>Privacy invariant (CLAUDE.md rule #1).</b> <c>data</c>-bag НЕ містить акаунти,
/// тіло повідомлень чи шляхи до файлів — лише process state (enum), час останнього
/// ping (UTC ISO-8601), boolean outcome. Це безпечно для публічного <c>/healthz</c>.
/// </para>
/// </remarks>
public sealed class SignalCliHealthCheck : IHealthCheck
{
    private readonly ProcessStateManager _stateManager;
    private readonly SignalCliHealthMonitor _monitor;

    /// <summary>
    /// Створює адаптер. Реєструється DI-контейнером через
    /// <see cref="SignalCliHealthCheckServiceCollectionExtensions.AddSignalCliHealthCheck"/>.
    /// </summary>
    /// <param name="stateManager">Менеджер стану процесу signal-cli — джерело істини.</param>
    /// <param name="monitor">Health-monitor — джерело <c>LastPingResult</c>.</param>
    public SignalCliHealthCheck(ProcessStateManager stateManager, SignalCliHealthMonitor monitor)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var state = _stateManager.CurrentState;
        var lastPing = _monitor.LastPingResult;

        var data = new Dictionary<string, object>
        {
            ["state"] = state.ToString(),
            ["last_ping_ok"] = lastPing?.Ok ?? false,
            ["last_ping_at"] = lastPing?.At.ToString("O") ?? "never",
        };

        return Task.FromResult(state switch
        {
            ProcessState.Running when lastPing is { Ok: true } =>
                HealthCheckResult.Healthy("signal-cli running, last ping ok", data),
            ProcessState.Running =>
                HealthCheckResult.Degraded("signal-cli running but no successful ping recorded", data: data),
            ProcessState.Starting or ProcessState.Stopping or ProcessState.NotStarted =>
                HealthCheckResult.Degraded($"signal-cli is in state {state}", data: data),
            ProcessState.Failed or ProcessState.Stopped =>
                new HealthCheckResult(
                    status: context.Registration.FailureStatus,
                    description: $"signal-cli is in state {state}",
                    exception: null,
                    data: data),
            _ => HealthCheckResult.Degraded($"signal-cli state unknown ({state})", data: data),
        });
    }
}
