using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Contacts;

/// <summary>
/// Опції для <c>updateContact</c> RPC. Створюється через <see cref="Builder"/>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 3. Wire-shape pinned до
/// <c>src/main/java/org/asamk/signal/commands/UpdateContactCommand.java @ bda4e7fc</c>.
/// <para>
/// <b>Clear-vs-leave-unchanged:</b> усі string-поля nullable; <c>null</c> = "не чіпати",
/// <c>""</c> = "очистити". Цей контракт mirror'ить upstream — там же null/empty distinction.
/// </para>
/// </remarks>
[PublicAPI]
public sealed record UpdateContactOptions
{
    /// <summary>E.164 номер акаунту.</summary>
    public string Account { get; private set; } = string.Empty;

    /// <summary>Recipient identifier (phone/UUID/PNI/username).</summary>
    public string Recipient { get; private set; } = string.Empty;

    /// <summary>Convenience alias: коли GivenName=null AND Name=set, upstream заповнює GivenName з Name + FamilyName="".</summary>
    public string? Name { get; private set; }

    /// <summary>Нове system given name.</summary>
    public string? GivenName { get; private set; }

    /// <summary>Нове system family name.</summary>
    public string? FamilyName { get; private set; }

    /// <summary>Нове nick given name.</summary>
    public string? NickGivenName { get; private set; }

    /// <summary>Нове nick family name.</summary>
    public string? NickFamilyName { get; private set; }

    /// <summary>Note (free-form text).</summary>
    public string? Note { get; private set; }

    /// <summary>Disappearing-message timer у секундах. <c>0</c> вимикає; null — leave-unchanged.</summary>
    public int? Expiration { get; private set; }

    /// <summary>Builder для <see cref="UpdateContactOptions"/>.</summary>
    [PublicAPI]
    public sealed class Builder
    {
        private readonly UpdateContactOptions _options;

        /// <summary>Створює builder з обов'язковими account + recipient.</summary>
        public Builder(string account, string recipient)
        {
            ArgumentException.ThrowIfNullOrEmpty(account);
            ArgumentException.ThrowIfNullOrEmpty(recipient);
            _options = new UpdateContactOptions { Account = account, Recipient = recipient };
        }

        /// <summary>Convenience: виставити name (заповнить GivenName, FamilyName="").</summary>
        public Builder WithName(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            _options.Name = name;
            return this;
        }

        /// <summary>Given name.</summary>
        public Builder WithGivenName(string givenName)
        {
            ArgumentNullException.ThrowIfNull(givenName);
            _options.GivenName = givenName;
            return this;
        }

        /// <summary>Family name.</summary>
        public Builder WithFamilyName(string familyName)
        {
            ArgumentNullException.ThrowIfNull(familyName);
            _options.FamilyName = familyName;
            return this;
        }

        /// <summary>Nick given name.</summary>
        public Builder WithNickGivenName(string nickGivenName)
        {
            ArgumentNullException.ThrowIfNull(nickGivenName);
            _options.NickGivenName = nickGivenName;
            return this;
        }

        /// <summary>Nick family name.</summary>
        public Builder WithNickFamilyName(string nickFamilyName)
        {
            ArgumentNullException.ThrowIfNull(nickFamilyName);
            _options.NickFamilyName = nickFamilyName;
            return this;
        }

        /// <summary>Note.</summary>
        public Builder WithNote(string note)
        {
            ArgumentNullException.ThrowIfNull(note);
            _options.Note = note;
            return this;
        }

        /// <summary>Message-expiration timer (seconds). 0 — вимкнути; пропуск — leave-unchanged.</summary>
        public Builder WithExpiration(int seconds)
        {
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds), "Expiration не може бути від'ємним.");
            _options.Expiration = seconds;
            return this;
        }

        /// <summary>Будує <see cref="UpdateContactOptions"/>.</summary>
        /// <exception cref="InvalidOperationException">Якщо required-поля скинуто після ctor.</exception>
        public UpdateContactOptions Build()
        {
            if (string.IsNullOrEmpty(_options.Account))
                throw new InvalidOperationException("Account був скинутий після конструювання Builder.");
            if (string.IsNullOrEmpty(_options.Recipient))
                throw new InvalidOperationException("Recipient був скинутий після конструювання Builder.");
            return _options;
        }
    }
}
