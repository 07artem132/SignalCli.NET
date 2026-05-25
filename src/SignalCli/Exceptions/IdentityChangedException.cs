using SignalCli.Models.Rpc;

namespace SignalCli.Exceptions;

/// <summary>
/// Виняток, що сигналізує про <b>зміну identity-key вже відомого контакту</b>
/// (re-install на пристрої співрозмовника), на відміну від першого contact'у з невідомим
/// identity (також JSON-RPC код <c>-4</c>, але семантично інший випадок).
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 1 (`messaging-interactive`): consumer-actionable derived
/// тип поверх <see cref="UntrustedIdentityException"/>. Caller може зробити
/// <c>catch (IdentityChangedException) { await _signalContacts.TrustAsync(...); /* retry */ }</c>
/// замість generic <c>catch (UntrustedIdentityException)</c> + inspect-message-text.
/// <para>
/// <b>Опт-ін у hierarchy:</b> цей тип НЕ диспатчиться окремо з
/// <c>JsonRpcClient.InvokeMethodAsync</c> (signal-cli повертає той самий код <c>-4</c>
/// для обох випадків — first-contact і re-install). Замість цього сервіс-методи у
/// signal-cli-сайті (наприклад <c>SendReactionAsync</c>) ловлять
/// <see cref="UntrustedIdentityException"/> і за повідомленням ре-throw'ять його як
/// <see cref="IdentityChangedException"/>, якщо identity вже існувала у локальному trust-store.
/// </para>
/// <para>
/// signal-cli RPC mapping: see <c>src/main/java/org/asamk/signal/jsonrpc/SignalJsonRpcCommandHandler.java</c>
/// @ <c>bda4e7fc</c> (lines 248-273 — promotion of <c>hasOnlyUntrustedIdentity()</c> into
/// JSON-RPC <c>-4 UNTRUSTED_KEY_ERROR</c>).
/// </para>
/// </remarks>
public sealed class IdentityChangedException : UntrustedIdentityException
{
    /// <summary>Створює новий екземпляр <see cref="IdentityChangedException"/> з RPC-error payload.</summary>
    /// <param name="error">JSON-RPC error від signal-cli (Code = <c>-4</c>).</param>
    public IdentityChangedException(JsonRpcError error) : base(error) { }
}
