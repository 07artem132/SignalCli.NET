using Microsoft.Extensions.Logging;

namespace SignalCli.Logging;

/// <summary>
/// C.6: source-generated логи для <see cref="Services.SignalCli.ProcessRunner"/>.
/// EventId-блок 900–959.
/// </summary>
internal static partial class ProcessRunnerLog
{
    [LoggerMessage(EventId = 900, Level = LogLevel.Debug, Message = "Запуск процесу: {Exe} {Args}")]
    public static partial void StartingProcess(ILogger logger, string? exe, string? args);

    [LoggerMessage(EventId = 901, Level = LogLevel.Information, Message = "Процес {Exe} запущено з PID {Pid}")]
    public static partial void ProcessStarted(ILogger logger, string? exe, int pid);

    [LoggerMessage(EventId = 902, Level = LogLevel.Error, Message = "Помилка запуску процесу {Exe}")]
    public static partial void StartFailed(ILogger logger, Exception ex, string? exe);
}
