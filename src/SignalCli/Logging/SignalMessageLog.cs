using Microsoft.Extensions.Logging;

namespace SignalCli.Logging;

/// <summary>
/// C.5: source-generated логи для <see cref="Services.Signal.SignalMessage"/>.
/// EventId-блок 700–799.
/// </summary>
internal static partial class SignalMessageLog
{
    [LoggerMessage(EventId = 700, Level = LogLevel.Error,
        Message = "Отримано null-відповідь від сервера при відправці повідомлення")]
    public static partial void SendNullResponse(ILogger logger);

    [LoggerMessage(EventId = 701, Level = LogLevel.Information,
        Message = "Повідомлення відправлено успішно. TimeStamp={TimeStamp}")]
    public static partial void SendOk(ILogger logger, long timeStamp);

    [LoggerMessage(EventId = 702, Level = LogLevel.Error, Message = "Помилка при відправці повідомлення")]
    public static partial void SendFailed(ILogger logger, Exception ex);
}
