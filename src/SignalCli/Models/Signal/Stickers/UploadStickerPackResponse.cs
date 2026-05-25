using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Stickers;

/// <summary>
/// Відповідь на <c>uploadStickerPack</c>: shareable URL зі шляху + ключем pack'у.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 4. Wire-shape — flat <c>{ "url": "..." }</c> object per
/// <c>UploadStickerPackCommand.java:49-51 @ bda4e7fc</c> (writer.write(Map.of("url", url.getUrl()))).
/// <para>
/// <b>Quirk:</b> upstream НЕ повертає separately packId+packKey — лише URL з вшитими hex-fragment'ами.
/// Для raw bytes — parse URL fragment самостійно (e.g. regex on <c>#pack_id=&lt;hex&gt;&amp;pack_key=&lt;hex&gt;</c>).
/// </para>
/// </remarks>
/// <param name="Url">Full <c>https://signal.art/addstickers/#pack_id=...&amp;pack_key=...</c> URL.</param>
[PublicAPI]
public sealed record UploadStickerPackResponse(
    [property: JsonPropertyName("url")] string Url);
