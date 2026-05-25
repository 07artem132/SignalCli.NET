using Microsoft.Extensions.Logging;

namespace SignalCli.Logging;

/// <summary>
/// signal-cli-api-coverage Wave 4 (sticker-packs): source-generated логи для
/// <see cref="Services.Signal.SignalStickers"/>. EventId-блок <b>850–859</b>
/// (within shared 800-899 з Accounts/Devices/Groups/Contacts).
/// </summary>
internal static partial class SignalStickersLog
{
    [LoggerMessage(EventId = 850, Level = LogLevel.Information,
        Message = "Sticker pack завантажено успішно")]
    public static partial void UploadStickerPackOk(ILogger logger);

    [LoggerMessage(EventId = 851, Level = LogLevel.Information,
        Message = "Список sticker pack'ів отримано. Кількість={Count}")]
    public static partial void ListStickerPacksOk(ILogger logger, int count);

    [LoggerMessage(EventId = 852, Level = LogLevel.Information,
        Message = "Sticker pack'и встановлено. Count={Count}")]
    public static partial void AddStickerPackOk(ILogger logger, int count);
}
