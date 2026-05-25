using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Опції для <c>updateAccount</c> RPC (DESTRUCTIVE; gated by <c>EnableDestructiveOperations</c>).
/// Створюється через <see cref="Builder"/>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 6 (`account-lifecycle`). Pinned до
/// <c>src/main/java/org/asamk/signal/commands/UpdateAccountCommand.java @ bda4e7fc</c>.
/// <para>
/// <b>§F3 NumberSharing:</b> на wire — <c>bool?</c> (Java <c>type(Boolean.class)</c>), НЕ enum.
/// Internal <c>PhoneNumberSharingMode</c> існує у Manager API але НЕ exposed через JSON-RPC —
/// command використовує plain Boolean flag. .NET property теж <c>bool?</c>.
/// </para>
/// <para>
/// <b>Username vs DeleteUsername XOR:</b> argparse enforce'ить mutex на CLI-рівні, але
/// JSON-RPC шар приймає обидва — upstream silently виконає set-then-delete (account залишається
/// без username). Builder валідує XOR client-side.
/// </para>
/// </remarks>
[PublicAPI]
public sealed record UpdateAccountOptions
{
    /// <summary>E.164 номер акаунту.</summary>
    public string Account { get; private set; } = string.Empty;

    /// <summary>Нова назва device у linked-devices list. <c>null</c> = не міняти.</summary>
    public string? DeviceName { get; private set; }

    /// <summary>Allow anyone send unidentified-sender messages. <c>null</c> = не міняти.</summary>
    public bool? UnrestrictedUnidentifiedSender { get; private set; }

    /// <summary>Чи акаунт discoverable за phone-number. <c>null</c> = не міняти.</summary>
    public bool? DiscoverableByNumber { get; private set; }

    /// <summary>§F3: Boolean (НЕ enum). Чи Signal sharing phone number при send. <c>null</c> = не міняти.</summary>
    public bool? NumberSharing { get; private set; }

    /// <summary>Новий Signal username. Mutex з <see cref="DeleteUsername"/>.</summary>
    public string? Username { get; private set; }

    /// <summary>Якщо <c>true</c> — видалити існуючий username. Mutex з <see cref="Username"/>.</summary>
    public bool DeleteUsername { get; private set; }

    /// <summary>Builder з §XOR enforcement.</summary>
    [PublicAPI]
    public sealed class Builder
    {
        private readonly UpdateAccountOptions _options;

        /// <summary>Створює builder з обов'язковим account'ом.</summary>
        public Builder(string account)
        {
            ArgumentException.ThrowIfNullOrEmpty(account);
            _options = new UpdateAccountOptions { Account = account };
        }

        /// <summary>Виставити нову назву device.</summary>
        public Builder WithDeviceName(string deviceName)
        {
            ArgumentException.ThrowIfNullOrEmpty(deviceName);
            _options.DeviceName = deviceName;
            return this;
        }

        /// <summary>Toggle unrestricted-unidentified-sender.</summary>
        public Builder WithUnrestrictedUnidentifiedSender(bool value)
        {
            _options.UnrestrictedUnidentifiedSender = value;
            return this;
        }

        /// <summary>Toggle discoverable-by-number.</summary>
        public Builder WithDiscoverableByNumber(bool value)
        {
            _options.DiscoverableByNumber = value;
            return this;
        }

        /// <summary>§F3 Toggle number-sharing (bool, NOT enum).</summary>
        public Builder WithNumberSharing(bool value)
        {
            _options.NumberSharing = value;
            return this;
        }

        /// <summary>Виставити новий username. Mutex з <see cref="WithDeleteUsername"/>.</summary>
        public Builder WithUsername(string username)
        {
            ArgumentException.ThrowIfNullOrEmpty(username);
            if (_options.DeleteUsername)
                throw new InvalidOperationException("XOR: Username та DeleteUsername — взаємовиключні.");
            _options.Username = username;
            return this;
        }

        /// <summary>Помітити для видалення username. Mutex з <see cref="WithUsername"/>.</summary>
        public Builder WithDeleteUsername()
        {
            if (_options.Username is not null)
                throw new InvalidOperationException("XOR: Username та DeleteUsername — взаємовиключні.");
            _options.DeleteUsername = true;
            return this;
        }

        /// <summary>Будує <see cref="UpdateAccountOptions"/>.</summary>
        public UpdateAccountOptions Build()
        {
            if (string.IsNullOrEmpty(_options.Account))
                throw new InvalidOperationException("Account був скинутий після конструювання Builder.");
            return _options;
        }
    }
}
