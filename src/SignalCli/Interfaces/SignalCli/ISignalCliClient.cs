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
    /// </summary>
    /// <typeparam name="TRequest">Тип об'єкта запиту.</typeparam>
    /// <typeparam name="TResponse">Тип об'єкта відповіді.</typeparam>
    /// <param name="method">Назва методу Signal CLI, який потрібно викликати.</param>
    /// <param name="parameters">Параметри для виклику методу.</param>
    /// <param name="cancellationToken">Токен скасування для переривання операції.</param>
    /// <returns>Об'єкт відповіді від Signal CLI.</returns>
    /// <remarks>
    /// post-modernize-tuning §4.27 (audit N11): порядок generic-параметрів узгоджено
    /// з `JsonSerializer.Deserialize&lt;TValue&gt;` — запит спершу, відповідь другою.
    /// Сумісність-shim неможливий: C# overload-resolution не розрізняє методи
    /// з однаковим runtime-сигнатурою, що відрізняються лише порядком typeparam'ів.
    /// </remarks>
    Task<TResponse> InvokeMethodAsync<TRequest, TResponse>(
        string method,
        TRequest parameters,
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
