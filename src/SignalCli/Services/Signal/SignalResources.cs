using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Logging;
using SignalCli.Models.Signal.Resources;
using SignalCli.Serialization;

namespace SignalCli.Services.Signal;

/// <summary>
/// signal-cli-api-coverage Wave 4 (binary-resource-fetch). 3 read-only методи що повертають
/// raw bytes після base64-decode.
/// </summary>
internal sealed class SignalResources(
    ISignalCliClient signalCliClient,
    ILogger<SignalResources> logger)
    : ISignalResources
{
    private readonly ISignalCliClient _signalCliClient =
        signalCliClient ?? throw new ArgumentNullException(nameof(signalCliClient));

    private readonly ILogger<SignalResources> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public async Task<byte[]> GetAttachmentAsync(
        string account,
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);
        ArgumentException.ThrowIfNullOrEmpty(id);

        var response = await _signalCliClient
            .InvokeMethodAsync(
                "getAttachment",
                new GetAttachmentParameters(account, id),
                SignalJsonContext.Default.GetAttachmentParameters,
                SignalJsonContext.Default.JsonAttachmentData,
                cancellationToken).ConfigureAwait(false);

        if (response == null)
            throw new InvalidOperationException("Отримано нульову відповідь від сервера");

        var bytes = DecodeBase64OrThrow(response.Data, method: "getAttachment");
        SignalResourcesLog.GetAttachmentOk(_logger, bytes.Length);
        return bytes;
    }

    /// <inheritdoc/>
    public async Task<byte[]> GetAvatarAsync(GetAvatarOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Defense-in-depth: §F19 XOR уже enforce'нуто у Builder, але якщо хтось обійшов
        // builder direct-record-construction — guard'имо тут.
        var sourceCount =
            (options.Contact is not null ? 1 : 0) +
            (options.Profile is not null ? 1 : 0) +
            (options.GroupId is not null ? 1 : 0);
        if (sourceCount != 1)
            throw new ArgumentException(
                "§F19: точно одне з Contact/Profile/GroupId має бути виставлено.", nameof(options));

        var parameters = new GetAvatarParameters(
            Account: options.Account,
            Contact: options.Contact,
            Profile: options.Profile,
            GroupId: options.GroupId);

        var response = await _signalCliClient
            .InvokeMethodAsync(
                "getAvatar",
                parameters,
                SignalJsonContext.Default.GetAvatarParameters,
                SignalJsonContext.Default.JsonAttachmentData,
                cancellationToken).ConfigureAwait(false);

        if (response == null)
            throw new InvalidOperationException("Отримано нульову відповідь від сервера");

        var bytes = DecodeBase64OrThrow(response.Data, method: "getAvatar");
        var source = options.Contact is not null ? "Contact"
                   : options.Profile is not null ? "Profile"
                   : "GroupId";
        SignalResourcesLog.GetAvatarOk(_logger, source, bytes.Length);
        return bytes;
    }

    /// <inheritdoc/>
    public async Task<byte[]> GetStickerAsync(
        string account,
        string packId,
        int stickerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);
        ArgumentException.ThrowIfNullOrEmpty(packId);
        ValidateHexPackId(packId);

        var response = await _signalCliClient
            .InvokeMethodAsync(
                "getSticker",
                new GetStickerParameters(account, packId, stickerId),
                SignalJsonContext.Default.GetStickerParameters,
                SignalJsonContext.Default.JsonAttachmentData,
                cancellationToken).ConfigureAwait(false);

        if (response == null)
            throw new InvalidOperationException("Отримано нульову відповідь від сервера");

        var bytes = DecodeBase64OrThrow(response.Data, method: "getSticker");
        SignalResourcesLog.GetStickerOk(_logger, bytes.Length);
        return bytes;
    }

    /// <summary>
    /// Client-side hex-валідація packId перед wire'ом. Запобігає upstream
    /// IOException → -32603 INTERNAL_ERROR і дає чіткіший <see cref="ArgumentException"/>.
    /// </summary>
    private static void ValidateHexPackId(string packId)
    {
        // StickerPackId — 16 bytes → 32 hex-chars lowercase per signal-cli convention.
        if (packId.Length != 32)
            throw new ArgumentException(
                $"packId повинен бути 32-char lowercase-hex рядком (16 байт), а отримано {packId.Length}.",
                nameof(packId));
        foreach (var c in packId)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                throw new ArgumentException(
                    "packId повинен бути lowercase-hex (0-9, a-f).", nameof(packId));
        }
    }

    /// <summary>
    /// Декодує base64 з diagnostic-friendly помилкою. Per task §4.6: invalid base64 →
    /// <see cref="InvalidOperationException"/>, не <see cref="FormatException"/> (clearer
    /// для wrapper consumer'ів — це не їх формат-помилка, а upstream-protocol drift).
    /// </summary>
    private byte[] DecodeBase64OrThrow(string data, string method)
    {
        try
        {
            return Convert.FromBase64String(data);
        }
        catch (FormatException ex)
        {
            SignalResourcesLog.InvalidBase64(_logger, method);
            throw new InvalidOperationException(
                $"signal-cli повернув invalid base64 у відповіді {method}: {ex.Message}", ex);
        }
    }
}
