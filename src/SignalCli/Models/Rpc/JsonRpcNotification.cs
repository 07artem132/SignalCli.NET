using System.Text.Json;
using JetBrains.Annotations;
using System.Text.Json.Serialization;

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
    [JsonPropertyName("jsonrpc")]
    public string? JsonRpc { get; init; }

    /// <summary>
    /// Назва методу, по якому надійшло повідомлення.
    /// </summary>
    [JsonPropertyName("method")]
    public string? Method { get; init; }

    /// <summary>
    /// Параметри повідомлення у форматі JsonElement.
    /// </summary>
    [JsonPropertyName("params")]
    public JsonElement Params { get; init; }
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
    [JsonPropertyName("jsonrpc")]
    public string? JsonRpc { get; init; }

    /// <summary>
    /// Назва методу, по якому надійшло повідомлення.
    /// </summary>
    [JsonPropertyName("method")]
    public string? Method { get; init; }

    /// <summary>
    /// Типізовані параметри повідомлення.
    /// </summary>
    [JsonPropertyName("params")]
    public T Params { get; init; }
}