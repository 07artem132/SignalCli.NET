using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Message;

/// <summary>
/// Опції для відправки <b>remote-delete</b>-запиту (попросити одержувачів видалити повідомлення)
/// через Signal. Створюється через <see cref="Builder"/>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 1 (`messaging-interactive`). Wire-shape pinned до
/// <c>src/main/java/org/asamk/signal/commands/RemoteDeleteCommand.java</c> @ <c>bda4e7fc</c>.
/// <para>
/// <b>Best-effort.</b> Receivers' Signal clients можуть відмовитися виконати delete (наприклад,
/// поза часовим вікном, яке протокол дозволяє); upstream signal-cli НЕ перевіряє це client-side.
/// Local-store: НЕ видаляє локальну копію повідомлення у signal-cli'й DB — consumers повинні
/// трекати sent-messages самостійно якщо потрібно.
/// </para>
/// <para>
/// <b>§F10:</b> <c>IOException</c> у цьому методі мапиться на код <c>-32603 INTERNAL_ERROR</c>
/// (через <c>UnexpectedErrorException</c>), не на <c>-3 IoError</c>. Inconsistent із sendTyping
/// (<c>-1</c>); відповідає sendReaction'у. Це upstream-choice, не нормалізуємо.
/// </para>
/// </remarks>
[PublicAPI]
public sealed record RemoteDeleteOptions
{
    /// <summary>Номер акаунту-відправника (E.164).</summary>
    public string Account { get; private set; } = string.Empty;

    /// <summary>Send-timestamp повідомлення, яке потрібно видалити.</summary>
    public long TargetTimestamp { get; private set; }

    /// <summary>Список номерів/UUID отримувачів (опційно — комбінується з GroupIds/Usernames/NoteToSelf).</summary>
    public IReadOnlyList<string>? Recipients { get; private set; }

    /// <summary>Список base64-id груп (опційно).</summary>
    public IReadOnlyList<string>? GroupIds { get; private set; }

    /// <summary>Список Signal username'ів (опційно).</summary>
    public IReadOnlyList<string>? Usernames { get; private set; }

    /// <summary>Чи слати delete у noteToSelf-thread.</summary>
    public bool NoteToSelf { get; private set; }

    /// <summary>Будівельник <see cref="RemoteDeleteOptions"/>.</summary>
    [PublicAPI]
    public sealed class Builder
    {
        private readonly RemoteDeleteOptions _options;

        /// <summary>Створює будівельник з обов'язковими account + targetTimestamp.</summary>
        /// <param name="account">E.164 номер акаунту-відправника.</param>
        /// <param name="targetTimestamp">Send-timestamp оригіналу.</param>
        public Builder(string account, long targetTimestamp)
        {
            ArgumentException.ThrowIfNullOrEmpty(account);
            _options = new RemoteDeleteOptions
            {
                Account = account,
                TargetTimestamp = targetTimestamp,
            };
        }

        /// <summary>Додати recipient'ів.</summary>
        public Builder WithRecipients(IEnumerable<string> recipients)
        {
            ArgumentNullException.ThrowIfNull(recipients);
            _options.Recipients = [.. recipients];
            return this;
        }

        /// <summary>Додати group-id'и.</summary>
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

        /// <summary>Слати delete також у noteToSelf-thread.</summary>
        public Builder WithNoteToSelf(bool value = true)
        {
            _options.NoteToSelf = value;
            return this;
        }

        /// <summary>Будує <see cref="RemoteDeleteOptions"/>. Перевіряє, що хоча б один recipient-source виставлено.</summary>
        /// <exception cref="InvalidOperationException">Якщо немає жодного recipient-source або Account скинуто.</exception>
        public RemoteDeleteOptions Build()
        {
            if (string.IsNullOrEmpty(_options.Account))
                throw new InvalidOperationException("Account був скинутий після конструювання Builder.");
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
