using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Contacts;

/// <summary>
/// Опції для <c>updateProfile</c> RPC. Створюється через <see cref="Builder"/>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 3. Wire-shape pinned до
/// <c>src/main/java/org/asamk/signal/commands/UpdateProfileCommand.java @ bda4e7fc</c>.
/// <para>
/// <b>§F18 Avatar XOR:</b> <see cref="AvatarPath"/> та <see cref="RemoveAvatar"/> mutex.
/// Builder enforce'ить XOR client-side щоб уникнути undefined upstream-behavior
/// (argparse mutex group там argparse-only). Обидва unset = avatar untouched.
/// </para>
/// <para>
/// <b>Daemon-side filesystem:</b> <see cref="AvatarPath"/> читається з ФС signal-cli-процесу,
/// не з клієнтського процесу. При daemon-deployment'і шлях має бути доступним
/// сервер-side; для bytes-over-RPC consumer'ам треба попередньо записати у local temp file.
/// </para>
/// </remarks>
[PublicAPI]
public sealed record UpdateProfileOptions
{
    /// <summary>E.164 номер акаунту.</summary>
    public string Account { get; private set; } = string.Empty;

    /// <summary>Profile given name. <c>""</c> = clear; <c>null</c> = leave-unchanged.</summary>
    public string? GivenName { get; private set; }

    /// <summary>Profile family name.</summary>
    public string? FamilyName { get; private set; }

    /// <summary>About-текст.</summary>
    public string? About { get; private set; }

    /// <summary>About emoji (single emoji string).</summary>
    public string? AboutEmoji { get; private set; }

    /// <summary>Base64-encoded MobileCoin public-address bytes.</summary>
    public string? MobileCoinAddress { get; private set; }

    /// <summary>Шлях до avatar-файлу на daemon-side ФС. Mutex з <see cref="RemoveAvatar"/>.</summary>
    public string? AvatarPath { get; private set; }

    /// <summary>Якщо <c>true</c>, видалити поточний avatar. Mutex з <see cref="AvatarPath"/>.</summary>
    public bool RemoveAvatar { get; private set; }

    /// <summary>Builder для <see cref="UpdateProfileOptions"/>.</summary>
    [PublicAPI]
    public sealed class Builder
    {
        private readonly UpdateProfileOptions _options;

        /// <summary>Створює builder з обов'язковим account'ом.</summary>
        public Builder(string account)
        {
            ArgumentException.ThrowIfNullOrEmpty(account);
            _options = new UpdateProfileOptions { Account = account };
        }

        /// <summary>Profile given name.</summary>
        public Builder WithGivenName(string givenName)
        {
            ArgumentNullException.ThrowIfNull(givenName);
            _options.GivenName = givenName;
            return this;
        }

        /// <summary>Profile family name.</summary>
        public Builder WithFamilyName(string familyName)
        {
            ArgumentNullException.ThrowIfNull(familyName);
            _options.FamilyName = familyName;
            return this;
        }

        /// <summary>About text.</summary>
        public Builder WithAbout(string about)
        {
            ArgumentNullException.ThrowIfNull(about);
            _options.About = about;
            return this;
        }

        /// <summary>About emoji.</summary>
        public Builder WithAboutEmoji(string aboutEmoji)
        {
            ArgumentNullException.ThrowIfNull(aboutEmoji);
            _options.AboutEmoji = aboutEmoji;
            return this;
        }

        /// <summary>MobileCoin public-address (base64). Consumer повинен сам валідувати base64.</summary>
        public Builder WithMobileCoinAddress(string mobileCoinAddressBase64)
        {
            ArgumentException.ThrowIfNullOrEmpty(mobileCoinAddressBase64);
            _options.MobileCoinAddress = mobileCoinAddressBase64;
            return this;
        }

        /// <summary>Avatar file path (daemon-side filesystem).</summary>
        public Builder WithAvatarPath(string avatarPath)
        {
            ArgumentException.ThrowIfNullOrEmpty(avatarPath);
            if (_options.RemoveAvatar)
                throw new InvalidOperationException("§F18: WithAvatarPath та WithRemoveAvatar — взаємовиключні.");
            _options.AvatarPath = avatarPath;
            return this;
        }

        /// <summary>Видалити поточний avatar.</summary>
        public Builder WithRemoveAvatar()
        {
            if (_options.AvatarPath is not null)
                throw new InvalidOperationException("§F18: WithAvatarPath та WithRemoveAvatar — взаємовиключні.");
            _options.RemoveAvatar = true;
            return this;
        }

        /// <summary>Будує <see cref="UpdateProfileOptions"/>.</summary>
        public UpdateProfileOptions Build()
        {
            if (string.IsNullOrEmpty(_options.Account))
                throw new InvalidOperationException("Account був скинутий після конструювання Builder.");
            return _options;
        }
    }
}
