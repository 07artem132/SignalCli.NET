using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Contacts;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>updateProfile</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 3. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/UpdateProfileCommand.java @ bda4e7fc</c>.
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
/// <param name="GivenName">Profile given name.</param>
/// <param name="FamilyName">Profile family name.</param>
/// <param name="About">About text.</param>
/// <param name="AboutEmoji">About emoji.</param>
/// <param name="MobileCoinAddress">MobileCoin base64-encoded public-address bytes.</param>
/// <param name="Avatar">Daemon-side filesystem path до avatar-файлу.</param>
/// <param name="RemoveAvatar">Якщо <c>true</c>, видалити avatar.</param>
[PublicAPI]
public sealed record UpdateProfileParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("givenName")] string? GivenName,
    [property: JsonPropertyName("familyName")] string? FamilyName,
    [property: JsonPropertyName("about")] string? About,
    [property: JsonPropertyName("aboutEmoji")] string? AboutEmoji,
    [property: JsonPropertyName("mobileCoinAddress")] string? MobileCoinAddress,
    [property: JsonPropertyName("avatar")] string? Avatar,
    [property: JsonPropertyName("removeAvatar")] bool RemoveAvatar);
