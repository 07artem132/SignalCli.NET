using Microsoft.Extensions.Logging;
using SignalCli.Models.SignalCli;

namespace SignalCli.Logging;

/// <summary>
/// C.1: source-generated логи для <see cref="Services.SignalCli.SignalCliHostedService"/>.
/// EventId-блок 100–199.
/// </summary>
internal static partial class SignalCliHostedServiceLog
{
    [LoggerMessage(EventId = 100, Level = LogLevel.Information, Message = "SignalCliHostedService запускається...")]
    public static partial void StartBegin(ILogger logger);

    [LoggerMessage(EventId = 101, Level = LogLevel.Information, Message = "SignalCliHostedService успішно запущено.")]
    public static partial void Started(ILogger logger);

    [LoggerMessage(EventId = 102, Level = LogLevel.Error, Message = "Помилка запуску SignalCliHostedService")]
    public static partial void StartFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 103, Level = LogLevel.Information, Message = "SignalCliHostedService зупиняється...")]
    public static partial void StopBegin(ILogger logger);

    [LoggerMessage(EventId = 104, Level = LogLevel.Information, Message = "SignalCliHostedService зупинено.")]
    public static partial void Stopped(ILogger logger);

    [LoggerMessage(EventId = 105, Level = LogLevel.Error, Message = "Помилка зупинки SignalCliHostedService")]
    public static partial void StopFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 106, Level = LogLevel.Information,
        Message = "StopProcessInternalAsync: поточний стан = {State}, пропускаємо зупинку.")]
    public static partial void StopProcessSkipped(ILogger logger, ProcessState state);

    [LoggerMessage(EventId = 107, Level = LogLevel.Warning,
        Message = "ForceRestartAsync викликано, але сервіс утилізовано або зупиняється.")]
    public static partial void ForceRestartIgnored(ILogger logger);

    [LoggerMessage(EventId = 108, Level = LogLevel.Warning,
        Message = "ForceRestartAsync: сервіс було утилізовано/зупинено під час очікування блокування.")]
    public static partial void ForceRestartLockLost(ILogger logger);

    [LoggerMessage(EventId = 109, Level = LogLevel.Information,
        Message = "ForceRestartAsync: поточний стан = {State}, пропускаємо примусовий перезапуск.")]
    public static partial void ForceRestartSkipped(ILogger logger, ProcessState state);

    [LoggerMessage(EventId = 110, Level = LogLevel.Error,
        Message = "ForceRestartAsync: перевищено MaxRestartAttempts ({Max}) у вікні {Window}c. Скасування перезапуску.")]
    public static partial void ForceRestartBudgetExceeded(ILogger logger, int max, int window);

    [LoggerMessage(EventId = 111, Level = LogLevel.Warning,
        Message = "Примусовий перезапуск SignalCLI (спроба #{Count}/{Max})")]
    public static partial void ForceRestartAttempt(ILogger logger, int count, int max);

    [LoggerMessage(EventId = 112, Level = LogLevel.Warning, Message = "Неможливо запустити у стані {State}")]
    public static partial void CannotStartInState(ILogger logger, ProcessState state);

    [LoggerMessage(EventId = 113, Level = LogLevel.Information, Message = "Запуск процесу Signal CLI...")]
    public static partial void StartingProcess(ILogger logger);

    [LoggerMessage(EventId = 114, Level = LogLevel.Information, Message = "Процес Signal CLI запущено (PID={Pid})")]
    public static partial void ProcessStarted(ILogger logger, int pid);

    [LoggerMessage(EventId = 115, Level = LogLevel.Error, Message = "Не вдалося запустити процес Signal CLI")]
    public static partial void ProcessStartFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 116, Level = LogLevel.Information, Message = "Зупинка процесу Signal CLI...")]
    public static partial void StoppingProcess(ILogger logger);

    // signal-cli-protocol-alignment §1: stdin EOF (Close) — це канонічний graceful-shutdown
    // trigger у signal-cli (JsonRpcReader.java:59-75 повертає null при EOF, JsonRpcDispatcher
    // у finally чистить підписки, JVM exit clean). Раніше тут писалося літеральне "exit" —
    // signal-cli парсить кожен stdin-рядок як JSON, отримував -32700 Parse error, процес
    // лишався живим, і ми завжди falled through до Kill(entireProcessTree:true).
    [LoggerMessage(EventId = 117, Level = LogLevel.Debug, Message = "Не вдалося закрити stdin — перейдемо до wait/kill")]
    public static partial void StdinCloseFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 118, Level = LogLevel.Warning,
        Message = "Процес не завершився за {Timeout}c, примусово завершуємо його...")]
    public static partial void ProcessKillTimeout(ILogger logger, int timeout);

    [LoggerMessage(EventId = 119, Level = LogLevel.Information, Message = "Процес Signal CLI зупинено.")]
    public static partial void ProcessStopped(ILogger logger);

    [LoggerMessage(EventId = 120, Level = LogLevel.Error, Message = "Помилка зупинки Signal CLI")]
    public static partial void ProcessStopFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 121, Level = LogLevel.Information,
        Message = "Процес стабільно у Running {Window}c — скидаю _restartCount={Count} у 0")]
    public static partial void RestartCountReset(ILogger logger, int window, int count);

    [LoggerMessage(EventId = 122, Level = LogLevel.Error,
        Message = "Помилка у колбеку таймера вікна стабільності рестартів")]
    public static partial void RestartWindowCallbackFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 123, Level = LogLevel.Error,
        Message = "Незловлений виняток у OnProcessExited — проігноровано, щоб не зривати хост.")]
    public static partial void OnExitedUnhandled(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 124, Level = LogLevel.Debug, Message = "OnProcessExited: процес уже зупинено навмисно — пропускаємо.")]
    public static partial void OnExitedAlreadyStopped(ILogger logger);

    [LoggerMessage(EventId = 125, Level = LogLevel.Warning, Message = "Процес Signal CLI завершився неочікувано.")]
    public static partial void ProcessExitedUnexpectedly(ILogger logger);

    [LoggerMessage(EventId = 126, Level = LogLevel.Warning, Message = "Автоперезапуск вимкнено (MaxRestartAttempts=0).")]
    public static partial void AutoRestartDisabled(ILogger logger);

    [LoggerMessage(EventId = 127, Level = LogLevel.Error,
        Message = "Досягнуто максимальну кількість перезапусків ({Max}) у вікні {Window}c.")]
    public static partial void AutoRestartBudgetExceeded(ILogger logger, int max, int window);

    [LoggerMessage(EventId = 128, Level = LogLevel.Information, Message = "Спроба автоперезапуску (#{Count}/{Max})...")]
    public static partial void AutoRestartAttempt(ILogger logger, int count, int max);

    [LoggerMessage(EventId = 129, Level = LogLevel.Error, Message = "Не вдалося перезапустити процес Signal CLI.")]
    public static partial void AutoRestartFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 130, Level = LogLevel.Information, Message = "Disposing SignalCliHostedService...")]
    public static partial void Disposing(ILogger logger);

    [LoggerMessage(EventId = 131, Level = LogLevel.Error, Message = "Помилка при disposing SignalCliHostedService")]
    public static partial void DisposeFailed(ILogger logger, Exception ex);

    // post-modernize-tuning §8a.3 (audit C1): DisposeAsync не зміг dren'ити _operationLock
    // за 2с — переходимо до sync-cleanup'у без drain'у.
    [LoggerMessage(EventId = 132, Level = LogLevel.Warning,
        Message = "DisposeAsync: 2с-drain-window закінчилось, in-flight operation не завершилась — продовжуємо sync-cleanup.")]
    public static partial void DisposeAsyncDrainTimeout(ILogger logger);
}
