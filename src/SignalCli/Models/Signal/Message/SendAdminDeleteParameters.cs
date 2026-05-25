using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Message;

/// <summary>Wire-DTO для <c>sendAdminDelete</c>.</summary>
/// <remarks>signal-cli-api-coverage Wave 7. Pinned до <c>SendAdminDeleteCommand.java @ bda4e7fc</c>.</remarks>
[PublicAPI]
public sealed record SendAdminDeleteParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("groupId")] IEnumerable<string> GroupIds,
    [property: JsonPropertyName("notifySelf")] bool NotifySelf,
    [property: JsonPropertyName("targetAuthor")] string TargetAuthor,
    [property: JsonPropertyName("targetTimestamp")] long TargetTimestamp,
    [property: JsonPropertyName("story")] bool Story);
