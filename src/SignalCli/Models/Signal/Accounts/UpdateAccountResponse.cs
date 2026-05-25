using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Відповідь на <c>updateAccount</c>. Поля заповнюються ТІЛЬКИ при successful username-set;
/// при attribute-only updates або delete-username — обидва nullable null.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 6. Pinned до
/// <c>UpdateAccountCommand.java:70-76, 94-97 @ bda4e7fc</c> (private JsonAccountResponse record).
/// </remarks>
/// <param name="Username">Newly assigned username (null коли не set).</param>
/// <param name="UsernameLink">Username link URL (null коли username не set).</param>
[PublicAPI]
public sealed record UpdateAccountResponse(
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("usernameLink")] string? UsernameLink);
