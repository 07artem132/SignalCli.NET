using SignalCli.Models.Rpc;

namespace SignalCli.Exceptions;

/// <summary>
/// Виняток, що виникає коли signal-cli повертає JSON-RPC error code <c>-4</c>
/// (untrusted identity — key verification failure для отримувача).
/// </summary>
/// <remarks>
/// signal-cli-protocol-alignment (typed-rpc-errors): derived тип для high-leverage коду,
/// щоб consumer'и могли robити <c>catch (UntrustedIdentityException)</c> + verify-safety-number
/// flow замість inspect-message-text. <see cref="JsonRpcException.Error"/>.Code завжди <c>-4</c>
/// (= <see cref="JsonRpcErrorCode.UntrustedIdentity"/>) by construction.
/// <para>
/// signal-cli-api-coverage Wave 1 (messaging-interactive): тип un-sealed щоб дозволити
/// derivation <see cref="IdentityChangedException"/> для опт-ін різнення "re-install уже
/// відомого контакту" vs "first-contact untrusted" (upstream signal-cli обидва випадки емітить
/// тим самим кодом <c>-4</c>, тож розрізнення — client-side concern).
/// </para>
/// </remarks>
public class UntrustedIdentityException : JsonRpcException
{
    /// <summary>Створює новий екземпляр <see cref="UntrustedIdentityException"/> з RPC-error payload.</summary>
    /// <param name="error">JSON-RPC error від signal-cli.</param>
    public UntrustedIdentityException(JsonRpcError error) : base(error) { }
}
