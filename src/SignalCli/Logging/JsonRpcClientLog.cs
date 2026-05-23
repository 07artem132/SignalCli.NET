using Microsoft.Extensions.Logging;

namespace SignalCli.Logging;

/// <summary>
/// C.3: source-generated логи для <see cref="Services.Rpc.JsonRpcClient"/>.
/// EventId-блок 300–399.
/// </summary>
internal static partial class JsonRpcClientLog
{
    [LoggerMessage(EventId = 300, Level = LogLevel.Warning,
        Message = "Очікування завершення reader-циклів перевищило таймаут — продовжуємо disposal")]
    public static partial void ReaderStopTimeout(ILogger logger);

    [LoggerMessage(EventId = 301, Level = LogLevel.Trace, Message = "Отримано рядок від signal-cli: {Line}")]
    public static partial void StdoutLine(ILogger logger, string line);

    [LoggerMessage(EventId = 302, Level = LogLevel.Error, Message = "Помилка читання з виходу процесу")]
    public static partial void StdoutReadFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 303, Level = LogLevel.Trace, Message = "STDERR> {Line}")]
    public static partial void StderrLine(ILogger logger, string line);

    [LoggerMessage(EventId = 304, Level = LogLevel.Error, Message = "Помилка читання stderr процесу")]
    public static partial void StderrReadFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 305, Level = LogLevel.Warning, Message = "Не вдалося встановити результат")]
    public static partial void TrySetResultFailed(ILogger logger);

    [LoggerMessage(EventId = 306, Level = LogLevel.Debug, Message = "Отримано повідомлення: Method={Method}")]
    public static partial void NotificationMethod(ILogger logger, string? method);

    [LoggerMessage(EventId = 307, Level = LogLevel.Trace, Message = "RawParams={Json}")]
    public static partial void NotificationRawParams(ILogger logger, string json);

    [LoggerMessage(EventId = 308, Level = LogLevel.Warning, Message = "Невідоме повідомлення: {Json}")]
    public static partial void UnknownMessage(ILogger logger, string json);

    [LoggerMessage(EventId = 309, Level = LogLevel.Error, Message = "Помилка розбору JSON: {Line}")]
    public static partial void JsonParseFailed(ILogger logger, Exception ex, string line);

    [LoggerMessage(EventId = 310, Level = LogLevel.Error, Message = "Неочікувана помилка обробки JSON-рядка: {Line}")]
    public static partial void UnexpectedProcessMessage(ILogger logger, Exception ex, string line);

    [LoggerMessage(EventId = 320, Level = LogLevel.Trace, Message = "Відправлено JSON-RPC запит: {Json}")]
    public static partial void SentRequest(ILogger logger, string json);
}
