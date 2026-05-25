using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Devices;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>listDevices</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 5. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/ListDevicesCommand.java @ bda4e7fc</c>.
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
[PublicAPI]
public sealed record ListDevicesParameters(
    [property: JsonPropertyName("account")] string Account);
