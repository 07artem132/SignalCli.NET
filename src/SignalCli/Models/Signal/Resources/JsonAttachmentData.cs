using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Resources;

/// <summary>
/// Shared envelope для відповідей <c>getAttachment</c>, <c>getAvatar</c>, <c>getSticker</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 4 (`binary-resource-fetch`). Pinned до
/// <c>src/main/java/org/asamk/signal/json/JsonAttachmentData.java @ bda4e7fc</c>.
/// <para>
/// Base64-encoding — standard alphabet (RFC 4648, з <c>+</c>/<c>/</c>), НЕ URL-safe.
/// <see cref="Convert.FromBase64String"/> на .NET-сайті приймає standard.
/// </para>
/// </remarks>
/// <param name="Data">Base64-encoded bytes ресурсу.</param>
[PublicAPI]
public sealed record JsonAttachmentData(
    [property: JsonPropertyName("data")] string Data);
