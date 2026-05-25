using Microsoft.Extensions.Logging;

namespace SignalCli.Logging;

/// <summary>
/// signal-cli-api-coverage Wave 3 (contacts-identity): source-generated логи для
/// <see cref="Services.Signal.SignalContacts"/>. EventId-блок <b>830–849</b>
/// (within shared 800-899 range з Accounts/Devices/Groups per CLAUDE.md "Established patterns → Logging").
/// </summary>
/// <remarks>
/// Privacy (CLAUDE.md rule #1): жодних PII (account/recipient/safetyNumber/avatarPath) у
/// шаблонах <c>Information+</c>. Лише counts, method-name, idempotent-markers.
/// </remarks>
internal static partial class SignalContactsLog
{
    [LoggerMessage(EventId = 830, Level = LogLevel.Information,
        Message = "Список контактів отримано. Кількість={Count}")]
    public static partial void ListContactsOk(ILogger logger, int count);

    [LoggerMessage(EventId = 831, Level = LogLevel.Information,
        Message = "Список identity-keys отримано. Кількість={Count}")]
    public static partial void ListIdentitiesOk(ILogger logger, int count);

    [LoggerMessage(EventId = 832, Level = LogLevel.Information,
        Message = "Trust виставлено: Mode={Mode}")]
    public static partial void TrustOk(ILogger logger, string mode);

    [LoggerMessage(EventId = 833, Level = LogLevel.Information,
        Message = "Контакт оновлено")]
    public static partial void UpdateContactOk(ILogger logger);

    [LoggerMessage(EventId = 834, Level = LogLevel.Information,
        Message = "Контакт видалено. Mode={Mode}")]
    public static partial void RemoveContactOk(ILogger logger, string mode);

    [LoggerMessage(EventId = 835, Level = LogLevel.Information,
        Message = "Profile оновлено")]
    public static partial void UpdateProfileOk(ILogger logger);

    [LoggerMessage(EventId = 836, Level = LogLevel.Information,
        Message = "Block операцію виконано. Recipients={RecipientCount}, Groups={GroupCount}")]
    public static partial void BlockOk(ILogger logger, int recipientCount, int groupCount);

    [LoggerMessage(EventId = 837, Level = LogLevel.Information,
        Message = "Unblock операцію виконано. Recipients={RecipientCount}, Groups={GroupCount}")]
    public static partial void UnblockOk(ILogger logger, int recipientCount, int groupCount);
}
