using Microsoft.Extensions.Logging;

namespace SignalCli.Logging;

/// <summary>
/// C.5: source-generated логи для <see cref="Services.Signal.SignalGroups"/>.
/// EventId-блок 810–819.
/// </summary>
internal static partial class SignalGroupsLog
{
    [LoggerMessage(EventId = 810, Level = LogLevel.Debug, Message = "Отримання списку груп")]
    public static partial void ListGroupsRequested(ILogger logger);

    [LoggerMessage(EventId = 811, Level = LogLevel.Error, Message = "Отримано нульову відповідь на listGroups")]
    public static partial void ListGroupsNullResponse(ILogger logger);

    [LoggerMessage(EventId = 812, Level = LogLevel.Information,
        Message = "Список груп отримано успішно. Кількість={Count}")]
    public static partial void ListGroupsOk(ILogger logger, int count);

    [LoggerMessage(EventId = 813, Level = LogLevel.Trace, Message = "Групи={Groups}")]
    public static partial void ListGroupsTrace(ILogger logger, string groups);

    [LoggerMessage(EventId = 814, Level = LogLevel.Error, Message = "Помилка отримання списку груп")]
    public static partial void ListGroupsFailed(ILogger logger, Exception ex);

    // signal-cli-api-coverage Wave 2 (groups-crud). Privacy: groupId — PII, не у Information+;
    // лише timestamp + idempotent-marker. Block 815-819 (Accounts: 800-808; Groups: 810-819; Devices: 820+).

    [LoggerMessage(EventId = 815, Level = LogLevel.Information,
        Message = "Приєднано до групи. TimeStamp={TimeStamp}, OnlyRequested={OnlyRequested}")]
    public static partial void JoinGroupOk(ILogger logger, long timeStamp, bool onlyRequested);

    [LoggerMessage(EventId = 816, Level = LogLevel.Information,
        Message = "Групу оновлено. TimeStamp={TimeStamp}, Created={Created}")]
    public static partial void UpdateGroupOk(ILogger logger, long? timeStamp, bool created);

    [LoggerMessage(EventId = 817, Level = LogLevel.Information,
        Message = "Залишено групу. TimeStamp={TimeStamp}")]
    public static partial void QuitGroupOk(ILogger logger, long timeStamp);

    [LoggerMessage(EventId = 818, Level = LogLevel.Information,
        Message = "QuitGroup — idempotent no-op: акаунт вже не є member групи")]
    public static partial void QuitGroupAlreadyNotMember(ILogger logger);

    [LoggerMessage(EventId = 819, Level = LogLevel.Error, Message = "Отримано нульову відповідь на групову операцію {Method}")]
    public static partial void GroupOpNullResponse(ILogger logger, string method);
}
