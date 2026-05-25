using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Message;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>remoteDelete</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 1. Wire-shape pinned до
/// <c>src/main/java/org/asamk/signal/commands/RemoteDeleteCommand.java</c> @ <c>bda4e7fc</c>.
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
/// <param name="TargetTimestamp">Send-timestamp оригіналу (single value, на відміну від sendReceipt).</param>
/// <param name="GroupIds">Base64 group-ids (опційно).</param>
/// <param name="Recipients">Phone/UUID recipients (опційно).</param>
/// <param name="Usernames">Signal username'и (опційно).</param>
/// <param name="NoteToSelf">Слати у noteToSelf-thread.</param>
[PublicAPI]
public sealed record RemoteDeleteParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("target-timestamp")] long TargetTimestamp,
    [property: JsonPropertyName("group-id")] IEnumerable<string>? GroupIds,
    [property: JsonPropertyName("recipient")] IEnumerable<string>? Recipients,
    [property: JsonPropertyName("username")] IEnumerable<string>? Usernames,
    [property: JsonPropertyName("note-to-self")] bool NoteToSelf);
