using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Message;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>sendTyping</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 1. Wire-shape pinned до
/// <c>src/main/java/org/asamk/signal/commands/SendTypingCommand.java</c> @ <c>bda4e7fc</c>.
/// <para>
/// <b>Boolean-as-action.</b> Wire не має <c>"action": "START"|"STOP"</c> поля — натомість boolean
/// <see cref="Stop"/>: <c>false</c>=START, <c>true</c>=STOP. Pinned до
/// <c>SendTypingCommand.java:36, 47 @ bda4e7fc</c>.
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
/// <param name="Recipients">Phone/UUID recipients (опційно).</param>
/// <param name="GroupIds">Base64 group-ids (опційно).</param>
/// <param name="Stop">START (<c>false</c>) або STOP (<c>true</c>).</param>
[PublicAPI]
public sealed record SendTypingParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("recipient")] IEnumerable<string>? Recipients,
    [property: JsonPropertyName("group-id")] IEnumerable<string>? GroupIds,
    [property: JsonPropertyName("stop")] bool Stop);
