using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Devices;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>removeDevice</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 5. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/RemoveDeviceCommand.java @ bda4e7fc</c>.
/// <para>
/// <b>Quirk — <see cref="DeviceId"/> is <c>int</c> not <c>long</c>:</b> asymmetric з
/// <see cref="ListDevicesResponse"/> де <c>Device.Id</c> — <c>long</c>. Upstream argparse
/// <c>type(int.class)</c> на input-side; widening до <c>long</c> відбувається лише при
/// JSON-projection вихідного <c>JsonDevice</c>. Цей DTO mirror'ить input-side <c>int</c>.
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
/// <param name="DeviceId">Signal-protocol device ID (int).</param>
[PublicAPI]
public sealed record RemoveDeviceParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("deviceId")] int DeviceId);
