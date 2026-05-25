using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Groups;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>quitGroup</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 2. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/QuitGroupCommand.java @ bda4e7fc</c>.
/// <para>
/// <b>§F8 idempotent NotAGroupMember:</b> якщо вже не member групи, upstream silently
/// logs "User is not a group member" і повертає <c>{}</c> (empty object). Сервіс-метод
/// інтерпретує це як success per CLAUDE.md rule #14 (prefer idempotency over throwing).
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
/// <param name="GroupId">Base64 id групи. Required.</param>
/// <param name="Delete">Якщо <c>true</c>, локально видалити дані групи після quit.</param>
/// <param name="Admins">Список нових admin'ів (потрібен якщо акаунт — last admin групи).</param>
[PublicAPI]
public sealed record QuitGroupParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("groupId")] string GroupId,
    [property: JsonPropertyName("delete")] bool Delete,
    [property: JsonPropertyName("admins")] IEnumerable<string>? Admins);
