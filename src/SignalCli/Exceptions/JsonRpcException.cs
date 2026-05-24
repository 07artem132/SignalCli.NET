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
    // post-modernize-tuning §4.21 (audit N14, CA1032): sentinel "no error info"
    // повертається з ctor-ів, що не приймають JsonRpcError напряму. Використовує
    // канонічний JSON-RPC 2.0 код -32603 ("Internal error"), не нестандартний -32000.
    private static JsonRpcError DefaultError => new() { Code = -32603, Message = "JSON-RPC error" };

    /// <summary>
    /// Деталізована інформація про помилку JSON-RPC.
    /// </summary>
    /// <value>Об'єкт <see cref="JsonRpcError"/> з кодом та повідомленням помилки.</value>
    public JsonRpcError Error { get; init; }

    /// <summary>
    /// signal-cli-protocol-alignment (typed-rpc-errors): типізований доступ до коду помилки.
    /// Повертає <see cref="JsonRpcErrorCode"/>-значення, якщо <see cref="Error"/>.Code входить
    /// до відомого набору (standard JSON-RPC 2.0 <c>-32700..-32603</c> + signal-cli custom
    /// <c>-1..-6</c>); інакше <c>null</c> (forward-compat для нових кодів у майбутніх signal-cli).
    /// </summary>
    /// <remarks>
    /// Використовуй замість порівняння <see cref="Error"/>.Code з magic-числами:
    /// <code>
    /// catch (JsonRpcException ex) when (ex.KnownCode == JsonRpcErrorCode.UserError) { ... }
    /// </code>
    /// Для двох high-leverage кодів (<c>-5</c> RateLimit, <c>-4</c> UntrustedIdentity) є окремі
    /// derived-типи — <see cref="RateLimitException"/>, <see cref="UntrustedIdentityException"/>.
    /// </remarks>
    public JsonRpcErrorCode? KnownCode =>
        Enum.IsDefined(typeof(JsonRpcErrorCode), Error.Code) ? (JsonRpcErrorCode)Error.Code : null;

    /// <summary>
    /// post-modernize-tuning §4.21 (CA1032): стандартний parameterless ctor.
    /// <see cref="Error"/> заповнюється sentinel-значенням (`code -32603, "JSON-RPC error"`).
    /// </summary>
    public JsonRpcException() : base("JSON-RPC error")
    {
        Error = DefaultError;
    }

    /// <summary>
    /// post-modernize-tuning §4.21 (CA1032): стандартний message-only ctor.
    /// <see cref="Error"/> заповнюється sentinel-значенням з переданим повідомленням
    /// і канонічним кодом -32603 ("Internal error" per JSON-RPC 2.0).
    /// </summary>
    /// <param name="message">Повідомлення про помилку.</param>
    public JsonRpcException(string message) : base(message)
    {
        Error = new JsonRpcError { Code = -32603, Message = message };
    }

    /// <summary>
    /// post-modernize-tuning §4.21 (CA1032): стандартний (message, innerException) ctor.
    /// <see cref="Error"/> заповнюється канонічним -32603 + переданим повідомленням.
    /// </summary>
    /// <param name="message">Повідомлення про помилку.</param>
    /// <param name="innerException">Внутрішній виняток, що спричинив цю помилку.</param>
    public JsonRpcException(string message, Exception innerException) : base(message, innerException)
    {
        Error = new JsonRpcError { Code = -32603, Message = message };
    }

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

    // post-modernize-tuning §4.22 (audit N15/E3): legacy `JsonRpcException(string, Exception?)`
    // з нестандартним кодом -32000 ВИДАЛЕНО. Аудит підтвердив zero внутрішніх call sites;
    // зовнішні consumers мають мігрувати на канонічний `JsonRpcException(string, Exception)`
    // (-32603 per JSON-RPC 2.0) або передавати власний `JsonRpcError`.
}