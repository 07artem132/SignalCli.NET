using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>getUserStatus</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 8. Pinned до
/// <c>GetUserStatusCommand.java @ bda4e7fc</c>.
/// <para>
/// <b>§F5 AND/OR merge:</b> обидва списки опційні, не mutually exclusive. Empty + empty → <c>[]</c>.
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
/// <param name="Recipients">Phone numbers для lookup'у.</param>
/// <param name="Usernames">Usernames (or username-links) для lookup'у.</param>
[PublicAPI]
public sealed record GetUserStatusParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("recipient")] IEnumerable<string>? Recipients,
    [property: JsonPropertyName("username")] IEnumerable<string>? Usernames);
