using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Message;

/// <summary>
/// Опції для відправки <b>reaction</b>-повідомлення (emoji-реакція на існуюче повідомлення)
/// через Signal. Створюється через <see cref="Builder"/>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 1 (`messaging-interactive`). Wire-shape pinned до
/// <c>src/main/java/org/asamk/signal/commands/SendReactionCommand.java</c> @ <c>bda4e7fc</c>.
/// <para>
/// <b>Recipient resolution.</b> Має бути виставлений як мінімум один з:
/// <see cref="Recipients"/>, <see cref="GroupIds"/>, <see cref="Usernames"/>,
/// <see cref="NoteToSelf"/>. Якщо всі чотири порожні — <see cref="Builder.Build"/>
/// кидає <see cref="InvalidOperationException"/>.
/// </para>
/// <para>
/// <b>§F20 quirk:</b> <see cref="NotifySelf"/> — унікальний прапорець саме для <c>sendReaction</c>
/// серед 4-х Wave-1 send-методів. Контролює чи self-копія йде як regular-message vs sync.
/// </para>
/// </remarks>
[PublicAPI]
public sealed record ReactionOptions
{
    /// <summary>Номер акаунту-відправника (E.164).</summary>
    public string Account { get; private set; } = string.Empty;

    /// <summary>Список номерів/UUID отримувачів (опційно — комбінується з <see cref="GroupIds"/>/<see cref="Usernames"/>/<see cref="NoteToSelf"/>).</summary>
    public IReadOnlyList<string>? Recipients { get; private set; }

    /// <summary>Список base64-id груп (опційно).</summary>
    public IReadOnlyList<string>? GroupIds { get; private set; }

    /// <summary>Список Signal-username'ів отримувачів (опційно).</summary>
    public IReadOnlyList<string>? Usernames { get; private set; }

    /// <summary>Чи слати реакцію самому собі (noteToSelf-thread).</summary>
    public bool NoteToSelf { get; private set; }

    /// <summary>
    /// §F20: чи отримує self linked-devices regular-message replica чи лише sync-message.
    /// Унікальне поле саме для sendReaction.
    /// </summary>
    public bool NotifySelf { get; private set; }

    /// <summary>Emoji-реакція (single Unicode grapheme cluster). Обов'язкове.</summary>
    public string Emoji { get; private set; } = string.Empty;

    /// <summary>
    /// Phone-number/UUID автора повідомлення, на яке робиться реакція. Опційно — якщо <c>null</c>
    /// AND у <see cref="Recipients"/> рівно один single-recipient, signal-cli використовує
    /// цього recipient'а як target-author. Quirk pinned до <c>SendReactionCommand.java:82-88 @ bda4e7fc</c>.
    /// </summary>
    public string? TargetAuthor { get; private set; }

    /// <summary>Send-timestamp (epoch ms) повідомлення, на яке робиться реакція. Обов'язкове.</summary>
    public long TargetTimestamp { get; private set; }

    /// <summary>Якщо <c>true</c> — видалити попередню реакцію (matched on author+timestamp).</summary>
    public bool Remove { get; private set; }

    /// <summary>Якщо <c>true</c> — реакція адресована Story-повідомленню, а не звичайному DataMessage.</summary>
    public bool Story { get; private set; }

    /// <summary>Будівельник <see cref="ReactionOptions"/>.</summary>
    [PublicAPI]
    public sealed class Builder
    {
        private readonly ReactionOptions _options;

        /// <summary>
        /// Створює будівельник з трьома обов'язковими параметрами.
        /// Хоча б один recipient-source (Recipients/GroupIds/Usernames/NoteToSelf) має бути доданий
        /// перед <see cref="Build"/>.
        /// </summary>
        /// <param name="account">Номер акаунту-відправника (E.164).</param>
        /// <param name="emoji">Emoji-реакція.</param>
        /// <param name="targetTimestamp">Send-timestamp повідомлення-таргета.</param>
        public Builder(string account, string emoji, long targetTimestamp)
        {
            ArgumentException.ThrowIfNullOrEmpty(account);
            ArgumentException.ThrowIfNullOrEmpty(emoji);
            _options = new ReactionOptions
            {
                Account = account,
                Emoji = emoji,
                TargetTimestamp = targetTimestamp,
            };
        }

        /// <summary>Додати recipient'ів (phone/UUID).</summary>
        public Builder WithRecipients(IEnumerable<string> recipients)
        {
            ArgumentNullException.ThrowIfNull(recipients);
            _options.Recipients = [.. recipients];
            return this;
        }

        /// <summary>Додати group-id'и (base64).</summary>
        public Builder WithGroupIds(IEnumerable<string> groupIds)
        {
            ArgumentNullException.ThrowIfNull(groupIds);
            _options.GroupIds = [.. groupIds];
            return this;
        }

        /// <summary>Додати Signal username'и.</summary>
        public Builder WithUsernames(IEnumerable<string> usernames)
        {
            ArgumentNullException.ThrowIfNull(usernames);
            _options.Usernames = [.. usernames];
            return this;
        }

        /// <summary>Слати реакцію також у noteToSelf-thread.</summary>
        public Builder WithNoteToSelf(bool value = true)
        {
            _options.NoteToSelf = value;
            return this;
        }

        /// <summary>Виставити <see cref="NotifySelf"/> (§F20 — унікальне для sendReaction).</summary>
        public Builder WithNotifySelf(bool value = true)
        {
            _options.NotifySelf = value;
            return this;
        }

        /// <summary>Виставити явного target-author'а (опційно).</summary>
        public Builder WithTargetAuthor(string targetAuthor)
        {
            ArgumentException.ThrowIfNullOrEmpty(targetAuthor);
            _options.TargetAuthor = targetAuthor;
            return this;
        }

        /// <summary>Помітити цю операцію як видалення попередньої реакції.</summary>
        public Builder AsRemove()
        {
            _options.Remove = true;
            return this;
        }

        /// <summary>Помітити що target — Story-повідомлення.</summary>
        public Builder AsStoryReaction()
        {
            _options.Story = true;
            return this;
        }

        /// <summary>
        /// Будує <see cref="ReactionOptions"/>. Перевіряє, що хоча б один recipient-source виставлено.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Виникає, якщо ні Recipients, ні GroupIds, ні Usernames, ні NoteToSelf не виставлено,
        /// або обов'язкові поля (Account/Emoji) очищено через reflection після ctor.
        /// </exception>
        public ReactionOptions Build()
        {
            if (string.IsNullOrEmpty(_options.Account))
                throw new InvalidOperationException("Account був скинутий після конструювання Builder.");
            if (string.IsNullOrEmpty(_options.Emoji))
                throw new InvalidOperationException("Emoji був скинутий після конструювання Builder.");
            var hasRecipient =
                (_options.Recipients?.Count > 0) ||
                (_options.GroupIds?.Count > 0) ||
                (_options.Usernames?.Count > 0) ||
                _options.NoteToSelf;
            if (!hasRecipient)
            {
                throw new InvalidOperationException(
                    "Хоча б один recipient-source (Recipients/GroupIds/Usernames/NoteToSelf) має бути виставлено.");
            }
            return _options;
        }
    }
}
