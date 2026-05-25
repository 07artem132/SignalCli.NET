using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Groups;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>updateGroup</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 2. Field naming convention — camelCase per upstream
/// <c>JsonRpcNamespace.dashSeparatedToCamelCaseString</c>; enum fields передаються як
/// kebab-case string (e.g. <c>"enabled-with-approval"</c>) — сервіс-метод робить
/// PascalCase→kebab mapping перед серіалізацією. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/UpdateGroupCommand.java @ bda4e7fc</c>.
/// </remarks>
/// <param name="Account">E.164 номер акаунту-власника.</param>
/// <param name="GroupId">Base64 group id. <c>null</c> → CREATE-mode (§F14).</param>
/// <param name="Name">Нова назва.</param>
/// <param name="Description">Новий опис.</param>
/// <param name="Avatar">Локальний шлях до avatar-файлу.</param>
/// <param name="Members">Members додати.</param>
/// <param name="RemoveMembers">Members видалити.</param>
/// <param name="Admins">Promote-to-admin.</param>
/// <param name="RemoveAdmins">Demote-from-admin.</param>
/// <param name="Bans">Members забанити.</param>
/// <param name="Unbans">Members розбанити.</param>
/// <param name="ResetLink">Regenerate link-password.</param>
/// <param name="Link">Group-invite-link state (kebab-case: <c>"enabled"</c>/<c>"enabled-with-approval"</c>/<c>"disabled"</c>).</param>
/// <param name="SetPermissionAddMember">Add-member permission (kebab-case <c>"every-member"</c>/<c>"only-admins"</c>).</param>
/// <param name="SetPermissionEditDetails">Edit-details permission.</param>
/// <param name="SetPermissionSendMessages">Send-messages permission.</param>
/// <param name="Expiration">Disappearing-message timer (seconds).</param>
[PublicAPI]
public sealed record UpdateGroupParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("groupId")] string? GroupId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("avatar")] string? Avatar,
    [property: JsonPropertyName("members")] IEnumerable<string>? Members,
    [property: JsonPropertyName("removeMembers")] IEnumerable<string>? RemoveMembers,
    [property: JsonPropertyName("admins")] IEnumerable<string>? Admins,
    [property: JsonPropertyName("removeAdmins")] IEnumerable<string>? RemoveAdmins,
    [property: JsonPropertyName("bans")] IEnumerable<string>? Bans,
    [property: JsonPropertyName("unbans")] IEnumerable<string>? Unbans,
    [property: JsonPropertyName("resetLink")] bool ResetLink,
    [property: JsonPropertyName("link")] string? Link,
    [property: JsonPropertyName("setPermissionAddMember")] string? SetPermissionAddMember,
    [property: JsonPropertyName("setPermissionEditDetails")] string? SetPermissionEditDetails,
    [property: JsonPropertyName("setPermissionSendMessages")] string? SetPermissionSendMessages,
    [property: JsonPropertyName("expiration")] int? Expiration);
