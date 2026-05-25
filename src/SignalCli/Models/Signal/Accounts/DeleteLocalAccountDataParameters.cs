using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Wire-DTO для <c>deleteLocalAccountData</c> (DESTRUCTIVE — CANNOT BE UNDONE).
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 6. Pinned до
/// <c>DeleteLocalAccountDataCommand.java @ bda4e7fc</c>.
/// <para>
/// Wipe'ає local account directory: identity keys, sessions, contacts, profile cache.
/// Re-registration після цього створить новий identity — всі контакти побачать
/// "safety-number-changed".
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер.</param>
/// <param name="IgnoreRegistered">Якщо <c>true</c> — wipe навіть якщо account ще registered server-side (для recovery scenarios).</param>
[PublicAPI]
public sealed record DeleteLocalAccountDataParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("ignoreRegistered")] bool IgnoreRegistered);
