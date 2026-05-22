using SignalCli.Models.Rpc;

namespace SignalCli.Exceptions;

/// <summary>
/// Виняток, що виникає при помилках JSON-RPC взаємодії.
/// </summary>
/// <remarks>
/// Використовується для обробки помилок, які повертає JSON-RPC сервер або
/// виникають під час серіалізації/десеріалізації JSON-RPC повідомлень.
/// Містить детальну інформацію про помилку в властивості <see cref="Error"/>.
/// </remarks>
public class JsonRpcException : Exception
{
    /// <summary>
    /// Деталізована інформація про помилку JSON-RPC.
    /// </summary>
    /// <value>Об'єкт <see cref="JsonRpcError"/> з кодом та повідомленням помилки.</value>
    public JsonRpcError Error { get; init; }

    /// <summary>
    /// Створює новий екземпляр винятку з інформацією про помилку JSON-RPC.
    /// </summary>
    /// <param name="error">Об'єкт з інформацією про помилку JSON-RPC.</param>
    /// <exception cref="ArgumentNullException">Виникає, якщо параметр error дорівнює null.</exception>
    public JsonRpcException(JsonRpcError error)
        : base(error?.Message ?? "Невідома помилка JSON-RPC")
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }

    /// <summary>
    /// Створює новий екземпляр винятку з текстовим повідомленням та опціональним внутрішнім винятком.
    /// </summary>
    /// <param name="message">Повідомлення про помилку.</param>
    /// <param name="inner">Внутрішній виняток, що спричинив цю помилку.</param>
    /// <remarks>
    /// Автоматично створює об'єкт <see cref="JsonRpcError"/> з кодом -32000 (внутрішня помилка сервера)
    /// та вказаним повідомленням.
    /// </remarks>
    public JsonRpcException(string message, Exception? inner = null)
        : base(message, inner)
    {
        Error = new JsonRpcError
        {
            Code = -32000,
            Message = message
        };
    }
}