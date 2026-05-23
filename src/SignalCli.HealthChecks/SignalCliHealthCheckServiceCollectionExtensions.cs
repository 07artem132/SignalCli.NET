using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SignalCli.HealthChecks;

/// <summary>
/// post-modernize-tuning §11.C.3 (audit N3): canonical extension-method для реєстрації
/// <see cref="SignalCliHealthCheck"/> через <see cref="IHealthChecksBuilder"/> — за
/// Microsoft pattern <c>Distribute a health check library</c>.
/// </summary>
public static class SignalCliHealthCheckServiceCollectionExtensions
{
    /// <summary>
    /// Реєструє <see cref="SignalCliHealthCheck"/> у DI-контейнері. Викликати після
    /// <c>services.AddSignalCli(...)</c>.
    /// </summary>
    /// <param name="builder"><see cref="IHealthChecksBuilder"/> з ASP.NET Core hosting.</param>
    /// <param name="name">Ім'я health-check для агрегації; за замовчуванням <c>"signal-cli"</c>.</param>
    /// <param name="failureStatus">
    /// Статус, що повертається при <see cref="SignalCli.Models.SignalCli.ProcessState.Failed"/>/<c>Stopped</c>.
    /// За замовчуванням — <see cref="HealthStatus.Unhealthy"/>.
    /// </param>
    /// <param name="tags">Опціональні теги для агрегації.</param>
    /// <returns>Той самий <paramref name="builder"/> для chain-конфігурації.</returns>
    /// <remarks>
    /// <para>
    /// <b>Залежність — лише generic-host.</b> Пакет <c>SignalCli.NET.HealthChecks</c>
    /// тягне тільки <c>Microsoft.Extensions.Diagnostics.HealthChecks</c> — це частина
    /// generic-host infrastructure, працює всюди де є <c>IHost</c> (console worker,
    /// daemon, ASP.NET Core). ASP.NET НЕ потрібен для самого health-check'а.
    /// </para>
    /// <para>
    /// Для HTTP-endpoint <c>/healthz</c> consumer додає окремий пакет
    /// <c>Microsoft.AspNetCore.Diagnostics.HealthChecks</c> і <c>app.MapHealthChecks("/healthz")</c>
    /// у своєму <c>Program.cs</c> — це його вибір, не наша залежність.
    /// </para>
    /// </remarks>
    /// <example>
    /// ASP.NET Core consumer:
    /// <code>
    /// builder.Services.AddSignalCli(opts =&gt; { /* ... */ });
    /// builder.Services.AddHealthChecks()
    ///     .AddSignalCliHealthCheck();
    /// // Тільки для ASP.NET: окремий пакет Microsoft.AspNetCore.Diagnostics.HealthChecks
    /// app.MapHealthChecks("/healthz");
    /// </code>
    /// Generic-host (worker, daemon — БЕЗ ASP.NET):
    /// <code>
    /// var host = Host.CreateDefaultBuilder()
    ///     .ConfigureServices(s =&gt;
    ///     {
    ///         s.AddSignalCli(opts =&gt; { /* ... */ });
    ///         s.AddHealthChecks().AddSignalCliHealthCheck();
    ///     }).Build();
    /// var hc = host.Services.GetRequiredService&lt;HealthCheckService&gt;();
    /// var report = await hc.CheckHealthAsync();
    /// </code>
    /// </example>
    public static IHealthChecksBuilder AddSignalCliHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "signal-cli",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        // AddCheck<T>(name, failureStatus, tags) приймає non-nullable `IEnumerable<string>`;
        // нормалізуємо null → empty щоб залишити caller-API nullable-friendly.
        return builder.AddCheck<SignalCliHealthCheck>(name, failureStatus, tags ?? []);
    }
}
