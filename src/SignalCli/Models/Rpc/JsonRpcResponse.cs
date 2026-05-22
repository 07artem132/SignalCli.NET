using System.Text.Json;
using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Rpc;

/// <summary>
/// Відповідь JSON-RPC.
/// </summary>
/// <remarks>
/// Містить результат виконання методу або інформацію про помилку.
/// </remarks>
[PublicAPI]
public record JsonRpcResponse
{
    /// <summary>
    /// Версія протоколу JSON-RPC.
    /// </summary>
    /// <value>Зазвичай "2.0" для JSON-RPC 2.0.</value>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; }

    /// <summary>
    /// Результат виконання методу.
    /// </summary>
    /// <remarks>
    /// Доступний, якщо виконання методу завершилось успішно.
    /// Має значення null, якщо сталася помилка.
    /// </remarks>
    [JsonPropertyName("result")]
    public JsonElement Result { get; init; }

    /// <summary>
    /// Інформація про помилку.
    /// </summary>
    /// <remarks>
    /// Доступна, якщо під час виконання методу сталася помилка.
    /// Має значення null при успішному виконанні методу.
    /// </remarks>
    [JsonPropertyName("error")]
    public JsonRpcError Error { get; init; }

    /// <summary>
    /// Ідентифікатор запиту, на який надається відповідь.
    /// </summary>
    /// <remarks>
    /// Співпадає з ідентифікатором у відповідному запиті.
    /// </remarks>
    [JsonPropertyName("id")]
    public string Id { get; init; }
}

/// <summary>
/// Типізована відповідь JSON-RPC з результатом вказаного типу.
/// </summary>
/// <typeparam name="T">Тип результату відповіді.</typeparam>
/// <remarks>
/// Використовується для автоматичної десеріалізації результату
/// в об'єкт вказаного типу.
/// </remarks>
[PublicAPI]
public record JsonRpcResponse<T>
{
    /// <summary>
    /// Версія протоколу JSON-RPC.
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; }

    /// <summary>
    /// Результат виконання методу вказаного типу.
    /// </summary>
    [JsonPropertyName("result")]
    public T Result { get; init; }

    /// <summary>
    /// Інформація про помилку, якщо вона сталася.
    /// </summary>
    [JsonPropertyName("error")]
    public JsonRpcError Error { get; init; }

    /// <summary>
    /// Ідентифікатор запиту, на який надається відповідь.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; }
}
