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

    // ===== signal-cli-api-coverage Wave 8 (`utility-rpc`) — read-only + utility =====

    /// <summary>
    /// Перевіряє registration status для phone numbers AND/OR usernames. Read-only. CDSI lookup.
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see <c>GetUserStatusCommand.java</c> @ <c>bda4e7fc</c>.
    /// <para>
    /// <b>§F5 AND/OR merge:</b> обидва <paramref name="recipients"/> та <paramref name="usernames"/>
    /// опційні; обидва можуть бути non-null одночасно (response merges entries).
    /// Empty + empty → <c>GetUserStatusResponse</c> з порожнім Items.
    /// </para>
    /// </remarks>
    /// <param name="account">E.164 номер caller'а.</param>
    /// <param name="recipients">Phone numbers для перевірки (опційно).</param>
    /// <param name="usernames">Usernames або username-links для перевірки (опційно).</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <exception cref="RateLimitException">CDSI throttled (<c>-5</c>).</exception>
    Task<GetUserStatusResponse> GetUserStatusAsync(
        string account,
        IEnumerable<string>? recipients = null,
        IEnumerable<string>? usernames = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submit'ить captcha-solution для rate-limit challenge. Після success — наступні send-операції
    /// проходять. §F24: <see cref="CaptchaRequiredException"/> при rejected captcha.
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see <c>SubmitRateLimitChallengeCommand.java</c> @ <c>bda4e7fc</c>.
    /// <para>
    /// <b>Workflow:</b> consumer отримує rate-limit error з <c>error.data</c> challenge-token,
    /// розв'язує CAPTCHA на <see href="https://signalcaptchas.org/challenge/generate.html"/>,
    /// викликає цей метод з token + captcha.
    /// </para>
    /// </remarks>
    /// <param name="account">E.164 номер.</param>
    /// <param name="challenge">Opaque challenge token з prior rate-limit error.</param>
    /// <param name="captcha">Captcha solution token.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <exception cref="ArgumentException">Якщо <paramref name="challenge"/> або <paramref name="captcha"/> порожні.</exception>
    /// <exception cref="CaptchaRequiredException">Якщо upstream rejects captcha (<c>-6</c>).</exception>
    /// <exception cref="JsonRpcException">Інші помилки (<c>-3 IoError</c>).</exception>
    Task SubmitRateLimitChallengeAsync(
        string account,
        string challenge,
        string captcha,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Push local contact list до linked devices через SyncMessage.Contacts. <b>One-way push</b>
    /// — inverse direction: для FETCH use <see cref="SyncAccountAsync"/>.
    /// </summary>
    /// <remarks>signal-cli RPC mapping: see <c>SendContactsCommand.java</c> @ <c>bda4e7fc</c>.</remarks>
    /// <param name="account">E.164 номер.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <exception cref="JsonRpcException"><c>-3 IoError</c> при send failure (no linked devices / network).</exception>
    Task SendContactsAsync(string account, CancellationToken cancellationToken = default);
}