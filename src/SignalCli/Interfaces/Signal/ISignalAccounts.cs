using SignalCli.Exceptions;
using SignalCli.Models.Signal.Accounts;

namespace SignalCli.Interfaces.Signal;

/// <summary>
/// Сервіс для роботи з обліковими записами Signal.
/// </summary>
/// <remarks>
/// Надає методи для отримання інформації про зареєстровані акаунти
/// та керування їхніми налаштуваннями.
/// </remarks>
public interface ISignalAccounts
{
    /// <summary>
    /// Отримує список зареєстрованих акаунтів Signal.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Список зареєстрованих акаунтів.</returns>
    /// <exception cref="InvalidOperationException">Виникає при помилці отримання списку акаунтів.</exception>
    /// <exception cref="JsonRpcException">Виникає при помилці JSON-RPC запиту.</exception>
    /// <exception cref="OperationCanceledException">Виникає, якщо операцію скасовано.</exception>
    /// <example>
    /// <code>
    /// var accounts = await signalAccounts.ListAccounts(cancellationToken);
    /// foreach (var account in accounts)
    /// {
    ///     Console.WriteLine($"Зареєстрований акаунт: {account.Number}");
    /// }
    /// </code>
    /// </example>
    Task<ListAccountsResponse> ListAccountsAsync(
        CancellationToken cancellationToken = default
    );

    // deprecated-shim-removal §3: `ListAccounts` Async-suffix-less shim видалено.
    /// <summary>
    /// Надішліть повідомлення із запитом на синхронізацію на основний пристрій (для груп, контактів, ...).
    /// Основний пристрій відповість повідомленням синхронізації з повним списком контактів і груп.
    /// Синхронізація проводиться в фоновому режимі.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Пустий об'єкт.</returns>
    /// <exception cref="InvalidOperationException">Виникає при помилці отримання списку акаунтів.</exception>
    /// <exception cref="JsonRpcException">Виникає при помилці JSON-RPC запиту.</exception>
    /// <exception cref="OperationCanceledException">Виникає, якщо операцію скасовано.</exception>

    Task<SyncAccountsResponse> SyncAccountAsync(CancellationToken cancellationToken = default);

    // deprecated-shim-removal §3: `SyncAccount` Async-suffix-less shim видалено.

    // ===== signal-cli-api-coverage Wave 6 (account-lifecycle, DESTRUCTIVE, opt-in gated) =====
    //
    // ⚠ ВСІ 8 методів нижче — destructive. Гейтінг через
    // SignalCliOptions.EnableDestructiveOperations (default false). Без opt-in кидають
    // InvalidOperationException на першому виклику ПЕРЕД RPC dispatch.

    /// <summary>
    /// DESTRUCTIVE. Оновлює server-side attribute'и акаунту (deviceName, unidentified-sender policy,
    /// discoverability, number-sharing) і опційно set/delete username.
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see <c>UpdateAccountCommand.java</c> @ <c>bda4e7fc</c>.
    /// <para><b>§F3 NumberSharing</b> — bool, не enum (upstream argparse <c>type(Boolean.class)</c>).</para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Якщо <c>SignalCliOptions.EnableDestructiveOperations = false</c>.</exception>
    Task<UpdateAccountResponse> UpdateAccountAsync(UpdateAccountOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// DESTRUCTIVE. Unregister акаунт. З <paramref name="deleteAccount"/>=<c>true</c> — <b>irreversibly</b>
    /// deletes account з Signal серверів.
    /// </summary>
    /// <remarks>signal-cli RPC mapping: see <c>UnregisterCommand.java</c> @ <c>bda4e7fc</c>.</remarks>
    /// <exception cref="InvalidOperationException">Якщо <c>EnableDestructiveOperations = false</c>.</exception>
    Task UnregisterAsync(string account, bool deleteAccount = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// DESTRUCTIVE — <b>CANNOT BE UNDONE</b>. Wipe'ає local account directory.
    /// </summary>
    /// <remarks>signal-cli RPC mapping: see <c>DeleteLocalAccountDataCommand.java</c> @ <c>bda4e7fc</c>.</remarks>
    /// <exception cref="InvalidOperationException">Якщо <c>EnableDestructiveOperations = false</c>.</exception>
    Task DeleteLocalAccountDataAsync(string account, bool ignoreRegistered = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// DESTRUCTIVE. Розпочинає phone-number-change flow. §F4 <c>Voice</c> — bool (default false=SMS).
    /// </summary>
    /// <remarks>signal-cli RPC mapping: see <c>StartChangeNumberCommand.java</c> @ <c>bda4e7fc</c>.</remarks>
    /// <exception cref="InvalidOperationException">Якщо <c>EnableDestructiveOperations = false</c>.</exception>
    Task StartChangeNumberAsync(StartChangeNumberOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// DESTRUCTIVE. Завершує phone-number change. OLD number більше не associated.
    /// </summary>
    /// <remarks>signal-cli RPC mapping: see <c>FinishChangeNumberCommand.java</c> @ <c>bda4e7fc</c>.</remarks>
    /// <exception cref="InvalidOperationException">Якщо <c>EnableDestructiveOperations = false</c>.</exception>
    Task FinishChangeNumberAsync(FinishChangeNumberOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// DESTRUCTIVE. Оновлює per-account configuration (4 nullable bool flags); syncs до linked devices.
    /// </summary>
    /// <remarks>signal-cli RPC mapping: see <c>UpdateConfigurationCommand.java</c> @ <c>bda4e7fc</c>.</remarks>
    /// <exception cref="InvalidOperationException">Якщо <c>EnableDestructiveOperations = false</c>.</exception>
    Task UpdateConfigurationAsync(UpdateConfigurationOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// DESTRUCTIVE. Sets Signal registration-lock PIN через Secure Value Recovery.
    /// </summary>
    /// <remarks>signal-cli RPC mapping: see <c>SetPinCommand.java</c> @ <c>bda4e7fc</c>. Client-side enforce: pin ≥4 chars.</remarks>
    /// <exception cref="InvalidOperationException">Якщо <c>EnableDestructiveOperations = false</c>.</exception>
    /// <exception cref="ArgumentException">Якщо <paramref name="pin"/> має менше 4 chars.</exception>
    Task SetPinAsync(string account, string pin, CancellationToken cancellationToken = default);

    /// <summary>
    /// DESTRUCTIVE. Removes registration-lock PIN. Idempotent server-side.
    /// </summary>
    /// <remarks>signal-cli RPC mapping: see <c>RemovePinCommand.java</c> @ <c>bda4e7fc</c>.</remarks>
    /// <exception cref="InvalidOperationException">Якщо <c>EnableDestructiveOperations = false</c>.</exception>
    Task RemovePinAsync(string account, CancellationToken cancellationToken = default);
}