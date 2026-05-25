using Microsoft.Extensions.Logging;

namespace SignalCli.Logging;

/// <summary>
/// signal-cli-api-coverage Wave 4 (binary-resource-fetch): source-generated логи для
/// <see cref="Services.Signal.SignalResources"/>. EventId-блок <b>860–869</b>
/// (within shared 800-899).
/// </summary>
/// <remarks>
/// Privacy (CLAUDE.md rule #1): жодних path/id/packId у Information+. Лише byte-count
/// (corollary'й leak — розмір ресурсу — minimal, не fingerprint'абельний).
/// </remarks>
internal static partial class SignalResourcesLog
{
    [LoggerMessage(EventId = 860, Level = LogLevel.Information,
        Message = "Attachment отримано. Bytes={Bytes}")]
    public static partial void GetAttachmentOk(ILogger logger, int bytes);

    [LoggerMessage(EventId = 861, Level = LogLevel.Information,
        Message = "Avatar отримано. Source={Source}, Bytes={Bytes}")]
    public static partial void GetAvatarOk(ILogger logger, string source, int bytes);

    [LoggerMessage(EventId = 862, Level = LogLevel.Information,
        Message = "Sticker отримано. Bytes={Bytes}")]
    public static partial void GetStickerOk(ILogger logger, int bytes);

    [LoggerMessage(EventId = 863, Level = LogLevel.Error,
        Message = "Upstream повернув invalid base64 для {Method}")]
    public static partial void InvalidBase64(ILogger logger, string method);
}
