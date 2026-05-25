using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Groups;

/// <summary>
/// Опції для <c>updateGroup</c> RPC. Створюється через <see cref="Builder"/>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 2 (`groups-crud`). Wire-shape pinned до
/// <c>src/main/java/org/asamk/signal/commands/UpdateGroupCommand.java @ bda4e7fc</c> +
/// <c>lib/src/main/java/org/asamk/signal/manager/api/UpdateGroup.java @ bda4e7fc</c>.
/// <para>
/// <b>§F14 dual-mode:</b> якщо <see cref="GroupId"/> == <c>null</c>, upstream signal-cli
/// викликає <c>m.createGroup(name, members, avatar)</c> ПЕРШИМ, далі <c>m.updateGroup(...)</c>.
/// Тобто <i>один і той же RPC = create OR update</i> залежно від наявності GroupId.
/// На .NET-сайті це single API, dual-semantic; XMLDoc на <c>UpdateGroupAsync</c> чітко
/// документує цю двозначність. У відповіді <see cref="UpdateGroupResponse.GroupId"/>
/// присутній ТІЛЬКИ при create-path (новий group); при update-path — <c>null</c>.
/// </para>
/// </remarks>
[PublicAPI]
public sealed record UpdateGroupOptions
{
    /// <summary>E.164 номер акаунту-власника.</summary>
    public string Account { get; private set; } = string.Empty;

    /// <summary>
    /// §F14: <c>null</c> → CREATE-then-update (новий group); інакше — UPDATE existing.
    /// Base64 group id.
    /// </summary>
    public string? GroupId { get; private set; }

    /// <summary>Нова назва групи.</summary>
    public string? Name { get; private set; }

    /// <summary>Новий опис групи.</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Локальний шлях до avatar-файлу. <b>Privacy:</b> signal-cli читає файл з ФС у власному
    /// process'і; шлях НЕ логуй вище <c>Trace</c> (CLAUDE.md rule #1).
    /// </summary>
    public string? AvatarPath { get; private set; }

    /// <summary>Members додати (phone/UUID/username).</summary>
    public IReadOnlyList<string>? Members { get; private set; }

    /// <summary>Members видалити.</summary>
    public IReadOnlyList<string>? RemoveMembers { get; private set; }

    /// <summary>Members промотити в admin'и.</summary>
    public IReadOnlyList<string>? Admins { get; private set; }

    /// <summary>Members зняти з admin'ів.</summary>
    public IReadOnlyList<string>? RemoveAdmins { get; private set; }

    /// <summary>Members забанити.</summary>
    public IReadOnlyList<string>? Bans { get; private set; }

    /// <summary>Members розбанити.</summary>
    public IReadOnlyList<string>? Unbans { get; private set; }

    /// <summary>Скинути group-invite-link password (regenerate).</summary>
    public bool ResetLink { get; private set; }

    /// <summary>Стан group-invite-link.</summary>
    public GroupLinkState? LinkState { get; private set; }

    /// <summary>Permission на add-member (хто може додавати).</summary>
    public GroupPermission? PermissionAddMember { get; private set; }

    /// <summary>Permission на edit-details (хто може редагувати details групи).</summary>
    public GroupPermission? PermissionEditDetails { get; private set; }

    /// <summary>
    /// Permission на send-messages. Upstream нормалізує: <see cref="GroupPermission.OnlyAdmins"/>
    /// → <c>isAnnouncementGroup=true</c>. У .NET ми лишаємо semantic-symmetric з іншими permission-полями.
    /// </summary>
    public GroupPermission? PermissionSendMessages { get; private set; }

    /// <summary>Disappearing-messages timer у секундах. <c>null</c> = не міняти, <c>0</c> = вимкнути.</summary>
    public int? Expiration { get; private set; }

    /// <summary>Builder для <see cref="UpdateGroupOptions"/>.</summary>
    [PublicAPI]
    public sealed class Builder
    {
        private readonly UpdateGroupOptions _options;

        /// <summary>
        /// Створює builder з обов'язковим account'ом. GroupId опційний — null означає CREATE
        /// замість UPDATE (§F14).
        /// </summary>
        /// <param name="account">E.164 номер акаунту-власника.</param>
        public Builder(string account)
        {
            ArgumentException.ThrowIfNullOrEmpty(account);
            _options = new UpdateGroupOptions { Account = account };
        }

