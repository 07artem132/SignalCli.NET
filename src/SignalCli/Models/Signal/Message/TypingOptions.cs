using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Message;

/// <summary>
/// Опції для відправки <b>typing indicator</b> (START/STOP) через Signal.
/// Створюється через <see cref="Builder"/>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 1 (`messaging-interactive`). Wire-shape pinned до
/// <c>src/main/java/org/asamk/signal/commands/SendTypingCommand.java</c> @ <c>bda4e7fc</c>.
/// <para>
/// <b>Note.</b> START typing-indicator auto-expires через ~15 секунд на стороні receiver'а
/// якщо STOP не отримано — отже explicit STOP опційний для коротких типувань.
/// </para>
/// <para>
/// <b>No username / noteToSelf.</b> Typing-indicators потребують resolved Signal-адресу
/// (recipient або group); username-only та self-targeting не підтримуються upstream'ом.
/// </para>
/// </remarks>
[PublicAPI]
public sealed record TypingOptions
{
    /// <summary>Номер акаунту-відправника (E.164).</summary>
    public string Account { get; private set; } = string.Empty;

    /// <summary>Список номерів/UUID отримувачів (опційно — комбінується з <see cref="GroupIds"/>).</summary>
    public IReadOnlyList<string>? Recipients { get; private set; }

    /// <summary>Список base64-id груп (опційно).</summary>
    public IReadOnlyList<string>? GroupIds { get; private set; }

    /// <summary>Якщо <c>true</c> — STOP-typing; інакше START. Default — START (<c>false</c>).</summary>
    public bool Stop { get; private set; }

    /// <summary>Будівельник <see cref="TypingOptions"/>.</summary>
    [PublicAPI]
    public sealed class Builder
    {
        private readonly TypingOptions _options;

        /// <summary>Створює будівельник з обов'язковим account'ом. Recipients або GroupIds мають бути додані.</summary>
        /// <param name="account">E.164 номер акаунту-відправника.</param>
        public Builder(string account)
        {
            ArgumentException.ThrowIfNullOrEmpty(account);
            _options = new TypingOptions { Account = account };
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

        /// <summary>Слати STOP-typing замість START.</summary>
        public Builder AsStop()
        {
            _options.Stop = true;
            return this;
        }

        /// <summary>Будує <see cref="TypingOptions"/>.</summary>
        /// <exception cref="InvalidOperationException">Якщо ні Recipients, ні GroupIds не виставлено, або Account скинуто.</exception>
        public TypingOptions Build()
        {
            if (string.IsNullOrEmpty(_options.Account))
                throw new InvalidOperationException("Account був скинутий після конструювання Builder.");
            var hasRecipient = (_options.Recipients?.Count > 0) || (_options.GroupIds?.Count > 0);
            if (!hasRecipient)
            {
                throw new InvalidOperationException(
                    "Хоча б один recipient-source (Recipients або GroupIds) має бути виставлено.");
            }
            return _options;
        }
    }
}
