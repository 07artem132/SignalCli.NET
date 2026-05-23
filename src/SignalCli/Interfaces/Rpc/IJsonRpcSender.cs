using System.Text.Json.Serialization.Metadata;
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
    /// Асинхронно викликає метод JSON-RPC. AOT-сумісна signature: викликач передає
    /// source-generated <see cref="JsonTypeInfo{T}"/>-метадані для запиту й відповіді.
    /// </summary>
    /// <typeparam name="TRequest">Тип об'єкта запиту.</typeparam>
    /// <typeparam name="TResponse">Тип об'єкта відповіді.</typeparam>
    /// <param name="method">Назва методу, який потрібно викликати.</param>
    /// <param name="parameters">Параметри для виклику методу.</param>
    /// <param name="requestTypeInfo">
    /// Source-gen метадані для серіалізації <typeparamref name="TRequest"/>. Беруться з
    /// <c>SignalJsonContext.Default.&lt;TypeName&gt;</c>.
    /// </param>
    /// <param name="responseTypeInfo">
    /// Source-gen метадані для десеріалізації <typeparamref name="TResponse"/>.
    /// </param>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Типізована відповідь від сервера.</returns>
    /// <exception cref="ArgumentNullException">Виникає, якщо <paramref name="parameters"/>, <paramref name="requestTypeInfo"/> або <paramref name="responseTypeInfo"/> дорівнюють null.</exception>
    /// <exception cref="InvalidOperationException">Виникає при помилці десеріалізації відповіді.</exception>
    /// <exception cref="JsonRpcException">Виникає, якщо сервер повернув помилку.</exception>
    /// <exception cref="OperationCanceledException">Виникає, якщо операцію скасовано.</exception>
    /// <remarks>
    /// post-modernize-tuning §6.7 (audit P6): public-signature змінено для AOT-сумісності.
    /// Раніше метод викликав <c>JsonSerializer.SerializeToElement&lt;T&gt;(_, options)</c> й
    /// <c>Deserialize&lt;T&gt;(_, options)</c>, які тягли reflection-based resolver (IL2026/IL3050).
    /// Тепер metadata-провайдер передається явно — `&lt;IsAotCompatible&gt;true&lt;/IsAotCompatible&gt;`
    /// білдиться без warning'ів.
    /// </remarks>
    /// <example>
    /// <code>
    /// var response = await jsonRpcSender.InvokeMethodAsync(
    ///     "version",
    ///     new VersionParameters(),
    ///     SignalJsonContext.Default.VersionParameters,
    ///     SignalJsonContext.Default.VersionResponse,
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<TResponse> InvokeMethodAsync<TRequest, TResponse>(
        string method,
        TRequest parameters,
        JsonTypeInfo<TRequest> requestTypeInfo,
        JsonTypeInfo<TResponse> responseTypeInfo,
        CancellationToken cancellationToken = default
    ) where TResponse : notnull;
}
