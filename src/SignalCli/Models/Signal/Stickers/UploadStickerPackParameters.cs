using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Stickers;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>uploadStickerPack</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 4 (`sticker-packs`). Pinned до
/// <c>src/main/java/org/asamk/signal/commands/UploadStickerPackCommand.java @ bda4e7fc</c>.
/// <para>
/// <b>Daemon-side filesystem:</b> <see cref="Path"/> інтерпретується signal-cli-процесом
/// на його ФС (не клієнтом). Daemon-mode consumers повинні мати файл доступним сервер-side.
/// Файл — або <c>manifest.json</c>, або <c>.zip</c> з sticker pack.
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
/// <param name="Path">Filesystem-шлях до manifest.json або .zip sticker pack'у.</param>
[PublicAPI]
public sealed record UploadStickerPackParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("path")] string Path);
