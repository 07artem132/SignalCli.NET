using Microsoft.Extensions.Logging;

namespace SignalCli.Logging;

/// <summary>
/// C.5: source-generated логи для <see cref="Services.Signal.SignalDevices"/>.
/// EventId-блок 820–829.
/// </summary>
internal static partial class SignalDevicesLog
{
    [LoggerMessage(EventId = 820, Level = LogLevel.Error, Message = "Помилка запуску процесу зв'язування пристрою")]
    public static partial void StartLinkFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 821, Level = LogLevel.Error, Message = "Помилка завершення процесу зв'язування пристрою")]
    public static partial void FinishLinkFailed(ILogger logger, Exception ex);

    // post-modernize-tuning §8c.13 (audit N16): entry-level logs symmetric to ListAccounts/ListGroups.
    [LoggerMessage(EventId = 822, Level = LogLevel.Debug, Message = "Запуск процесу зв'язування пристрою")]
    public static partial void StartLinkRequested(ILogger logger);

    [LoggerMessage(EventId = 823, Level = LogLevel.Debug, Message = "Завершення процесу зв'язування пристрою (deviceName={DeviceName})")]
    public static partial void FinishLinkRequested(ILogger logger, string deviceName);
}
