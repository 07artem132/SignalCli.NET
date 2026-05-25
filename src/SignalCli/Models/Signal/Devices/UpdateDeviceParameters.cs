using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Devices;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>updateDevice</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 5. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/UpdateDeviceCommand.java @ bda4e7fc</c>.
/// <para>
/// <b>§F12 deviceName encrypted server-side:</b> upstream шифрує <c>DeviceName</c> ідентичним
/// ключем device'а перед transmission. Bytes — opaque ciphertext, тож логування <see cref="DeviceName"/>
/// вище <c>Trace</c> level — privacy violation (CLAUDE.md rule #1).
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
/// <param name="DeviceId">Signal-protocol device ID (int).</param>
/// <param name="DeviceName">Нова назва device'а. ENCRYPTED server-side — НЕ логуй вище Trace.</param>
[PublicAPI]
public sealed record UpdateDeviceParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("deviceId")] int DeviceId,
    [property: JsonPropertyName("deviceName")] string DeviceName);
