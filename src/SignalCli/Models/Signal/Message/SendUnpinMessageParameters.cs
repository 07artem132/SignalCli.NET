using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Message;

/// <summary>Wire-DTO для <c>sendUnpinMessage</c>.</summary>
[PublicAPI]
public sealed record SendUnpinMessageParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("recipient")] IEnumerable<string>? Recipients,
    [property: JsonPropertyName("groupId")] IEnumerable<string>? GroupIds,
    [property: JsonPropertyName("username")] IEnumerable<string>? Usernames,
    [property: JsonPropertyName("noteToSelf")] bool NoteToSelf,
    [property: JsonPropertyName("notifySelf")] bool NotifySelf,
    [property: JsonPropertyName("targetAuthor")] string TargetAuthor,
    [property: JsonPropertyName("targetTimestamp")] long TargetTimestamp,
    [property: JsonPropertyName("story")] bool Story);
