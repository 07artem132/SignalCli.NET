using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Contacts;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>unblock</c>. Shape identical to <see cref="BlockParameters"/>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 3. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/UnblockCommand.java @ bda4e7fc</c>.
/// Окремий тип (а не reuse <see cref="BlockParameters"/>) для type-safety method dispatch.
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
/// <param name="Recipients">Recipient'и розблокувати.</param>
/// <param name="GroupIds">Group-id'и розблокувати.</param>
[PublicAPI]
public sealed record UnblockParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("recipient")] IEnumerable<string>? Recipients,
    [property: JsonPropertyName("groupId")] IEnumerable<string>? GroupIds);
