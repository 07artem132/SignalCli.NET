using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Groups;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>joinGroup</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 2 (groups-crud). Pinned до
/// <c>src/main/java/org/asamk/signal/commands/JoinGroupCommand.java @ bda4e7fc</c>.
/// </remarks>
/// <param name="Account">E.164 номер акаунту-приєднувача.</param>
/// <param name="Uri">Signal group invitation link (<c>https://signal.group/#...</c> або <c>sgnl://...</c>). Required.</param>
[PublicAPI]
public sealed record JoinGroupParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("uri")] string Uri);
