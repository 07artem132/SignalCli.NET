using JetBrains.Annotations;
using Newtonsoft.Json;

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
    [JsonProperty("code")]
    public int Code { get; init; }

    /// <summary>
    /// Повідомлення про помилку.
    /// </summary>
    [JsonProperty("message")]
    public string Message { get; init; }

    /// <summary>
    /// Додаткові дані про помилку.
    /// </summary>
    [JsonProperty("data")]
    public object Data { get; init; }
}