using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Contacts;

/// <summary>
/// Один контакт у відповіді <c>listContacts</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 3 (`contacts-identity`). Wire-shape pinned до
/// <c>src/main/java/org/asamk/signal/json/JsonContact.java @ bda4e7fc</c>.
/// <para>
/// Field-naming quirks (preserved as-is з upstream'у):
/// <list type="bullet">
/// <item><c>nickGivenName</c> / <c>nickFamilyName</c> — wire дропає другий "Name" з Java-полів
/// <c>nickNameGivenName</c>/<c>nickNameFamilyName</c>. .NET property повторює wire-name.</item>
/// <item><c>discoverableByPhonenumber</c> — одне слово, lowercase "n" (upstream typo, preserved).</item>
/// <item><see cref="Name"/> — composed display name (givenName+familyName), не окреме збережене поле.</item>
/// <item><see cref="Internal"/> — present ТІЛЬКИ коли request <c>internal=true</c> (Jackson <c>@JsonInclude(NON_NULL)</c>).</item>
/// </list>
/// </para>
/// </remarks>
[PublicAPI]
public sealed record JsonContact(
    [property: JsonPropertyName("number")] string? Number,
    [property: JsonPropertyName("uuid")] string? Uuid,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("givenName")] string? GivenName,
    [property: JsonPropertyName("familyName")] string? FamilyName,
    [property: JsonPropertyName("nickName")] string? NickName,
    [property: JsonPropertyName("nickGivenName")] string? NickGivenName,
    [property: JsonPropertyName("nickFamilyName")] string? NickFamilyName,
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("color")] string? Color,
    [property: JsonPropertyName("isArchived")] bool IsArchived,
    [property: JsonPropertyName("isBlocked")] bool IsBlocked,
    [property: JsonPropertyName("isHidden")] bool IsHidden,
    [property: JsonPropertyName("messageExpirationTime")] int MessageExpirationTime,
    [property: JsonPropertyName("profileSharing")] bool ProfileSharing,
    [property: JsonPropertyName("unregistered")] bool Unregistered,
    [property: JsonPropertyName("profile")] JsonContactProfile? Profile,
    [property: JsonPropertyName("internal")] JsonContactInternal? Internal);

/// <summary>
/// Profile-секція у контакті (server-side profile data).
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 3. Pinned до <c>JsonContact.java:31-40 @ bda4e7fc</c>.
/// Перейменовано з upstream <c>JsonProfile</c> у <c>JsonContactProfile</c> щоб уникнути
/// конфлікту з потенційним top-level profile DTO у майбутніх wave'ах.
/// </remarks>
[PublicAPI]
public sealed record JsonContactProfile(
    [property: JsonPropertyName("lastUpdateTimestamp")] long LastUpdateTimestamp,
    [property: JsonPropertyName("givenName")] string? GivenName,
    [property: JsonPropertyName("familyName")] string? FamilyName,
    [property: JsonPropertyName("about")] string? About,
    [property: JsonPropertyName("aboutEmoji")] string? AboutEmoji,
    [property: JsonPropertyName("hasAvatar")] bool HasAvatar,
    [property: JsonPropertyName("mobileCoinAddress")] string? MobileCoinAddress);

/// <summary>
/// "Internal"-секція у контакті (capabilities + access mode + sharing flags).
/// Present лише коли request <c>internal=true</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 3. Pinned до <c>JsonContact.java:42-48 @ bda4e7fc</c>.
/// Field <c>discoverableByPhonenumber</c> — single word, lowercase "n" — upstream-typo preserved verbatim.
/// <para>
/// <c>capabilities</c> — Java enum identifiers у lowercase/mixedCase
/// (<c>"storage"</c>, <c>"storageServiceEncryptionV2Capability"</c>), а НЕ UPPER_SNAKE
/// (різниться з рештою Java-enum-wire-naming). Тому модельовано як <c>string[]</c>.
/// </para>
/// <para>
/// <c>unidentifiedAccessMode</c> — UPPER_SNAKE: <c>DISABLED</c>/<c>ENABLED</c>/<c>UNRESTRICTED</c>;
/// <c>UNKNOWN</c> мапиться upstream'ом у <c>null</c>. Модельовано як <c>string?</c>.
/// </para>
/// </remarks>
[PublicAPI]
public sealed record JsonContactInternal(
    [property: JsonPropertyName("capabilities")] IReadOnlyList<string>? Capabilities,
    [property: JsonPropertyName("unidentifiedAccessMode")] string? UnidentifiedAccessMode,
    [property: JsonPropertyName("sharesPhoneNumber")] bool? SharesPhoneNumber,
    [property: JsonPropertyName("discoverableByPhonenumber")] bool? DiscoverableByPhonenumber);
