using SignalCli.Exceptions;
using SignalCli.Models.Signal.Contacts;

namespace SignalCli.Interfaces.Signal;

/// <summary>
/// Сервіс для роботи з контактами та identity-keys у Signal (signal-cli-api-coverage Wave 3).
/// </summary>
/// <remarks>
/// 8 RPC методів (Trust розщеплено на 2 type-safe методи: TrustAllKnownKeys + TrustVerified —
/// XOR mutex enforce'иться через окремі API). Read-only методи (<c>listContacts</c>/<c>listIdentities</c>)
/// безпечно ділять акаунт між test-runs; mutating методи (<c>updateContact</c>, <c>block</c>,
/// etc.) тригерять contacts-sync на linked devices.
/// </remarks>
public interface ISignalContacts
{
    /// <summary>
    /// Список усіх відомих контактів акаунту з опційним фільтром.
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see
    /// <c>src/main/java/org/asamk/signal/commands/ListContactsCommand.java</c> @ <c>bda4e7fc</c>.
    /// Read-only.
    /// </remarks>
    /// <param name="account">E.164 номер акаунту.</param>
    /// <param name="recipients">Опційний фільтр — лише ці recipient'и.</param>
    /// <param name="allRecipients">Якщо <c>true</c>, returnн усі known recipients (не лише contacts).</param>
    /// <param name="blocked">Фільтр по blocked-state: <c>null</c>=no-filter.</param>
    /// <param name="name">Substring-фільтр по contact-name або profile-name.</param>
    /// <param name="includeInternal">Якщо <c>true</c>, додає <c>internal</c> sub-object у кожен результат.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <exception cref="ArgumentException">Якщо <paramref name="account"/> порожнє.</exception>
    /// <exception cref="JsonRpcException">При RPC-помилці.</exception>
    Task<ListContactsResponse> ListContactsAsync(
        string account,
        IEnumerable<string>? recipients = null,
        bool allRecipients = false,
        bool? blocked = null,
        string? name = null,
        bool includeInternal = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Список identity-keys для акаунту з опційним фільтром по recipient'у.
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see
    /// <c>src/main/java/org/asamk/signal/commands/ListIdentitiesCommand.java</c> @ <c>bda4e7fc</c>.
    /// Read-only.
    /// </remarks>
    /// <param name="account">E.164 номер акаунту.</param>
    /// <param name="recipient">Опційний recipient identifier (phone/UUID/PNI/username). <c>null</c> = усі.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    Task<ListIdentitiesResponse> ListIdentitiesAsync(
        string account,
        string? recipient = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Trust усіх known identity-keys recipient'а (testing-only path, <c>--trust-all-known-keys</c>).
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see <c>src/main/java/org/asamk/signal/commands/TrustCommand.java</c>
    /// @ <c>bda4e7fc</c>. Mutates trust DB; може тригерити verified-message sync на linked devices.
    /// </remarks>
    /// <param name="account">E.164 номер акаунту.</param>
    /// <param name="recipient">Recipient identifier.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <exception cref="JsonRpcException"><c>-1</c> якщо recipient не зареєстрований або не має identity.</exception>
    Task TrustAllKnownKeysAsync(
        string account,
        string recipient,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Trust identity-key recipient'а за verified safety-number (стандартний production path).
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see <c>src/main/java/org/asamk/signal/commands/TrustCommand.java</c>
    /// @ <c>bda4e7fc</c>. <paramref name="verifiedSafetyNumber"/> може бути 60-digit safety-number
    /// (з пробілами або без), 66-hex-fingerprint, або base64 scannable-safety-number — upstream
    /// disambiguate'ить за довжиною.
    /// </remarks>
    /// <param name="account">E.164 номер акаунту.</param>
    /// <param name="recipient">Recipient identifier.</param>
    /// <param name="verifiedSafetyNumber">60/66-char або base64 safety-number.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <exception cref="JsonRpcException"><c>-1</c> якщо safety-number не співпадає або має invalid format.</exception>
    Task TrustVerifiedAsync(
        string account,
        string recipient,
        string verifiedSafetyNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Оновлює метадані контакту (імена/note/expiration). Не змінює profile-side (для цього <see cref="UpdateProfileAsync"/>).
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see
    /// <c>src/main/java/org/asamk/signal/commands/UpdateContactCommand.java</c> @ <c>bda4e7fc</c>.
    /// </remarks>
    /// <param name="options">Опції з required <c>account</c>+<c>recipient</c>; всі інші поля nullable.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    Task UpdateContactAsync(UpdateContactOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Видаляє контакт у одному з трьох режимів (<see cref="RemoveContactMode"/>).
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see
    /// <c>src/main/java/org/asamk/signal/commands/RemoveContactCommand.java</c> @ <c>bda4e7fc</c>.
    /// <para>
    /// §F9: <see cref="RemoveContactMode"/> enum гарантує валідний XOR на wire (uplstream
    /// argparse mutex group НЕ enforce'иться у JSON-RPC шарі, тож unsanitized direct DTO
    /// усе одно accepts hide=true+forget=true → hide-first-wins; цей service-метод цьому запобігає).
    /// </para>
    /// </remarks>
    /// <param name="account">E.164 номер акаунту.</param>
    /// <param name="recipient">Recipient identifier.</param>
    /// <param name="mode">Режим видалення. Default — <see cref="RemoveContactMode.DeleteContact"/>.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    Task RemoveContactAsync(
        string account,
        string recipient,
        RemoveContactMode mode = RemoveContactMode.DeleteContact,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Оновлює власний profile (givenName/familyName/about/aboutEmoji/mobileCoinAddress + avatar).
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see
    /// <c>src/main/java/org/asamk/signal/commands/UpdateProfileCommand.java</c> @ <c>bda4e7fc</c>.
    /// <para>
    /// §F18: <c>AvatarPath</c> та <c>RemoveAvatar</c> mutex. Builder enforce'ить XOR client-side.
    /// </para>
    /// <para>
    /// <b>Daemon-side filesystem:</b> avatar-шлях читається з ФС signal-cli-процесу, не клієнта.
    /// </para>
    /// </remarks>
    /// <param name="options">Опції profile-update'у.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    Task UpdateProfileAsync(UpdateProfileOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Блокує recipient'ів та/або group-id'и.
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see
    /// <c>src/main/java/org/asamk/signal/commands/BlockCommand.java</c> @ <c>bda4e7fc</c>.
    /// <para>
    /// <b>Caveat:</b> recipient-block та group-block — 2 окремі Manager calls. Якщо перший
    /// fails, другий не виконується (partial-application). Unknown group-id — лише warn-лог,
    /// інші group-id'и продовжують застосуватись.
    /// </para>
    /// <para>
    /// <b>Linked devices:</b> метод throw'ить UserError на linked-device callers
    /// (NotPrimaryDeviceException).
    /// </para>
    /// </remarks>
    /// <param name="account">E.164 номер акаунту.</param>
    /// <param name="recipients">Recipient'и (опційно).</param>
    /// <param name="groupIds">Group-id'и (опційно).</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    Task BlockAsync(
        string account,
        IEnumerable<string>? recipients = null,
        IEnumerable<string>? groupIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Розблоковує recipient'ів та/або group-id'и. Same caveats as <see cref="BlockAsync"/>.
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see
    /// <c>src/main/java/org/asamk/signal/commands/UnblockCommand.java</c> @ <c>bda4e7fc</c>.
    /// </remarks>
    Task UnblockAsync(
        string account,
        IEnumerable<string>? recipients = null,
        IEnumerable<string>? groupIds = null,
        CancellationToken cancellationToken = default);
}
