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

    // signal-cli-api-coverage Wave 6 (account-lifecycle, DESTRUCTIVE). Block 870-879 within shared 800-899.
    // Privacy (CLAUDE.md rule #1): жодних phone/pin/verification-code у Information+ шаблонах.

    [LoggerMessage(EventId = 870, Level = LogLevel.Warning,
        Message = "Спроба destructive operation '{Method}' заблокована — EnableDestructiveOperations=false")]
    public static partial void DestructiveOperationBlocked(ILogger logger, string method);

    [LoggerMessage(EventId = 871, Level = LogLevel.Information,
        Message = "Account оновлено. UsernameChanged={UsernameChanged}")]
    public static partial void UpdateAccountOk(ILogger logger, bool usernameChanged);

    [LoggerMessage(EventId = 872, Level = LogLevel.Warning,
        Message = "Account UNREGISTERED. DeleteAccount={DeleteAccount}")]
    public static partial void UnregisterOk(ILogger logger, bool deleteAccount);

    [LoggerMessage(EventId = 873, Level = LogLevel.Warning,
        Message = "Local account data WIPED. IgnoreRegistered={IgnoreRegistered}")]
    public static partial void DeleteLocalAccountDataOk(ILogger logger, bool ignoreRegistered);

    [LoggerMessage(EventId = 874, Level = LogLevel.Information,
        Message = "Change-number flow started. Voice={Voice}")]
    public static partial void StartChangeNumberOk(ILogger logger, bool voice);

    [LoggerMessage(EventId = 875, Level = LogLevel.Warning,
        Message = "Change-number flow COMPLETED — account number змінено")]
    public static partial void FinishChangeNumberOk(ILogger logger);

    [LoggerMessage(EventId = 876, Level = LogLevel.Information,
        Message = "Configuration оновлено")]
    public static partial void UpdateConfigurationOk(ILogger logger);

    [LoggerMessage(EventId = 877, Level = LogLevel.Information,
        Message = "Registration-lock PIN встановлено")]
    public static partial void SetPinOk(ILogger logger);

    [LoggerMessage(EventId = 878, Level = LogLevel.Warning,
        Message = "Registration-lock PIN видалено — account security weakened")]
    public static partial void RemovePinOk(ILogger logger);
}
