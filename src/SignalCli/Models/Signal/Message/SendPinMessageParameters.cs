using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Message;

/// <summary>Wire-DTO для <c>sendPinMessage</c>. §F23 <c>pinDuration=-1</c> = forever.</summary>
[PublicAPI]
public sealed record SendPinMessageParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("recipient")] IEnumerable<string>? Recipients,
    [property: JsonPropertyName("groupId")] IEnumerable<string>? GroupIds,
    [property: JsonPropertyName("username")] IEnumerable<string>? Usernames,
    [property: JsonPropertyName("noteToSelf")] bool NoteToSelf,
    [property: JsonPropertyName("notifySelf")] bool NotifySelf,
    [property: JsonPropertyName("pinDuration")] int PinDuration,
    [property: JsonPropertyName("targetAuthor")] string TargetAuthor,
    [property: JsonPropertyName("targetTimestamp")] long TargetTimestamp,
    [property: JsonPropertyName("story")] bool Story);
