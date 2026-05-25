using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Devices;

/// <summary>
/// Один linked device у відповіді <c>listDevices</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 5 (`device-management`). Pinned до
/// <c>ListDevicesCommand.java:67 @ bda4e7fc</c> (private <c>JsonDevice</c> record).
/// <para>
/// <b>§F6 quirk — 4 fields only:</b> upstream <c>Device</c> record у
/// <c>lib/.../manager/api/Device.java</c> має 5 полів (<c>id, name, created, lastSeen,
/// isThisDevice</c>), але <c>JsonDevice</c> projection drop'ає <c>isThisDevice</c>
/// (consumed лише <c>PlainTextWriter</c>'ом для " (this device)" annotation —
/// <c>ListDevicesCommand.java:50</c>). НЕ додаємо <c>IsThisDevice</c> у .NET — це б lie'ло про
/// wire shape. Consumer'и можуть self-determine через <c>Id == 1</c> (primary).
/// </para>
/// <para>
/// <b>§Wire-name rename:</b> upstream <c>Device.created</c>/<c>lastSeen</c> renamed у
/// <c>createdTimestamp</c>/<c>lastSeenTimestamp</c> на wire (Jackson default field-name).
/// </para>
/// <para>
/// <b>§int → long widening:</b> upstream <c>Device.id()</c> — <c>int</c>, але <c>JsonDevice.id</c>
/// — <c>long</c> (implicit widening на projection-site). На .NET — <c>long</c> щоб mirror wire.
/// </para>
/// </remarks>
/// <param name="Id">Signal-protocol device ID. Primary завжди <c>1</c>.</param>
/// <param name="Name">Device's self-reported name. Може бути <c>null</c>.</param>
/// <param name="CreatedTimestamp">Unix-ms коли device був linked.</param>
/// <param name="LastSeenTimestamp">Unix-ms останньої серверної активності (<c>0</c> якщо ніколи не connect'ився).</param>
[PublicAPI]
public sealed record Device(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("createdTimestamp")] long CreatedTimestamp,
    [property: JsonPropertyName("lastSeenTimestamp")] long LastSeenTimestamp);