        /// <summary>Виставити existing groupId. Без виклику — CREATE-mode (§F14).</summary>
        public Builder WithGroupId(string groupId)
        {
            ArgumentException.ThrowIfNullOrEmpty(groupId);
            _options.GroupId = groupId;
            return this;
        }

        /// <summary>Виставити нову назву групи.</summary>
        public Builder WithName(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            _options.Name = name;
            return this;
        }

        /// <summary>Виставити новий опис.</summary>
        public Builder WithDescription(string description)
        {
            ArgumentNullException.ThrowIfNull(description);
            _options.Description = description;
            return this;
        }

        /// <summary>Виставити шлях до avatar-файлу на ФС signal-cli-процесу.</summary>
        public Builder WithAvatarPath(string avatarPath)
        {
            ArgumentException.ThrowIfNullOrEmpty(avatarPath);
            _options.AvatarPath = avatarPath;
            return this;
        }

        /// <summary>Додати members.</summary>
        public Builder WithMembers(IEnumerable<string> members)
        {
            ArgumentNullException.ThrowIfNull(members);
            _options.Members = [.. members];
            return this;
        }

        /// <summary>Видалити members.</summary>
        public Builder WithRemoveMembers(IEnumerable<string> members)
        {
            ArgumentNullException.ThrowIfNull(members);
            _options.RemoveMembers = [.. members];
            return this;
        }

        /// <summary>Промотити в admin'и.</summary>
        public Builder WithAdmins(IEnumerable<string> admins)
        {
            ArgumentNullException.ThrowIfNull(admins);
            _options.Admins = [.. admins];
            return this;
        }

        /// <summary>Зняти з admin'ів.</summary>
        public Builder WithRemoveAdmins(IEnumerable<string> admins)
        {
            ArgumentNullException.ThrowIfNull(admins);
            _options.RemoveAdmins = [.. admins];
            return this;
        }

        /// <summary>Забанити.</summary>
        public Builder WithBans(IEnumerable<string> bans)
        {
            ArgumentNullException.ThrowIfNull(bans);
            _options.Bans = [.. bans];
            return this;
        }

        /// <summary>Розбанити.</summary>
        public Builder WithUnbans(IEnumerable<string> unbans)
        {
            ArgumentNullException.ThrowIfNull(unbans);
            _options.Unbans = [.. unbans];
            return this;
        }

        /// <summary>Помітити що group-invite-link треба regenerate'нути.</summary>
        public Builder WithResetLink(bool value = true)
        {
            _options.ResetLink = value;
            return this;
        }

        /// <summary>Виставити стан group-invite-link.</summary>
        public Builder WithLinkState(GroupLinkState state)
        {
            _options.LinkState = state;
            return this;
        }

        /// <summary>Permission на add-member.</summary>
        public Builder WithPermissionAddMember(GroupPermission permission)
        {
            _options.PermissionAddMember = permission;
            return this;
        }

        /// <summary>Permission на edit-details.</summary>
        public Builder WithPermissionEditDetails(GroupPermission permission)
        {
            _options.PermissionEditDetails = permission;
            return this;
        }

        /// <summary>Permission на send-messages.</summary>
        public Builder WithPermissionSendMessages(GroupPermission permission)
        {
            _options.PermissionSendMessages = permission;
            return this;
        }

        /// <summary>Disappearing-message timer (seconds). <c>0</c> вимикає; null — leave-unchanged.</summary>
        public Builder WithExpiration(int seconds)
        {
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds), "Expiration не може бути від'ємним.");
            _options.Expiration = seconds;
            return this;
        }

        /// <summary>Будує <see cref="UpdateGroupOptions"/>.</summary>
        /// <exception cref="InvalidOperationException">Якщо Account скинуто після ctor.</exception>
        public UpdateGroupOptions Build()
        {
            if (string.IsNullOrEmpty(_options.Account))
                throw new InvalidOperationException("Account був скинутий після конструювання Builder.");
            return _options;
        }
    }
}
