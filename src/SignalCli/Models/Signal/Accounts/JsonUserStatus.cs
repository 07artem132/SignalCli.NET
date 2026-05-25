using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Один user-status запис у відповіді <c>getUserStatus</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 8 (utility-rpc). Pinned до
/// <c>src/main/java/org/asamk/signal/commands/GetUserStatusCommand.java:119-125 @ bda4e7fc</c>
/// (private <c>JsonUserStatus</c> record).
/// <para>
/// <b>Discriminator:</b> кожен row походить АБО з <c>recipient</c>-input (then <see cref="Number"/>
/// заповнено), АБО з <c>username</c>-input (then <see cref="Username"/> заповнено). Поля mutex
/// per row, але обидва inputs можуть бути merged у одній response array (§F5).
/// </para>
/// <para>
/// <b>Quirk:</b> <see cref="Uuid"/> appears as JSON <c>null</c> when not registered (NOT omitted);
/// <see cref="Number"/>/<see cref="Username"/> omitted from JSON when null (Jackson
/// <c>@JsonInclude(NON_NULL)</c>).
/// </para>
/// </remarks>
/// <param name="Recipient">Raw input string (phone or username/link).</param>
/// <param name="Number">Canonicalised E.164 (тільки для recipient-input rows + successful parse).</param>
/// <param name="Username">Username (тільки для username-input rows).</param>
/// <param name="Uuid">Signal account UUID — <c>null</c> якщо не registered.</param>
/// <param name="IsRegistered">Derived: <c>Uuid != null</c>. Always present.</param>
[PublicAPI]
public sealed record JsonUserStatus(
    [property: JsonPropertyName("recipient")] string Recipient,
    [property: JsonPropertyName("number")] string? Number,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("uuid")] string? Uuid,
    [property: JsonPropertyName("isRegistered")] bool IsRegistered);
