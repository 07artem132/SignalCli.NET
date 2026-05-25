using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Contacts;

/// <summary>
/// Один identity-запис у відповіді <c>listIdentities</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 3. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/ListIdentitiesCommand.java:88-96 @ bda4e7fc</c>
/// (private <c>JsonIdentity</c> record там само).
/// </remarks>
/// <param name="Number">E.164 phone, nullable.</param>
/// <param name="Uuid">ACI UUID string, nullable.</param>
/// <param name="Fingerprint">Hex-encoded fingerprint bytes.</param>
/// <param name="SafetyNumber">Space-formatted 60-digit safety number.</param>
/// <param name="ScannableSafetyNumber">Base64 scannable safety number; <c>null</c> якщо недоступно.</param>
/// <param name="TrustLevel">UPPER_SNAKE Java enum name (e.g. <c>"TRUSTED_VERIFIED"</c>).</param>
/// <param name="AddedTimestamp">Unix-ms коли identity додано.</param>
[PublicAPI]
public sealed record JsonIdentity(
    [property: JsonPropertyName("number")] string? Number,
    [property: JsonPropertyName("uuid")] string? Uuid,
    [property: JsonPropertyName("fingerprint")] string Fingerprint,
    [property: JsonPropertyName("safetyNumber")] string SafetyNumber,
    [property: JsonPropertyName("scannableSafetyNumber")] string? ScannableSafetyNumber,
    [property: JsonPropertyName("trustLevel")] string TrustLevel,
    [property: JsonPropertyName("addedTimestamp")] long AddedTimestamp);
