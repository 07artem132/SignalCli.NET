using System.Text.Json;
using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Rpc;

/// <summary>
/// Інформація про помилку JSON-RPC.
/// </summary>
[PublicAPI]
public record JsonRpcError
{
    /// <summary>
    /// Код помилки.
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; init; }

    /// <summary>
    /// Повідомлення про помилку.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>
    /// Додаткові дані про помилку (опціонально). Залишаємо як <see cref="JsonElement"/>?,
    /// щоб STJ source-gen не падав на нетипізованому <c>object</c> (H.20/F20).
    /// </summary>
    [JsonPropertyName("data")]
    public JsonElement? Data { get; init; }
}
