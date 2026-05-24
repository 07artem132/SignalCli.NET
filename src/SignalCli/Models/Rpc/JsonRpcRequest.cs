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
public record JsonRpcRequest(
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] JsonElement Params,
    [property: JsonPropertyName("id")] string Id)
{
    // post-modernize-tuning §4.12 (audit D10): body emptied — positional params already
    // generate {get; init;} properties; manual redeclarations (Method = Method, Params = Params, ...)
    // були cargo-cult'ом, що тільки дублював compiler-generated body. [JsonPropertyName] перенесено
    // на ctor-params через `[property: …]` syntax.

    /// <summary>Версія протоколу JSON-RPC.</summary>
    /// <value>Завжди "2.0" для JSON-RPC 2.0.</value>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";
}
