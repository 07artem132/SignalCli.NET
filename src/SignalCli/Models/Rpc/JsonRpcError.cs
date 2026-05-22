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
    public string Message { get; init; }

    /// <summary>
    /// Додаткові дані про помилку.
    /// </summary>
    [JsonPropertyName("data")]
    public object Data { get; init; }
}