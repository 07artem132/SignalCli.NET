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

    /// <summary>
    /// Додає secondary device до акаунту (primary-перспектива) (signal-cli-api-coverage Wave 5).
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see
    /// <c>src/main/java/org/asamk/signal/commands/AddDeviceCommand.java</c> @ <c>bda4e7fc</c>.
    /// <para>
    /// <b>Mental model:</b> secondary device генерує key-pair, кодує <c>uuid</c> + public-key
    /// у <c>sgnl://linkdevice?uuid=...&amp;pub_key=...</c> URL і відображає QR. Primary
    /// (цей API) приймає URL і виконує provisioning handshake.
    /// </para>
    /// <para>
    /// <b>Blocking:</b> key-exchange round-trip з secondary через Signal server — секунди.
    /// </para>
    /// <para>
    /// <b>Linked-device callers:</b> якщо цей signal-cli — secondary, throw'ить <c>-1 UserError</c>.
    /// </para>
    /// </remarks>
    Task AddDeviceAsync(string account, string uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Перелічує всі linked devices акаунту (server-side fetch, не local-cache).
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see
    /// <c>src/main/java/org/asamk/signal/commands/ListDevicesCommand.java</c> @ <c>bda4e7fc</c>.
    /// <para>
    /// <b>§F6 quirk:</b> <see cref="Device"/> має 4 поля — wire НЕ містить <c>isThisDevice</c>.
    /// Self-identification — за <c>Id == 1</c> (primary).
    /// </para>
    /// </remarks>
    Task<ListDevicesResponse> ListDevicesAsync(string account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Видаляє linked secondary device. <b>Destructive</b> — secondary одразу втрачає capability.
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see
    /// <c>src/main/java/org/asamk/signal/commands/RemoveDeviceCommand.java</c> @ <c>bda4e7fc</c>.
    /// <para>
    /// Видалений device — no undo path; secondary мусить re-link через <see cref="AddDeviceAsync"/>.
    /// </para>
    /// </remarks>
    Task RemoveDeviceAsync(string account, int deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Оновлює назву linked device'а (encrypted server-side).
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see
    /// <c>src/main/java/org/asamk/signal/commands/UpdateDeviceCommand.java</c> @ <c>bda4e7fc</c>.
    /// <para>
    /// <b>§F12:</b> <paramref name="deviceName"/> encrypted device's identity-key'ом перед transmission.
    /// .NET сервіс НЕ логує <paramref name="deviceName"/> вище <c>Trace</c> (CLAUDE.md rule #1).
    /// </para>
    /// </remarks>
    Task UpdateDeviceAsync(string account, int deviceId, string deviceName, CancellationToken cancellationToken = default);
}