using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Contacts;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>updateContact</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 3. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/UpdateContactCommand.java @ bda4e7fc</c>.
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
/// <param name="Recipient">Recipient identifier.</param>
/// <param name="Name">Convenience alias (див. UpdateContactOptions XMLDoc).</param>
/// <param name="GivenName">System given name.</param>
/// <param name="FamilyName">System family name.</param>
/// <param name="NickGivenName">Nick given name.</param>
/// <param name="NickFamilyName">Nick family name.</param>
/// <param name="Note">Free-form note.</param>
/// <param name="Expiration">Message-expiration timer (seconds).</param>
[PublicAPI]
public sealed record UpdateContactParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("recipient")] string Recipient,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("givenName")] string? GivenName,
    [property: JsonPropertyName("familyName")] string? FamilyName,
    [property: JsonPropertyName("nickGivenName")] string? NickGivenName,
    [property: JsonPropertyName("nickFamilyName")] string? NickFamilyName,
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("expiration")] int? Expiration);
