using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Contacts;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>listContacts</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 3. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/ListContactsCommand.java @ bda4e7fc</c>.
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
/// <param name="Recipients">Опціональний фільтр — лише ці recipient'и.</param>
/// <param name="AllRecipients">Якщо <c>true</c>, повертає ВСІ відомі recipient'и (а не лише contacts).</param>
/// <param name="Blocked">Фільтр: <c>null</c>=no-filter, <c>true</c>=blocked only, <c>false</c>=unblocked only.</param>
/// <param name="Name">Substring-фільтр по contact-name або profile-name.</param>
/// <param name="Internal">Якщо <c>true</c>, додає <c>internal</c> sub-object у кожен результат.</param>
[PublicAPI]
public sealed record ListContactsParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("recipient")] IEnumerable<string>? Recipients,
    [property: JsonPropertyName("allRecipients")] bool AllRecipients,
    [property: JsonPropertyName("blocked")] bool? Blocked,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("internal")] bool Internal);
