using System.Diagnostics.CodeAnalysis;
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
    /// <param name="timeout">
    /// add-per-call-rpc-timeout: опціональний per-call таймаут RPC-виклику <c>finishLink</c>, що
    /// переважає клієнтський default (<c>RequestTimeoutSeconds</c>, зазвичай 30 с) лише для цієї
    /// операції. Причина: <c>finishLink</c> має довгу interactive-фазу — primary device мусить
    /// вручну відсканувати QR-код і підтвердити зв'язування, що легко перевищує глобальний
    /// таймаут. Передайте, наприклад, <c>TimeSpan.FromSeconds(150)</c>, щоб дати користувачеві час.
    /// <c>null</c> — діє клієнтський default (поведінка незмінна).
    /// </param>
    /// <returns>Результат зв'язування з номером пристрою.</returns>
    /// <exception cref="ArgumentNullException">Виникає, якщо deviceLinkUri або deviceName дорівнює null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Виникає, якщо <paramref name="timeout"/> від'ємний.</exception>
    /// <exception cref="InvalidOperationException">Виникає при помилці завершення процесу зв'язування.</exception>
    /// <exception cref="JsonRpcException">Виникає при помилці JSON-RPC запиту.</exception>
    /// <exception cref="TimeoutException">Виникає, якщо відповідь не отримано за <paramref name="timeout"/> (якщо задано) або за клієнтським default'ом.</exception>
    /// <exception cref="OperationCanceledException">Виникає, якщо операцію скасовано.</exception>
    /// <example>
    /// <code>
    /// // Крок 1: Початок процесу зв'язування
    /// var linkInfo = await signalDevices.StartLinkAsync(cancellationToken);
    ///
    /// // Виведення QR-коду для сканування на іншому пристрої
    /// Console.WriteLine($"Відскануйте цей код: {linkInfo.DeviceLinkUri}");
    ///
    /// // Крок 2: Завершення процесу після сканування (з довшим per-call таймаутом на ручний скан)
    /// var result = await signalDevices.FinishLinkAsync(
    ///     deviceLinkUri: "sgnl://linkdevice?uuid=...",
    ///     deviceName: "Мій новий пристрій",
    ///     cancellationToken,
    ///     timeout: TimeSpan.FromSeconds(150));
    /// </code>
    /// </example>
    [SuppressMessage("Design", "CA1068:CancellationToken parameters must come last",
        Justification = "add-per-call-rpc-timeout: опц. timeout свідомо доданий ПІСЛЯ cancellationToken — " +
            "це API-additive seam. Постановка timeout перед ct зламала б call-site-сумісність: наявні " +
            "позиційні ct-аргументи зв'язалися б із timeout (TimeSpan?) → compile-error у консумерів.")]
    Task<FinishLinkResponse> FinishLinkAsync(
        string deviceLinkUri,
        string deviceName,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null
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