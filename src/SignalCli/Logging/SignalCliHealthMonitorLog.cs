using Microsoft.Extensions.Logging;

namespace SignalCli.Logging;

/// <summary>
/// C.2: source-generated логи для <see cref="Services.SignalCli.SignalCliHealthMonitor"/>.
/// EventId-блок 200–299.
/// </summary>
internal static partial class SignalCliHealthMonitorLog
{
    [LoggerMessage(EventId = 200, Level = LogLevel.Error,
        Message = "StartAsync викликано коли цикл вже працює, зупиніть монітор та викличте StartAsync.")]
    public static partial void AlreadyStarted(ILogger logger);

    [LoggerMessage(EventId = 201, Level = LogLevel.Information, Message = "SignalCliHealthMonitor запускається...")]
    public static partial void StartBegin(ILogger logger);

    [LoggerMessage(EventId = 202, Level = LogLevel.Information, Message = "SignalCliHealthMonitor запущено.")]
    public static partial void Started(ILogger logger);

    [LoggerMessage(EventId = 203, Level = LogLevel.Information, Message = "SignalCliHealthMonitor зупиняється...")]
    public static partial void StopBegin(ILogger logger);

    [LoggerMessage(EventId = 204, Level = LogLevel.Information, Message = "SignalCliHealthMonitor зупинено.")]
    public static partial void Stopped(ILogger logger);

    [LoggerMessage(EventId = 205, Level = LogLevel.Information, Message = "Цикл моніторингу запущено в SignalCliHealthMonitor")]
    public static partial void LoopStarted(ILogger logger);

    [LoggerMessage(EventId = 206, Level = LogLevel.Warning, Message = "Signal CLI не відповідає. Запускаємо перезапуск...")]
    public static partial void RestartTriggered(ILogger logger);

    [LoggerMessage(EventId = 207, Level = LogLevel.Error, Message = "Неочікувана помилка в циклі моніторингу")]
    public static partial void LoopIterationFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 208, Level = LogLevel.Information, Message = "Цикл моніторингу завершено в SignalCliHealthMonitor")]
    public static partial void LoopFinished(ILogger logger);

    [LoggerMessage(EventId = 209, Level = LogLevel.Debug,
        Message = "PingCliAsync: немає поточного StreamPair => CLI не готовий")]
    public static partial void PingNoStreamPair(ILogger logger);

    [LoggerMessage(EventId = 210, Level = LogLevel.Debug, Message = "PingCliAsync: CLI відповів з версією={Version}")]
    public static partial void PingOk(ILogger logger, string? version);

    [LoggerMessage(EventId = 211, Level = LogLevel.Error, Message = "PingCliAsync: пінг CLI невдалий")]
    public static partial void PingFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 212, Level = LogLevel.Error, Message = "Signal CLI не відповідає")]
    public static partial void PingTimedOut(ILogger logger, Exception ex);
}
