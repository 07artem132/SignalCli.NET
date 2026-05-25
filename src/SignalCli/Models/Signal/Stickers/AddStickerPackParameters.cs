using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Stickers;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>addStickerPack</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 4. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/AddStickerPackCommand.java @ bda4e7fc</c>.
/// <para>
/// Argparse <c>--uri ... +</c> (nargs="+") — приймає кілька URI у одному виклику.
/// Upstream loops через список послідовно; failure на ANY uri aborts без rollback
/// уже-installed packs (partial-application caveat).
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
/// <param name="Uri">Один або кілька <c>https://signal.art/addstickers/#pack_id=...&amp;pack_key=...</c> URLs.</param>
[PublicAPI]
public sealed record AddStickerPackParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("uri")] IEnumerable<string> Uri);
