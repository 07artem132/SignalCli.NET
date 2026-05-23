using SignalCli.Exceptions;

namespace SignalCli.Interfaces.Rpc;

/// <summary>
/// Відправник запитів JSON-RPC.
/// </summary>
/// <remarks>
/// Відповідає за відправку запитів на JSON-RPC сервер та отримання відповідей.
/// Підтримує типізовані запити та відповіді через узагальнені параметри.
/// </remarks>
public interface IJsonRpcSender
{
    /// <summary>
    /// Асинхронно викликає метод JSON-RPC.
    /// </summary>
    /// <typeparam name="TRequest">Тип об'єкта запиту.</typeparam>
    /// <typeparam name="TResponse">Тип об'єкта відповіді.</typeparam>
    /// <param name="method">Назва методу, який потрібно викликати.</param>
    /// <param name="parameters">Параметри для виклику методу.</param>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Типізована відповідь від сервера.</returns>
    /// <exception cref="ArgumentNullException">Виникає, якщо parameters дорівнює null.</exception>
    /// <exception cref="InvalidOperationException">Виникає при помилці десеріалізації відповіді.</exception>
    /// <exception cref="JsonRpcException">Виникає, якщо сервер повернув помилку.</exception>
    /// <exception cref="OperationCanceledException">Виникає, якщо операцію скасовано.</exception>
    /// <remarks>
    /// post-modernize-tuning §4.27 (audit N11): порядок generic-параметрів узгоджено
    /// з `JsonSerializer.Deserialize&lt;TValue&gt;` — запит спершу, відповідь другою.
    /// </remarks>
    /// <example>
    /// <code>
    /// var response = await jsonRpcSender.InvokeMethodAsync&lt;VersionParameters, VersionResponse&gt;(
    ///     "version",
    ///     new VersionParameters(),
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<TResponse> InvokeMethodAsync<TRequest, TResponse>(
        string method,
        TRequest parameters,
        CancellationToken cancellationToken = default
    ) where TResponse : notnull;
}
