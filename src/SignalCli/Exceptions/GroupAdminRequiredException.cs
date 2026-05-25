using SignalCli.Models.Rpc;

namespace SignalCli.Exceptions;

/// <summary>
/// Виняток, що виникає коли signal-cli повертає JSON-RPC error code <c>-1</c> (UserError)
/// з повідомленням, яке вказує на нестачу admin-прав у групі (повідомлення містить
/// токен "admin").
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 1 (`messaging-interactive`): high-leverage derived тип
/// поверх generic <see cref="JsonRpcException"/> для UserError-коду, дозволяє consumer'у
/// зробити <c>catch (GroupAdminRequiredException)</c> + escalate-to-human flow замість
/// inspect-message-text по локалізованих рядках.
/// <para>
/// <b>Диспатч.</b> <c>JsonRpcClient.InvokeMethodAsync</c> здійснює message-match:
/// якщо <see cref="JsonRpcException.Error"/>.Code == <c>-1</c> ТА Message містить
/// (case-insensitive) "admin" — кидається цей тип; інакше — base <see cref="JsonRpcException"/>.
/// Heuristic зумисно weak — upstream signal-cli не виділяє admin-required у окремий код,
/// тож точність "100%" недосяжна; false-positive (наприклад інше повідомлення яке згадує
/// admin) — прийнятна ціна за typed-catch ergonomics.
/// </para>
/// <para>
/// signal-cli RPC mapping: see <c>lib/src/main/java/org/asamk/signal/manager/groups/GroupHelper.java</c>
/// @ <c>bda4e7fc</c> (validates admin-role перед mutating-group operations; throws
/// <c>UserErrorException</c> з повідомленням типу "User is not an admin of the group" /
/// "Only admins can …").
/// </para>
/// </remarks>
public sealed class GroupAdminRequiredException : JsonRpcException
{
    /// <summary>Створює новий екземпляр <see cref="GroupAdminRequiredException"/> з RPC-error payload.</summary>
    /// <param name="error">JSON-RPC error від signal-cli (Code = <c>-1</c>, Message contains "admin").</param>
    public GroupAdminRequiredException(JsonRpcError error) : base(error) { }
}
