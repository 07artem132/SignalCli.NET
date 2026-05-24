using SignalCli.Exceptions;
using SignalCli.Models.Signal.Devices;

namespace SignalCli.Interfaces.Signal;

/// <summary>
/// Сервіс для роботи з пристроями Signal.
/// </summary>
/// <remarks>
/// Надає методи для зв'язування та управління пристроями, підключеними до
/// облікового запису Signal.
/// </remarks>
public interface ISignalDevices
{
    /// <summary>
    /// Починає процес зв'язування нового пристрою з обліковим записом.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Інформація для зв'язування, включаючи URI для QR-коду.</returns>
    /// <exception cref="InvalidOperationException">Виникає при помилці початку процесу зв'язування.</exception>
    /// <exception cref="JsonRpcException">Виникає при помилці JSON-RPC запиту.</exception>
    /// <exception cref="OperationCanceledException">Виникає, якщо операцію скасовано.</exception>
    Task<StartLinkResponse> StartLinkAsync(
        CancellationToken cancellationToken = default
    );

    // deprecated-shim-removal §3: `StartLink` Async-suffix-less shim видалено.

    /// <summary>
    /// Завершує процес зв'язування нового пристрою з обліковим записом.
    /// </summary>
    /// <param name="deviceLinkUri">URI для зв'язування, отриманий під час сканування QR-коду.</param>
    /// <param name="deviceName">Назва нового пристрою.</param>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Результат зв'язування з номером пристрою.</returns>
    /// <exception cref="ArgumentNullException">Виникає, якщо deviceLinkUri або deviceName дорівнює null.</exception>
    /// <exception cref="InvalidOperationException">Виникає при помилці завершення процесу зв'язування.</exception>
    /// <exception cref="JsonRpcException">Виникає при помилці JSON-RPC запиту.</exception>
    /// <exception cref="OperationCanceledException">Виникає, якщо операцію скасовано.</exception>
    /// <example>
    /// <code>
    /// // Крок 1: Початок процесу зв'язування
    /// var linkInfo = await signalDevices.StartLink(cancellationToken);
    /// 
    /// // Виведення QR-коду для сканування на іншому пристрої
    /// Console.WriteLine($"Відскануйте цей код: {linkInfo.DeviceLinkUri}");
    /// 
    /// // Крок 2: Завершення процесу після сканування
    /// var result = await signalDevices.FinishLink(
    ///     deviceLinkUri: "tsdevice:/?uuid=...",
    ///     deviceName: "Мій новий пристрій",
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<FinishLinkResponse> FinishLinkAsync(
        string deviceLinkUri,
        string deviceName,
        CancellationToken cancellationToken = default
    );

    // deprecated-shim-removal §3: `FinishLink` Async-suffix-less shim видалено.
}