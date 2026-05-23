using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Groups;

/// <summary>
/// Відповідь на запит списку груп Signal.
/// </summary>
/// <remarks>
/// post-modernize-tuning §4.20 (audit N10): wrapper-record над <see cref="IReadOnlyList{T}"/>.
/// Раніше тип успадковував <c>List&lt;Group&gt;</c>, що давало консумеру деструктивні
/// мутатори на даних, що прийшли від сервера. Wire-shape JSON-масиву зберігається
/// через <see cref="ListGroupsResponseConverter"/>.
/// </remarks>
[PublicAPI]
[JsonConverter(typeof(ListGroupsResponseConverter))]
public sealed record ListGroupsResponse(IReadOnlyList<Group> Items) : IReadOnlyList<Group>
{
    /// <summary>Кількість груп у відповіді.</summary>
    public int Count => Items.Count;

    /// <summary>Доступ до групи за індексом.</summary>
    public Group this[int index] => Items[index];

    /// <inheritdoc/>
    public IEnumerator<Group> GetEnumerator() => Items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Конвертер, що читає/пише <see cref="ListGroupsResponse"/> як плоский JSON-масив.
/// </summary>
internal sealed class ListGroupsResponseConverter : JsonConverter<ListGroupsResponse>
{
    public override ListGroupsResponse Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Очікувався JSON-масив для ListGroupsResponse.");
        var list = JsonSerializer.Deserialize<List<Group>>(ref reader, options)
                   ?? [];
        return new ListGroupsResponse(list);
    }

    public override void Write(Utf8JsonWriter writer, ListGroupsResponse value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, (List<Group>)value.Items, options);
}

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
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("description")]
    string? Description,
    [property: JsonPropertyName("isMember")] bool IsMember,
    [property: JsonPropertyName("isBlocked")] bool IsBlocked,
    [property: JsonPropertyName("messageExpirationTime")]
    int MessageExpirationTime,
    [property: JsonPropertyName("members")] List<Member> Members,
    [property: JsonPropertyName("pendingMembers")]
    List<Member> PendingMembers,
    [property: JsonPropertyName("requestingMembers")]
    List<Member> RequestingMembers,
    [property: JsonPropertyName("admins")] List<Member> Admins,
    [property: JsonPropertyName("banned")] List<Member> Banned,
    [property: JsonPropertyName("permissionAddMember")]
    string PermissionAddMember,
    [property: JsonPropertyName("permissionEditDetails")]
    string PermissionEditDetails,
    [property: JsonPropertyName("permissionSendMessage")]
    string PermissionSendMessage,
    [property: JsonPropertyName("groupInviteLink")]
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
    [property: JsonPropertyName("number")] string? Number,
    [property: JsonPropertyName("uuid")] string Uuid
);
