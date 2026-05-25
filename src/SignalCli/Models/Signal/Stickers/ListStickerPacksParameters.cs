using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Stickers;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>listStickerPacks</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 4. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/ListStickerPacksCommand.java @ bda4e7fc</c>.
/// Read-only. Argparse не має своїх аргументів — лише dispatcher-param <c>account</c>.
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
[PublicAPI]
public sealed record ListStickerPacksParameters(
    [property: JsonPropertyName("account")] string Account);
