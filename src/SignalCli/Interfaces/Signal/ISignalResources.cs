using SignalCli.Exceptions;
using SignalCli.Models.Signal.Resources;

namespace SignalCli.Interfaces.Signal;

/// <summary>
/// Сервіс для отримання binary ресурсів з кешу signal-cli (signal-cli-api-coverage Wave 4).
/// </summary>
/// <remarks>
/// 3 read-only RPC методи. Усі повертають bytes (декодовані з base64 wire-shape). Помилка
/// base64 deserialization → <see cref="InvalidOperationException"/> з diagnostic message.
/// <para>
/// Усі ресурси читаються з ЛОКАЛЬНОГО кешу signal-cli — НЕ тригерять re-download з CDN.
/// Якщо ресурс не у кеші (наприклад, attachment не processed через <c>receive</c> попередньо)
/// — отримаєш <c>-1 UserError</c> з повідомленням "Could not find attachment with ID:" /
/// "Could not find avatar" / "Could not find sticker with ID:".
/// </para>
/// </remarks>
public interface ISignalResources
{
    /// <summary>
    /// Читає attachment з локального кешу signal-cli за ID.
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see
    /// <c>src/main/java/org/asamk/signal/commands/GetAttachmentCommand.java</c> @ <c>bda4e7fc</c>.
    /// </remarks>
    /// <param name="account">E.164 номер акаунту.</param>
    /// <param name="id">ID attachment'а (з <c>DataMessage.Attachments[].Id</c>).</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <returns>Raw bytes attachment'а (декодовані з base64 wire-shape).</returns>
    /// <exception cref="JsonRpcException"><c>-1 UserError</c> якщо ID не знайдено у кеші.</exception>
    /// <exception cref="InvalidOperationException">Якщо upstream повернув invalid base64.</exception>
    Task<byte[]> GetAttachmentAsync(
        string account,
        string id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Читає avatar (contact-list, public profile, або group) з локального кешу.
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see
    /// <c>src/main/java/org/asamk/signal/commands/GetAvatarCommand.java</c> @ <c>bda4e7fc</c>.
    /// <para>
    /// <b>§F19 3-way XOR:</b> <see cref="GetAvatarOptions.Builder"/> enforce'ить вибір рівно
    /// одного з Contact/Profile/GroupId.
    /// </para>
    /// </remarks>
    /// <param name="options">Options з §F19 XOR-enforced джерелом avatar'а.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <returns>Raw bytes avatar image (зазвичай image/jpeg або image/webp).</returns>
    /// <exception cref="JsonRpcException"><c>-1 UserError</c> якщо avatar не знайдено.</exception>
    /// <exception cref="InvalidOperationException">Якщо upstream повернув invalid base64.</exception>
    Task<byte[]> GetAvatarAsync(GetAvatarOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Читає sticker з локального кешу за packId + stickerId.
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see
    /// <c>src/main/java/org/asamk/signal/commands/GetStickerCommand.java</c> @ <c>bda4e7fc</c>.
    /// </remarks>
    /// <param name="account">E.164 номер акаунту.</param>
    /// <param name="packId">Lowercase-hex 32-char StickerPackId.</param>
    /// <param name="stickerId">0-based sticker index у pack'у.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <returns>Raw bytes sticker image (зазвичай image/webp).</returns>
    /// <exception cref="ArgumentException">Якщо <paramref name="packId"/> — не valid lowercase-hex 32-char.</exception>
    /// <exception cref="JsonRpcException"><c>-1 UserError</c> якщо sticker не знайдено.</exception>
    Task<byte[]> GetStickerAsync(
        string account,
        string packId,
        int stickerId,
        CancellationToken cancellationToken = default);
}
