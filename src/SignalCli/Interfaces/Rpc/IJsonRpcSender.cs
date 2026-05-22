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
    /// <typeparam name="TResponse">Тип об'єкта відповіді.</typeparam>
    /// <typeparam name="TRequest">Тип об'єкта запиту.</typeparam>
    /// <param name="method">Назва методу, який потрібно викликати.</param>
    /// <param name="parameters">Параметри для виклику методу.</param>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Типізована відповідь від сервера.</returns>
    /// <exception cref="ArgumentNullException">Виникає, якщо parameters дорівнює null.</exception>
    /// <exception cref="InvalidOperationException">Виникає при помилці десеріалізації відповіді.</exception>
    /// <exception cref="JsonRpcException">Виникає, якщо сервер повернув помилку.</exception>
    /// <exception cref="OperationCanceledException">Виникає, якщо операцію скасовано.</exception>
    /// <example>
    /// <code>
    /// var response = await jsonRpcSender.InvokeMethodAsync&lt;VersionResponse, VersionParameters&gt;(
    ///     "version",
    ///     new VersionParameters(),
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<TResponse> InvokeMethodAsync<TResponse, TRequest>(
        string method,
        TRequest parameters,
        CancellationToken cancellationToken = default
    ) where TResponse : class;
}
