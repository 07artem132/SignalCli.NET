using SignalCli.Exceptions;
using SignalCli.Models.Signal.Stickers;

namespace SignalCli.Interfaces.Signal;

/// <summary>
/// Сервіс для роботи зі sticker pack'ами Signal (signal-cli-api-coverage Wave 4).
/// </summary>
/// <remarks>
/// 3 RPC методи: upload (network mutating), list (read-only), add/install (network mutating).
/// signal-cli НЕ надає <c>remove</c>/<c>uninstall</c> RPC — для виключення pack'у потрібен
/// ручний edit local-store з боку upstream (out of scope для .NET wrapper'у).
/// </remarks>
public interface ISignalStickers
{
    /// <summary>
    /// Завантажує sticker pack на Signal CDN з manifest.json або .zip файла на daemon-side ФС.
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see
    /// <c>src/main/java/org/asamk/signal/commands/UploadStickerPackCommand.java</c> @ <c>bda4e7fc</c>.
    /// <para>
    /// Після успішного upload'у pack автоматично НЕ install'иться для uploader'а — треба
    /// окремо викликати <see cref="AddStickerPackAsync"/> з повернутим URL.
    /// </para>
    /// </remarks>
    /// <param name="account">E.164 номер акаунту.</param>
    /// <param name="path">Daemon-side ФС-шлях до manifest.json або .zip pack'у.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <returns>Shareable URL pack'у (<c>https://signal.art/addstickers/#pack_id=...&amp;pack_key=...</c>).</returns>
    /// <exception cref="JsonRpcException"><c>-1 UserError</c> якщо pack invalid (missing manifest/limit exceeded); <c>-3 IoError</c> при network/image-size failure.</exception>
    Task<UploadStickerPackResponse> UploadStickerPackAsync(
        string account,
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Список усіх відомих sticker pack'ів акаунту (installed + seen via incoming sync).
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see
    /// <c>src/main/java/org/asamk/signal/commands/ListStickerPacksCommand.java</c> @ <c>bda4e7fc</c>.
    /// Read-only.
    /// </remarks>
    /// <param name="account">E.164 номер акаунту.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    Task<ListStickerPacksResponse> ListStickerPacksAsync(
        string account,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Install'ить один або кілька sticker pack'ів за URL'ами.
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see
    /// <c>src/main/java/org/asamk/signal/commands/AddStickerPackCommand.java</c> @ <c>bda4e7fc</c>.
    /// <para>
    /// <b>Caveat:</b> upstream обробляє URL'и послідовно — failure на будь-якому aborts BEZ
    /// rollback'у вже-installed pack'ів. Partial-application можлива.
    /// </para>
    /// </remarks>
    /// <param name="account">E.164 номер акаунту.</param>
    /// <param name="uris">Один або кілька <c>https://signal.art/addstickers/#...</c> URL'ів.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <exception cref="ArgumentException">Якщо <paramref name="uris"/> порожній.</exception>
    Task AddStickerPackAsync(
        string account,
        IEnumerable<string> uris,
        CancellationToken cancellationToken = default);
}
