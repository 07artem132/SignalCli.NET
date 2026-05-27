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
/// Тип un-sealed щоб дозволити derivation (zero внутрішніх sealed-guards). api-coverage-audit-followup §1:
/// прямого useful subtype'у не залишилося — <see cref="IdentityChangedException"/> deprecated
/// як speculative-split (never dispatched). Якщо потрібен new derived type — додавай лише після
/// підтвердження upstream distinguisher (§0.5 cite-and-read).
/// </para>
/// </remarks>
public class UntrustedIdentityException : JsonRpcException
{
    /// <summary>Створює новий екземпляр <see cref="UntrustedIdentityException"/> з RPC-error payload.</summary>
    /// <param name="error">JSON-RPC error від signal-cli.</param>
    public UntrustedIdentityException(JsonRpcError error) : base(error) { }
}
