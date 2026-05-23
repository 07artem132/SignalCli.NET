using Microsoft.Extensions.Logging;

namespace SignalCli.Logging;

/// <summary>
/// C.5: source-generated логи для <see cref="Services.Signal.SignalAccounts"/>.
/// EventId-блок 800–809.
/// </summary>
internal static partial class SignalAccountsLog
{
    [LoggerMessage(EventId = 800, Level = LogLevel.Debug, Message = "Отримання списку облікових записів")]
    public static partial void ListAccountsRequested(ILogger logger);

    [LoggerMessage(EventId = 801, Level = LogLevel.Error, Message = "Отримано нульову відповідь на listAccounts")]
    public static partial void ListAccountsNullResponse(ILogger logger);

    [LoggerMessage(EventId = 802, Level = LogLevel.Information,
        Message = "Список облікових записів отримано успішно. Кількість={Count}")]
    public static partial void ListAccountsOk(ILogger logger, int count);

    [LoggerMessage(EventId = 803, Level = LogLevel.Trace, Message = "Облікові записи={AccountList}")]
    public static partial void ListAccountsTrace(ILogger logger, string accountList);

    [LoggerMessage(EventId = 804, Level = LogLevel.Error, Message = "Помилка отримання списку облікових записів")]
    public static partial void ListAccountsFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 805, Level = LogLevel.Debug, Message = "Синхронізація облікових записів")]
    public static partial void SyncAccountRequested(ILogger logger);

    [LoggerMessage(EventId = 806, Level = LogLevel.Error, Message = "Отримано нульову відповідь на sendSyncRequest")]
    public static partial void SyncAccountNullResponse(ILogger logger);

    [LoggerMessage(EventId = 807, Level = LogLevel.Information, Message = "Синхронізація облікових записів виконана успішно.")]
    public static partial void SyncAccountOk(ILogger logger);

    [LoggerMessage(EventId = 808, Level = LogLevel.Error, Message = "Помилка синхронізації облікових записів")]
    public static partial void SyncAccountFailed(ILogger logger, Exception ex);
}
