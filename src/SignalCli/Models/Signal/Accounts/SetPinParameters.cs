using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Wire-DTO для <c>setPin</c> (DESTRUCTIVE; sets registration-lock PIN на Signal SVR).
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 6. Pinned до
/// <c>SetPinCommand.java @ bda4e7fc</c>.
/// <para>
/// Existing PIN силюно перезаписується (немає "set new" vs "update" distinction). Signal SVR
/// rejects PIN'и шорше 4 chars з server-side error → <c>-3 IoError</c>; service-метод
/// pre-validates client-side.
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер.</param>
/// <param name="Pin">New registration-lock PIN (4-20 chars numeric, OR 4+ alphanumeric per Signal documentation).</param>
[PublicAPI]
public sealed record SetPinParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("pin")] string Pin);
