using System.Text.Json;
using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Logging;
using SignalCli.Models.Signal.Stickers;
using SignalCli.Serialization;

namespace SignalCli.Services.Signal;

/// <summary>
/// signal-cli-api-coverage Wave 4 (sticker-packs).
/// </summary>
internal sealed class SignalStickers(
    ISignalCliClient signalCliClient,
    ILogger<SignalStickers> logger)
    : ISignalStickers
{
    private readonly ISignalCliClient _signalCliClient =
        signalCliClient ?? throw new ArgumentNullException(nameof(signalCliClient));

    private readonly ILogger<SignalStickers> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public async Task<UploadStickerPackResponse> UploadStickerPackAsync(
        string account,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);
        ArgumentException.ThrowIfNullOrEmpty(path);

        var response = await _signalCliClient
            .InvokeMethodAsync(
                "uploadStickerPack",
                new UploadStickerPackParameters(account, path),
                SignalJsonContext.Default.UploadStickerPackParameters,
                SignalJsonContext.Default.UploadStickerPackResponse,
                cancellationToken).ConfigureAwait(false);

        if (response == null)
            throw new InvalidOperationException("Отримано нульову відповідь від сервера");

        SignalStickersLog.UploadStickerPackOk(_logger);
        return response;
    }

    /// <inheritdoc/>
    public async Task<ListStickerPacksResponse> ListStickerPacksAsync(
        string account,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);

        var response = await _signalCliClient
            .InvokeMethodAsync(
                "listStickerPacks",
                new ListStickerPacksParameters(account),
                SignalJsonContext.Default.ListStickerPacksParameters,
                SignalJsonContext.Default.ListStickerPacksResponse,
                cancellationToken).ConfigureAwait(false);

        if (response == null)
            throw new InvalidOperationException("Отримано нульову відповідь від сервера");

        SignalStickersLog.ListStickerPacksOk(_logger, response.Count);
        return response;
    }

    /// <inheritdoc/>
    public async Task AddStickerPackAsync(
        string account,
        IEnumerable<string> uris,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);
        ArgumentNullException.ThrowIfNull(uris);

        var uriList = uris.ToList();
        if (uriList.Count == 0)
            throw new ArgumentException("Принаймні один URI обов'язковий.", nameof(uris));

        await _signalCliClient
            .InvokeMethodAsync(
                "addStickerPack",
                new AddStickerPackParameters(account, uriList),
                SignalJsonContext.Default.AddStickerPackParameters,
                SignalJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);

        SignalStickersLog.AddStickerPackOk(_logger, uriList.Count);
    }
}
