using Microsoft.Extensions.Logging;

namespace SignalCli.Logging;

/// <summary>
/// C.4: source-generated логи для <see cref="Services.Signal.SignalEventService"/>.
/// EventId-блок 500–599.
/// </summary>
internal static partial class SignalEventServiceLog
{
    [LoggerMessage(EventId = 500, Level = LogLevel.Debug, Message = "{Type}")]
    public static partial void DebugMessage(ILogger logger, string type);

    [LoggerMessage(EventId = 501, Level = LogLevel.Information,
        Message = "SubscribeAsync: обліковий запис={Account}, ідентифікатор підписки={SubId}")]
    public static partial void Subscribed(ILogger logger, string account, int subId);

    [LoggerMessage(EventId = 502, Level = LogLevel.Warning, Message = "Не знайдено підписку з ідентифікатором={SubId}")]
    public static partial void UnsubscribeMissing(ILogger logger, int subId);

    [LoggerMessage(EventId = 503, Level = LogLevel.Information,
        Message = "Відписка успішна: Обліковий запис={Account}, ІдПідписки={SubscriptionId}")]
    public static partial void Unsubscribed(ILogger logger, string account, int subscriptionId);

    [LoggerMessage(EventId = 504, Level = LogLevel.Warning, Message = "Сповіщення без Envelope, пропускаємо...")]
    public static partial void EnvelopeMissing(ILogger logger);

    [LoggerMessage(EventId = 505, Level = LogLevel.Debug, Message = "Подія для неактуальної підписки {SubId}, ігноруємо.")]
    public static partial void StaleSubscription(ILogger logger, int subId);

    [LoggerMessage(EventId = 506, Level = LogLevel.Debug,
        Message = "DataMessage без впізнаваного payload (пусте/групове-метадані) — пропускаємо.")]
    public static partial void DataMessageEmpty(ILogger logger);

    [LoggerMessage(EventId = 507, Level = LogLevel.Debug, Message = "Невідомий тип події, пропускаємо...")]
    public static partial void UnknownEnvelope(ILogger logger);

    [LoggerMessage(EventId = 508, Level = LogLevel.Error, Message = "Помилка при обробці вхідного сповіщення від Signal")]
    public static partial void NotificationDispatchFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 509, Level = LogLevel.Debug,
        Message = "E: канал {Type} переповнений — DropOldest. Сумарно дропів={Dropped}")]
    public static partial void ChannelOverflowed(ILogger logger, string type, long dropped);

    // audit N5: повторний SubscribeAsync для облікового запису з вже наявною підпискою —
    // повертаємо існуючий ID (ідемпотентність) і логуємо на Debug.
    [LoggerMessage(EventId = 510, Level = LogLevel.Debug,
        Message = "SubscribeAsync ідемпотентний: обліковий запис={Account} вже має підписку={SubId}, повертаємо існуючий ID без RPC")]
    public static partial void SubscribeIdempotent(ILogger logger, string account, int subId);

    // Захист від receive-нотіфікації без корисного навантаження (params або params.result == null):
    // трапляється, коли signal-cli віддає envelope, який не вдалося обробити (напр. sealed-sender без
    // serverGuid до фіксу upstream #2059). Лог-і-скіп замість NRE — Debug, бо очікувано-benign.
    [LoggerMessage(EventId = 511, Level = LogLevel.Debug,
        Message = "Сповіщення без корисного навантаження (params/result порожні), пропускаємо...")]
    public static partial void NotificationPayloadMissing(ILogger logger);
}
