using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Resources;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>getSticker</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 4. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/GetStickerCommand.java @ bda4e7fc</c>.
/// <para>
/// <b>Quirk:</b> <see cref="PackId"/> — lowercase-hex 32-char string (16 bytes after decode).
/// Upstream <c>Hex.toByteArray</c> на malformed hex кидає uncaught IOException →
/// <c>-32603 INTERNAL_ERROR</c>. Service-метод робить client-side hex validation і
/// throw'ить <see cref="ArgumentException"/> до wire'у.
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
/// <param name="PackId">Lowercase-hex 32-char StickerPackId.</param>
/// <param name="StickerId">0-based sticker index у pack'у.</param>
[PublicAPI]
public sealed record GetStickerParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("packId")] string PackId,
    [property: JsonPropertyName("stickerId")] int StickerId);
