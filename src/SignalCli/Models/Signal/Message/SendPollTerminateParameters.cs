using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Message;

/// <summary>Wire-DTO для <c>sendPollTerminate</c>.</summary>
/// <remarks>signal-cli-api-coverage Wave 7. Pinned до <c>SendPollTerminateCommand.java @ bda4e7fc</c>.</remarks>
[PublicAPI]
public sealed record SendPollTerminateParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("recipient")] IEnumerable<string>? Recipients,
    [property: JsonPropertyName("groupId")] IEnumerable<string>? GroupIds,
    [property: JsonPropertyName("username")] IEnumerable<string>? Usernames,
    [property: JsonPropertyName("noteToSelf")] bool NoteToSelf,
    [property: JsonPropertyName("notifySelf")] bool NotifySelf,
    [property: JsonPropertyName("pollTimestamp")] long PollTimestamp);
