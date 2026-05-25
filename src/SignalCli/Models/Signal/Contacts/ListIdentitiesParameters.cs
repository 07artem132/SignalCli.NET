using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Contacts;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>listIdentities</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 3. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/ListIdentitiesCommand.java @ bda4e7fc</c>.
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
/// <param name="Number">Опціональний фільтр — лише identities цього recipient'а. <c>null</c> = усі.</param>
[PublicAPI]
public sealed record ListIdentitiesParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("number")] string? Number);
