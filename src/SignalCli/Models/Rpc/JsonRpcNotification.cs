using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SignalCli.Models.Rpc;

/// <summary>
/// Нетипізоване повідомлення JSON-RPC (notification).
/// Використовується для отримання різних типів повідомлень від сервера.
/// </summary>
[PublicAPI]
public record JsonRpcNotificationRaw
{
    /// <summary>
    /// Версія протоколу JSON-RPC.
    /// </summary>
    [JsonProperty("jsonrpc")]
    public string JsonRpc { get; init; }

    /// <summary>
    /// Назва методу, по якому надійшло повідомлення.
    /// </summary>
    [JsonProperty("method")]
    public string Method { get; init; }

    /// <summary>
    /// Параметри повідомлення у форматі JToken.
    /// </summary>
    [JsonProperty("params")]
    public JToken Params { get; init; }
}

/// <summary>
/// Типізоване повідомлення JSON-RPC (notification).
/// Використовується для отримання повідомлень певного типу від сервера.
/// </summary>
/// <typeparam name="T">Тип параметрів повідомлення.</typeparam>
[PublicAPI]
public record JsonRpcNotification<T>
{
    /// <summary>
    /// Версія протоколу JSON-RPC.
    /// </summary>
    [JsonProperty("jsonrpc")]
    public string JsonRpc { get; init; }

    /// <summary>
    /// Назва методу, по якому надійшло повідомлення.
    /// </summary>
    [JsonProperty("method")]
    public string Method { get; init; }

    /// <summary>
    /// Типізовані параметри повідомлення.
    /// </summary>
    [JsonProperty("params")]
    public T Params { get; init; }
}