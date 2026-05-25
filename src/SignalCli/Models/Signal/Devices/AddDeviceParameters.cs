using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Devices;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>addDevice</c> (primary-перспектива — додати linked device).
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 5 (`device-management`). Pinned до
/// <c>src/main/java/org/asamk/signal/commands/AddDeviceCommand.java @ bda4e7fc</c>.
/// <para>
/// <b>Mental model:</b> URI генерується SECONDARY device'ом (Signal Desktop, secondary signal-cli)
/// під час його <c>startLink</c> flow, потім primary викликає <c>addDevice</c> з цим URI.
/// Зворотній flow до того, що передбачає інтуїція "primary creates invitation".
/// </para>
/// <para>
/// <b>§F11 base64 padding:</b> upstream <c>DeviceLinkUrl.java:48</c> strip'ить <c>=</c> з
/// <c>pub_key</c>-фрагменту. Java <c>Base64.getDecoder()</c> lenient до padding'у, тож signal-cli
/// приймає обидва форми. .NET сервіс просто pass-through URI — НЕ decode'ить pub_key самостійно,
/// тож padding-restoration helper тут не потрібен. Якщо у майбутньому додамо client-side URI
/// validation, скористаємось <c>Base64Helper.RestoreBase64Padding</c> (наразі не існує).
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер акаунту (primary).</param>
/// <param name="Uri"><c>sgnl://linkdevice?uuid=...&amp;pub_key=...</c> URL що відображає secondary.</param>
[PublicAPI]
public sealed record AddDeviceParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("uri")] string Uri);
