using System.Text.Json;
using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Rpc;

/// <summary>
/// Запит JSON-RPC відповідно до специфікації 2.0.
/// </summary>
/// <remarks>
/// Використовується для відправки запитів на JSON-RPC сервер.
/// Підтримує передачу параметрів, ідентифікатора запиту та версії протоколу.
/// </remarks>
[PublicAPI]
public record JsonRpcRequest(string Method, JsonElement Params, string Id)
{
    /// <summary>
    /// Версія протоколу JSON-RPC.
    /// </summary>
    /// <value>Завжди "2.0" для JSON-RPC 2.0.</value>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    /// <summary>
    /// Назва методу, що викликається на сервері.
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; init; } = Method;

    /// <summary>
    /// Параметри методу.
    /// </summary>
    /// <remarks>
    /// Може бути об'єктом, масивом або примітивним типом даних,
    /// залежно від вимог методу.
    /// </remarks>
    [JsonPropertyName("params")]
    public JsonElement Params { get; init; } = Params;

    /// <summary>
    /// Ідентифікатор запиту для співставлення з відповіддю.
    /// </summary>
    /// <remarks>
    /// Використовується клієнтом для ідентифікації відповіді на конкретний запит.
    /// </remarks>
    [JsonPropertyName("id")]
    public string Id { get; init; } = Id;
}
