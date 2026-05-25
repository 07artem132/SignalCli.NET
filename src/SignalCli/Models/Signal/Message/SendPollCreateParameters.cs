using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Message;

/// <summary>Wire-DTO для <c>sendPollCreate</c>. §F21: wire <c>noMulti</c> = inverted <c>AllowMultipleVotes</c>.</summary>
/// <remarks>signal-cli-api-coverage Wave 7. Pinned до <c>SendPollCreateCommand.java @ bda4e7fc</c>.</remarks>
[PublicAPI]
public sealed record SendPollCreateParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("recipient")] IEnumerable<string>? Recipients,
    [property: JsonPropertyName("groupId")] IEnumerable<string>? GroupIds,
    [property: JsonPropertyName("username")] IEnumerable<string>? Usernames,
    [property: JsonPropertyName("noteToSelf")] bool NoteToSelf,
    [property: JsonPropertyName("notifySelf")] bool NotifySelf,
    [property: JsonPropertyName("question")] string Question,
    [property: JsonPropertyName("noMulti")] bool NoMulti,
    [property: JsonPropertyName("option")] IEnumerable<string> Options);
