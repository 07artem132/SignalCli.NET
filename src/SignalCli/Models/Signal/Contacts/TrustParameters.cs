using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Contacts;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>trust</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 3. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/TrustCommand.java @ bda4e7fc</c>.
/// <para>
/// <b>Mutex client-side:</b> сервіс-методи <c>TrustAllKnownKeysAsync</c> та
/// <c>TrustVerifiedAsync</c> розводять виклики на два окремі API, тож на wire завжди
/// рівно одне з <see cref="TrustAllKnownKeys"/>/<see cref="VerifiedSafetyNumber"/>
/// виставлено. Argparse mutex group НЕ enforce'иться у JSON-RPC, тож якщо хтось напряму
/// будує цей wire-DTO — він/вона несе відповідальність за XOR.
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
/// <param name="Recipient">Single recipient identifier (phone/UUID/PNI/username).</param>
/// <param name="TrustAllKnownKeys">Trust усі відомі identity-keys recipient'а (testing-only).</param>
/// <param name="VerifiedSafetyNumber">60-digit safety-number/66-hex-fingerprint/base64 scannable.</param>
[PublicAPI]
public sealed record TrustParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("recipient")] string Recipient,
    [property: JsonPropertyName("trustAllKnownKeys")] bool TrustAllKnownKeys,
    [property: JsonPropertyName("verifiedSafetyNumber")] string? VerifiedSafetyNumber);
