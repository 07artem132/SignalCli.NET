using System.Text.Json.Serialization.Metadata;
using SignalCli.Models.SignalCli;

namespace SignalCli.Interfaces.SignalCli;

/// <summary>
/// Інтерфейс для виклику методів Signal CLI через JSON-RPC.
/// Надає можливість взаємодії з Signal месенджером через його CLI інтерфейс.
/// </summary>
public interface ISignalCliClient
{
    /// <summary>
    /// Асинхронно викликає вказаний метод Signal CLI з переданими параметрами.
    /// AOT-сумісна signature: викликач передає source-generated <see cref="JsonTypeInfo{T}"/>
    /// для запиту й відповіді.
    /// </summary>
    /// <typeparam name="TRequest">Тип об'єкта запиту.</typeparam>
    /// <typeparam name="TResponse">Тип об'єкта відповіді.</typeparam>
    /// <param name="method">Назва методу Signal CLI, який потрібно викликати.</param>
    /// <param name="parameters">Параметри для виклику методу.</param>
    /// <param name="requestTypeInfo">Source-gen метадані для серіалізації запиту (з <c>SignalJsonContext.Default</c>).</param>
    /// <param name="responseTypeInfo">Source-gen метадані для десеріалізації відповіді.</param>
    /// <param name="cancellationToken">Токен скасування для переривання операції.</param>
    /// <returns>Об'єкт відповіді від Signal CLI.</returns>
    /// <remarks>
    /// post-modernize-tuning §6.7 (audit P6): public-signature змінено для AOT-сумісності.
    /// Раніше метод тягнув reflection через `<see cref="System.Text.Json.JsonSerializer"/>` generic-overload'и;
    /// тепер metadata-провайдер є явним аргументом. Це **breaking** — попередній shape з 3-ма
    /// аргументами видалено (overload-resolution не розрізняє методи лише за наявністю опціональних
    /// параметрів через double-default-conflict).
    /// </remarks>
    Task<TResponse> InvokeMethodAsync<TRequest, TResponse>(
        string method,
        TRequest parameters,
        JsonTypeInfo<TRequest> requestTypeInfo,
        JsonTypeInfo<TResponse> responseTypeInfo,
        CancellationToken cancellationToken = default
    ) where TResponse : notnull;

    /// <summary>
    /// Асинхронно отримує інформацію про версію Signal CLI.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування для переривання операції.</param>
    /// <returns>Об'єкт з інформацією про версію Signal CLI.</returns>
    Task<VersionResponse> VersionAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Застаріле: використовуйте <see cref="VersionAsync"/>.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування для переривання операції.</param>
    /// <returns>Об'єкт з інформацією про версію Signal CLI.</returns>
    [Obsolete("Use VersionAsync; will be removed in 3.0")]
    public Task<VersionResponse> Version(
        CancellationToken cancellationToken = default
    ) => VersionAsync(cancellationToken);
}
