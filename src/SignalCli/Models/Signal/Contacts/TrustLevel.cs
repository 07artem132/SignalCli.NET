using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Contacts;

/// <summary>
/// Trust-рівень identity-key контакту. Wire-shape — UPPER_SNAKE_CASE Java enum name verbatim
/// (<c>"UNTRUSTED"</c> / <c>"TRUSTED_UNVERIFIED"</c> / <c>"TRUSTED_VERIFIED"</c>).
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 3 (`contacts-identity`). Pinned до
/// <c>lib/src/main/java/org/asamk/signal/manager/api/TrustLevel.java @ bda4e7fc</c>.
/// <para>
/// Emit'ить upstream'ом через <c>id.trustLevel().name()</c> (raw enum-name) у
/// <c>ListIdentitiesCommand.java:80</c>. Receive-side mapping відбувається у
/// сервіс-методі <c>SignalContacts.ListIdentitiesAsync</c> з string на цей enum
/// (UPPER_SNAKE → PascalCase).
/// </para>
/// </remarks>
[PublicAPI]
public enum TrustLevel
{
    /// <summary>Identity не довірена (default після першого contact'у).</summary>
    Untrusted,

    /// <summary>Identity довірена implicitly (trust-all-known-keys path).</summary>
    TrustedUnverified,

    /// <summary>Identity довірена explicitly (safety-number verification path).</summary>
    TrustedVerified,
}
