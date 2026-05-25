using SignalCli.Exceptions;
using SignalCli.Models.Signal.Groups;

namespace SignalCli.Interfaces.Signal;

/// <summary>
/// Сервіс для роботи з групами Signal.
/// </summary>
/// <remarks>
/// Надає методи для отримання списку груп та керування ними.
/// </remarks>
public interface ISignalGroups
{
    /// <summary>
    /// Отримує список груп для вказаного акаунту.
    /// </summary>
    /// <param name="account">Ідентифікатор акаунту (номер телефону).</param>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Список груп з детальною інформацією.</returns>
    /// <exception cref="ArgumentNullException">Виникає, якщо account дорівнює null або порожній.</exception>
    /// <exception cref="InvalidOperationException">Виникає при помилці отримання списку груп.</exception>
    /// <exception cref="JsonRpcException">Виникає при помилці JSON-RPC запиту.</exception>
    /// <exception cref="OperationCanceledException">Виникає, якщо операцію скасовано.</exception>
    /// <example>
    /// <code>
    /// var groups = await signalGroups.ListGroups("+380501234567", cancellationToken);
    /// foreach (var group in groups)
    /// {
    ///     Console.WriteLine($"Група: {group.Name}, Учасників: {group.Members.Count}");
    /// }
    /// </code>
    /// </example>
    Task<ListGroupsResponse> ListGroupsAsync(
        string account,
        CancellationToken cancellationToken = default
    );

    // deprecated-shim-removal §3: `ListGroups` Async-suffix-less shim видалено.

    /// <summary>
    /// Приєднується до Signal-групи за invitation-посиланням (<c>signal.group/#...</c>) (signal-cli-api-coverage Wave 2).
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see <c>src/main/java/org/asamk/signal/commands/JoinGroupCommand.java</c>
    /// @ <c>bda4e7fc</c>. Field names mirror Jackson record exactly.
    /// <para>
    /// <b>§F13:</b> Response поле <see cref="JoinGroupResponse.OnlyRequested"/> — <c>bool?</c>:
    /// <c>null</c> = direct join (повноправний member), <c>true</c> = pending admin approval.
    /// Wire ніколи не повертає <c>false</c> для цього поля.
    /// </para>
    /// </remarks>
    /// <param name="account">E.164 номер акаунту.</param>
    /// <param name="uri">Signal group invitation link.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <returns>Новий group-id + send-results.</returns>
    /// <exception cref="ArgumentException">Якщо account або uri порожні.</exception>
    /// <exception cref="JsonRpcException">Invalid link / unknown version / inactive / pending-admin-approval (<c>-1</c>) або IOError (<c>-3</c>).</exception>
    /// <exception cref="OperationCanceledException">При скасуванні.</exception>
    /// <exception cref="TimeoutException">При тайм-ауті.</exception>
    Task<JoinGroupResponse> JoinGroupAsync(
        string account,
        string uri,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Оновлює існуючу групу АБО створює нову (dual-mode) (signal-cli-api-coverage Wave 2).
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see <c>src/main/java/org/asamk/signal/commands/UpdateGroupCommand.java</c>
    /// @ <c>bda4e7fc</c>. Field names mirror Jackson record exactly.
    /// <para>
    /// <b>§F14 dual-mode:</b> якщо <see cref="UpdateGroupOptions.GroupId"/> == <c>null</c>,
    /// upstream signal-cli викликає <c>createGroup</c> ПЕРШИМ, далі <c>updateGroup</c> з
    /// решткою полів. У відповіді <see cref="UpdateGroupResponse.GroupId"/> присутній
    /// ТІЛЬКИ на create-path.
    /// </para>
    /// <para>
    /// <b>§F10 IOException divergence:</b> IOException у цьому методі мапиться на
    /// <c>-32603 INTERNAL_ERROR</c> (через <c>UnexpectedErrorException</c>), не на <c>-3 IoError</c>
    /// як у joinGroup/quitGroup. Inconsistent upstream-choice, не нормалізуємо.
    /// </para>
    /// </remarks>
    /// <param name="options">Опції з усіма необов'язковими полями (Name/Description/Members/Admins/Permissions/etc.).</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <returns>Send-results + новий groupId (тільки на create-path).</returns>
    /// <exception cref="ArgumentNullException">Якщо <paramref name="options"/> = null.</exception>
    /// <exception cref="GroupAdminRequiredException">Якщо акаунт — не admin при mutating операції (<c>-1</c> + "admin").</exception>
    /// <exception cref="JsonRpcException">Invalid group id / phone / link state / permission / etc. (<c>-1</c>) або IOException (<c>-32603</c>).</exception>
    /// <exception cref="OperationCanceledException">При скасуванні.</exception>
    /// <exception cref="TimeoutException">При тайм-ауті.</exception>
    Task<UpdateGroupResponse> UpdateGroupAsync(
        UpdateGroupOptions options,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Залишає Signal-групу. <b>Idempotent</b>: якщо акаунт уже не member, повертає response
    /// з <see cref="QuitGroupResponse.WasAlreadyNotMember"/> = <c>true</c> замість викидання
    /// винятку (signal-cli-api-coverage Wave 2).
    /// </summary>
    /// <remarks>
    /// signal-cli RPC mapping: see <c>src/main/java/org/asamk/signal/commands/QuitGroupCommand.java</c>
    /// @ <c>bda4e7fc</c>. Field names mirror Jackson record exactly.
    /// <para>
    /// <b>§F8 idempotent:</b> upstream silently catches <c>NotAGroupMemberException</c>,
    /// logs "User is not a group member", повертає порожній <c>{}</c>. Per CLAUDE.md rule #14
    /// ми трактуємо це як success-no-op (а не помилку).
    /// </para>
    /// <para>
    /// <b>delete=true:</b> локально видаляє group-data після quit-send.
    /// </para>
    /// </remarks>
    /// <param name="account">E.164 номер акаунту.</param>
    /// <param name="groupId">Base64 group id.</param>
    /// <param name="deleteLocally">Якщо <c>true</c>, локально видалити group-data після quit. На wire — поле <c>"delete"</c>.</param>
    /// <param name="admins">Список нових admin'ів (потрібен якщо акаунт — last admin групи).</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <returns>Send-результати (або marker idempotent-no-op якщо вже не member).</returns>
    /// <exception cref="ArgumentException">Якщо account/groupId порожні.</exception>
    /// <exception cref="JsonRpcException">Invalid group id / phone / last-admin-without-successor / IO error.</exception>
    /// <exception cref="OperationCanceledException">При скасуванні.</exception>
    /// <exception cref="TimeoutException">При тайм-ауті.</exception>
    Task<QuitGroupResponse> QuitGroupAsync(
        string account,
        string groupId,
        bool deleteLocally = false,
        IEnumerable<string>? admins = null,
        CancellationToken cancellationToken = default
    );
}