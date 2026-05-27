using SignalCli.Models.Rpc;

namespace SignalCli.Exceptions;

/// <summary>
/// ⚠ <b>Deprecated — never dispatched.</b> Type залишається у public-API як <c>[Obsolete]</c> shim
/// до 5.0 per one-major-grace convention; видаляється у 5.0.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lifetime context.</b> Wave 1 (`messaging-interactive`, 4.1.0) припустив що signal-cli
/// розрізняє "re-install identity-key вже відомого контакту" vs "first-contact untrusted"
/// у JSON-RPC шарі — і додав цей derived тип щоб consumer'и могли пащеризувати
/// <c>catch (IdentityChangedException)</c>. Post-merge code review (2026-05-25, після landing
/// `signal-cli-api-coverage`) виявив що це припущення невірне: upstream
/// <c>SendMessageResultUtils.java:60 @ bda4e7fc</c> throws
/// <c>UntrustedKeyErrorException("Failed to send message due to untrusted identities")</c> —
/// **один fixed string**, завжди plural, no variation. <c>JsonSendMessageResult.Type</c> має
/// рівно одну identity-related enum value: <c>IDENTITY_FAILURE</c> (не
/// <c>IDENTITY_NEW</c>/<c>IDENTITY_CHANGED</c>). Тип ніколи не диспатчиться з
/// <c>JsonRpcClient.InvokeMethodAsync</c>; жодного <c>throw new IdentityChangedException(...)</c>
/// у production-коді немає.
/// </para>
/// <para>
/// <b>Migration.</b> Catch <see cref="UntrustedIdentityException"/> замість цього типу. Якщо потрібно
/// розрізнити "first-contact vs re-install" — це client-side concern: cache
/// <see cref="SignalCli.Interfaces.Signal.ISignalContacts.ListIdentitiesAsync"/> результати
/// у consumer-side trust store, порівнюй safety-number перед/після failure.
/// </para>
/// <para>
/// Pinned fact #8 у <see href="../../.claude/rules/signal-cli-protocol.md">.claude/rules/signal-cli-protocol.md</see>
/// документує цю upstream-нон-distinction; api-coverage-audit-followup §1 (capability
/// `identity-changed-deprecation`) — context для deprecation.
/// </para>
/// </remarks>
[Obsolete(
    "Speculative type that is never dispatched — upstream signal-cli has no protocol-level distinction between first-contact-unknown and re-installed identity (both → UntrustedKeyErrorException \"Failed to send message due to untrusted identities\"). Catch UntrustedIdentityException instead and disambiguate via ISignalContacts.ListIdentitiesAsync if needed. Will be removed in 5.0.",
    DiagnosticId = "SIGNALCLI001")]
public sealed class IdentityChangedException : UntrustedIdentityException
{
    /// <summary>Створює новий екземпляр <see cref="IdentityChangedException"/> з RPC-error payload.</summary>
    /// <param name="error">JSON-RPC error від signal-cli (Code = <c>-4</c>).</param>
    public IdentityChangedException(JsonRpcError error) : base(error) { }
}
