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

    // signal-cli-api-coverage Wave 1 (messaging-interactive). Кожен з 4-х нових send-методів
    // логує per-method Ok-лог із timestamp'ом, щоб діагностика розрізняла події.
    // Privacy: жодних acc/recipient/body/emoji у шаблонах (Critical rule #1).

    [LoggerMessage(EventId = 703, Level = LogLevel.Information,
        Message = "Реакцію відправлено успішно. TimeStamp={TimeStamp}")]
    public static partial void ReactionOk(ILogger logger, long timeStamp);

    [LoggerMessage(EventId = 704, Level = LogLevel.Information,
        Message = "Receipt відправлено успішно. TimeStamp={TimeStamp}")]
    public static partial void ReceiptOk(ILogger logger, long timeStamp);

    [LoggerMessage(EventId = 705, Level = LogLevel.Information,
        Message = "Typing-indicator відправлено успішно. TimeStamp={TimeStamp}")]
    public static partial void TypingOk(ILogger logger, long timeStamp);

    [LoggerMessage(EventId = 706, Level = LogLevel.Information,
        Message = "Remote-delete відправлено успішно. TimeStamp={TimeStamp}")]
    public static partial void RemoteDeleteOk(ILogger logger, long timeStamp);

    // signal-cli-api-coverage Wave 7 (polls + messaging-power-user). Privacy: жодного poll-question/
    // recipient/MobileCoin-receipt-bytes у Information+ шаблонах.

    [LoggerMessage(EventId = 707, Level = LogLevel.Information, Message = "Poll створено. TimeStamp={TimeStamp}")]
    public static partial void PollCreateOk(ILogger logger, long timeStamp);

    [LoggerMessage(EventId = 708, Level = LogLevel.Information, Message = "Poll-vote відправлено. TimeStamp={TimeStamp}")]
    public static partial void PollVoteOk(ILogger logger, long timeStamp);

    [LoggerMessage(EventId = 709, Level = LogLevel.Information, Message = "Poll-terminate відправлено. TimeStamp={TimeStamp}")]
    public static partial void PollTerminateOk(ILogger logger, long timeStamp);

    [LoggerMessage(EventId = 710, Level = LogLevel.Warning, Message = "AdminDelete відправлено (group moderation). TimeStamp={TimeStamp}")]
    public static partial void AdminDeleteOk(ILogger logger, long timeStamp);

    [LoggerMessage(EventId = 711, Level = LogLevel.Information, Message = "PinMessage відправлено. TimeStamp={TimeStamp}")]
    public static partial void PinMessageOk(ILogger logger, long timeStamp);

    [LoggerMessage(EventId = 712, Level = LogLevel.Information, Message = "UnpinMessage відправлено. TimeStamp={TimeStamp}")]
    public static partial void UnpinMessageOk(ILogger logger, long timeStamp);

    [LoggerMessage(EventId = 713, Level = LogLevel.Information, Message = "MessageRequestResponse відправлено. Type={Type}")]
    public static partial void MessageRequestResponseOk(ILogger logger, string type);

    [LoggerMessage(EventId = 714, Level = LogLevel.Information, Message = "PaymentNotification відправлено. TimeStamp={TimeStamp}")]
    public static partial void PaymentNotificationOk(ILogger logger, long timeStamp);
}
