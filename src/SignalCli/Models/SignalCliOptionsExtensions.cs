using Microsoft.Extensions.Options;
using SignalCli.Models.SignalCli;

namespace SignalCli.Models;

/// <summary>
/// Internal-розширення для <see cref="SignalCliOptions"/> та конвертації з legacy <see cref="Config"/>.
/// </summary>
/// <remarks>
/// D.4: реалізує побудову <see cref="ProcessConfig"/> із типованих опцій. Внутрішньо
/// делегує legacy <see cref="Config.ToProcessConfig"/> через <see cref="SignalCliOptions.ToConfig"/>
/// — щоб логіка побудови команди (native vs JVM, classpath, env) лишалась в одному місці.
/// </remarks>
internal static class SignalCliOptionsExtensions
{
    /// <summary>Будує <see cref="ProcessConfig"/> для запуску signal-cli з опцій.</summary>
    public static ProcessConfig ToProcessConfig(this SignalCliOptions options)
        => options.ToConfig().ToProcessConfig();

    /// <summary>
    /// Конвертує legacy <see cref="Config"/> у нові <see cref="SignalCliOptions"/> (поле-в-поле).
    /// </summary>
    public static SignalCliOptions ToOptions(this Config c) => new()
    {
        AppHome = c.AppHome,
        LibDirectory = c.LibDirectory,
        JavaExecutable = c.JavaExecutable,
        SignalCliExecutable = c.SignalCliExecutable,
        CliLogLevelCli = c.CliLogLevelCli,
        LogFileCli = c.LogFileCli,
        StoragePathCli = c.StoragePathCli,
        UseManualReceiveMode = c.UseManualReceiveMode,
        MaxRestartAttempts = c.MaxRestartAttempts,
        HealthCheckIntervalSeconds = c.HealthCheckIntervalSeconds,
        HealthCheckTimeoutSeconds = c.HealthCheckTimeoutSeconds,
        RestartDelaySeconds = c.RestartDelaySeconds,
        StopTimeoutSeconds = c.StopTimeoutSeconds,
        RequestTimeoutSeconds = c.RequestTimeoutSeconds,
        RestartWindowSeconds = c.RestartWindowSeconds,
        EnvironmentVariables = c.EnvironmentVariables,
    };

    /// <summary>Обгортає legacy <see cref="Config"/> в <see cref="IOptions{TOptions}"/>.</summary>
    public static IOptions<SignalCliOptions> ToIOptions(this Config c) => Options.Create(c.ToOptions());
}
