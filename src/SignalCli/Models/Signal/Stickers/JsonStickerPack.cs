using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Stickers;

/// <summary>
/// Один sticker pack у відповіді <c>listStickerPacks</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 4. Pinned до
/// <c>ListStickerPacksCommand.java:53-70 @ bda4e7fc</c> (private JsonStickerPack record).
/// <para>
/// Quirks: <see cref="Title"/>/<see cref="Author"/> можуть бути empty strings (НЕ null) —
/// upstream <c>StickerPack</c> constructor default'ить на <c>""</c>. <see cref="Stickers"/> може
/// бути empty <c>[]</c> якщо манифест ще не fetched з CDN. <see cref="Cover"/> — єдине поле
/// що може бути <c>null</c>.
/// </para>
/// </remarks>
/// <param name="PackId">Lowercase-hex 16-byte StickerPackId.</param>
/// <param name="Url">Full <c>https://signal.art/addstickers/#...</c> URL.</param>
/// <param name="Installed">Чи цей account має pack як installed.</param>
/// <param name="Title">Title (empty string якщо unknown).</param>
/// <param name="Author">Author (empty string якщо unknown).</param>
/// <param name="Cover">Cover sticker (single), <c>null</c> якщо absent.</param>
/// <param name="Stickers">Повний список стікерів (empty <c>[]</c> якщо манифест ще не fetched).</param>
[PublicAPI]
public sealed record JsonStickerPack(
    [property: JsonPropertyName("packId")] string PackId,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("installed")] bool Installed,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("author")] string Author,
    [property: JsonPropertyName("cover")] JsonStickerPackItem? Cover,
    [property: JsonPropertyName("stickers")] IReadOnlyList<JsonStickerPackItem> Stickers);

/// <summary>
/// Sticker у sticker pack'у.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 4. Pinned до <c>ListStickerPacksCommand.java:73-78 @ bda4e7fc</c>.
/// </remarks>
/// <param name="Id">0-based sticker index у pack'у.</param>
/// <param name="Emoji">Emoji-асоціація.</param>
/// <param name="ContentType">MIME type (<c>"image/webp"</c>).</param>
[PublicAPI]
public sealed record JsonStickerPackItem(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("emoji")] string Emoji,
    [property: JsonPropertyName("contentType")] string ContentType);
