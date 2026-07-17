using System.Diagnostics.CodeAnalysis;
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
    /// <param name="timeout">
    /// add-per-call-rpc-timeout: опціональний per-call таймаут, що переважає клієнтський default
    /// (<c>RequestTimeoutSeconds</c>) лише для цього виклику. <c>null</c> або
    /// <see cref="TimeSpan.Zero"/> — «не задано»: діє клієнтський default (поведінка незмінна).
    /// Додатне значення — для довгих interactive-операцій (напр. <c>finishLink</c> з ручним
    /// QR-скануванням), триваліших за глобальний таймаут. Від'ємне значення →
    /// <see cref="ArgumentOutOfRangeException"/>.
    /// </param>
    /// <returns>Об'єкт відповіді від Signal CLI.</returns>
    /// <remarks>
    /// post-modernize-tuning §6.7 (audit P6): public-signature змінено для AOT-сумісності.
    /// Раніше метод тягнув reflection через `<see cref="System.Text.Json.JsonSerializer"/>` generic-overload'и;
    /// тепер metadata-провайдер є явним аргументом. Це **breaking** — попередній shape з 3-ма
    /// аргументами видалено (overload-resolution не розрізняє методи лише за наявністю опціональних
    /// параметрів через double-default-conflict).
    /// <para>
    /// add-per-call-rpc-timeout: опціональний <paramref name="timeout"/> доданий ОСТАННІМ — це
    /// API-additive зміна (існуючі call-site'и без аргументу компілюються й поводяться незмінно).
    /// </para>
    /// </remarks>
    [SuppressMessage("Design", "CA1068:CancellationToken parameters must come last",
        Justification = "add-per-call-rpc-timeout: опц. timeout свідомо доданий ПІСЛЯ cancellationToken — " +
            "це API-additive seam. Постановка timeout перед ct зламала б call-site-сумісність: наявні " +
            "позиційні ct-аргументи зв'язалися б із timeout (TimeSpan?) → compile-error у консумерів.")]
    Task<TResponse> InvokeMethodAsync<TRequest, TResponse>(
        string method,
        TRequest parameters,
        JsonTypeInfo<TRequest> requestTypeInfo,
        JsonTypeInfo<TResponse> responseTypeInfo,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null
    ) where TResponse : notnull;

    /// <summary>
    /// Асинхронно отримує інформацію про версію Signal CLI.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування для переривання операції.</param>
    /// <returns>Об'єкт з інформацією про версію Signal CLI.</returns>
    Task<VersionResponse> VersionAsync(
        CancellationToken cancellationToken = default
    );

    // deprecated-shim-removal §2 (remove-version-dim): `Version()` DIM shim видалено.
    // Усі consumer'и → `VersionAsync()`. sed-friendly migration: s/\.Version(/\.VersionAsync(/g.
}
