using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Resources;

/// <summary>
/// Опції для <c>getAvatar</c>. Точно ОДНЕ з <see cref="Contact"/>/<see cref="Profile"/>/<see cref="GroupId"/>
/// має бути виставлено. Створюється через <see cref="Builder"/> (§F19 3-way XOR enforced).
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 4. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/GetAvatarCommand.java @ bda4e7fc</c>.
/// <para>
/// <b>§F19 3-way XOR:</b> upstream argparse mutex group <c>.required(true)</c> enforce'ить
/// це на argparse-рівні; у JSON-RPC шарі mutex НЕ enforce'иться — upstream silently
/// first-wins. Builder валідує XOR client-side щоб уникнути ambiguous wire payload.
/// </para>
/// <para>
/// <b>Contact vs Profile semantic:</b> <see cref="Contact"/> читає avatar з ЛОКАЛЬНОЇ
/// контакт-карти (синхронізовано з address book linked-device); <see cref="Profile"/>
/// читає public profile avatar (Signal-server-stored). Document'уй цю різницю для
/// consumer'ів — вони НЕ еквівалентні.
/// </para>
/// </remarks>
[PublicAPI]
public sealed record GetAvatarOptions
{
    /// <summary>E.164 номер акаунту.</summary>
    public string Account { get; private set; } = string.Empty;

    /// <summary>Phone/UUID recipient'а — avatar з local contact-карти. Mutex з <see cref="Profile"/>/<see cref="GroupId"/>.</summary>
    public string? Contact { get; private set; }

    /// <summary>Phone/UUID recipient'а — public profile avatar. Mutex з <see cref="Contact"/>/<see cref="GroupId"/>.</summary>
    public string? Profile { get; private set; }

    /// <summary>Base64 group-id — group avatar. Mutex з <see cref="Contact"/>/<see cref="Profile"/>.</summary>
    public string? GroupId { get; private set; }

    /// <summary>Builder для <see cref="GetAvatarOptions"/> з §F19 3-way XOR enforcement.</summary>
    [PublicAPI]
    public sealed class Builder
    {
        private readonly GetAvatarOptions _options;
        private int _sourceCount;

        /// <summary>Створює builder з обов'язковим account'ом. Одне з 3 джерел треба додати перед Build.</summary>
        public Builder(string account)
        {
            ArgumentException.ThrowIfNullOrEmpty(account);
            _options = new GetAvatarOptions { Account = account };
        }

        /// <summary>Вибрати джерело avatar'а — local contact-карту recipient'а.</summary>
        public Builder WithContact(string recipient)
        {
            ArgumentException.ThrowIfNullOrEmpty(recipient);
            EnsureNoSourceYet();
            _options.Contact = recipient;
            return this;
        }

        /// <summary>Вибрати джерело — public profile avatar recipient'а.</summary>
        public Builder WithProfile(string recipient)
        {
            ArgumentException.ThrowIfNullOrEmpty(recipient);
            EnsureNoSourceYet();
            _options.Profile = recipient;
            return this;
        }

        /// <summary>Вибрати джерело — group avatar.</summary>
        public Builder WithGroupId(string groupId)
        {
            ArgumentException.ThrowIfNullOrEmpty(groupId);
            EnsureNoSourceYet();
            _options.GroupId = groupId;
            return this;
        }

        private void EnsureNoSourceYet()
        {
            if (_sourceCount > 0)
                throw new InvalidOperationException("§F19: точно одне з Contact/Profile/GroupId — взаємовиключні.");
            _sourceCount++;
        }

        /// <summary>Будує <see cref="GetAvatarOptions"/>.</summary>
        /// <exception cref="InvalidOperationException">Якщо жодного джерела не виставлено.</exception>
        public GetAvatarOptions Build()
        {
            if (string.IsNullOrEmpty(_options.Account))
                throw new InvalidOperationException("Account був скинутий після конструювання Builder.");
            if (_sourceCount == 0)
                throw new InvalidOperationException(
                    "§F19: треба виставити точно одне з Contact/Profile/GroupId перед Build.");
            return _options;
        }
    }
}
