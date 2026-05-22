using JetBrains.Annotations;
using Newtonsoft.Json;

namespace SignalCli.Models.Signal.Groups;

/// <summary>
/// Відповідь на запит списку груп.
/// </summary>
/// <remarks>
/// Наслідує List&lt;Group&gt; і містить колекцію груп з детальною інформацією.
/// </remarks>
[PublicAPI]
public class ListGroupsResponse() : List<Group>;

/// <summary>
/// Інформація про групу Signal.
/// </summary>
/// <remarks>
/// Містить детальні дані про групу: ідентифікатор, назву, опис, список учасників,
/// налаштування та права доступу.
/// </remarks>
/// <param name="Id">Унікальний ідентифікатор групи.</param>
/// <param name="Name">Назва групи.</param>
/// <param name="Description">Опис групи.</param>
/// <param name="IsMember">Чи є поточний користувач учасником групи.</param>
/// <param name="IsBlocked">Чи заблокована група для поточного користувача.</param>
/// <param name="MessageExpirationTime">Час автоматичного видалення повідомлень у секундах (0 - вимкнено).</param>
/// <param name="Members">Список активних учасників групи.</param>
/// <param name="PendingMembers">Список учасників, які очікують підтвердження.</param>
/// <param name="RequestingMembers">Список користувачів, які запитали участь у групі.</param>
/// <param name="Admins">Список адміністраторів групи.</param>
/// <param name="Banned">Список заблокованих користувачів.</param>
/// <param name="PermissionAddMember">Хто може додавати учасників ("EVERY_MEMBER", "ONLY_ADMINS").</param>
/// <param name="PermissionEditDetails">Хто може редагувати деталі групи ("EVERY_MEMBER", "ONLY_ADMINS").</param>
/// <param name="PermissionSendMessage">Хто може надсилати повідомлення ("EVERY_MEMBER", "ONLY_ADMINS").</param>
/// <param name="GroupInviteLink">Посилання для запрошення в групу.</param>
[PublicAPI]
public record Group(
    [property: JsonProperty("id")] string Id,
    [property: JsonProperty("name")] string? Name,
    [property: JsonProperty("description")]
    string? Description,
    [property: JsonProperty("isMember")] bool IsMember,
    [property: JsonProperty("isBlocked")] bool IsBlocked,
    [property: JsonProperty("messageExpirationTime")]
    int MessageExpirationTime,
    [property: JsonProperty("members")] List<Member> Members,
    [property: JsonProperty("pendingMembers")]
    List<Member> PendingMembers,
    [property: JsonProperty("requestingMembers")]
    List<Member> RequestingMembers,
    [property: JsonProperty("admins")] List<Member> Admins,
    [property: JsonProperty("banned")] List<Member> Banned,
    [property: JsonProperty("permissionAddMember")]
    string PermissionAddMember,
    [property: JsonProperty("permissionEditDetails")]
    string PermissionEditDetails,
    [property: JsonProperty("permissionSendMessage")]
    string PermissionSendMessage,
    [property: JsonProperty("groupInviteLink")]
    string? GroupInviteLink
);

/// <summary>
/// Інформація про учасника групи.
/// </summary>
/// <remarks>
/// Містить дані для ідентифікації користувача, який є учасником групи.
/// </remarks>
/// <param name="Number">Номер телефону користувача (може бути null для контактів без номеру).</param>
/// <param name="Uuid">Унікальний ідентифікатор користувача.</param>
[PublicAPI]
public record Member(
    [property: JsonProperty("number")] string? Number,
    [property: JsonProperty("uuid")] string Uuid
);