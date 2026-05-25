using JetBrains.Annotations;
using System.Text.Json.Serialization;
using SignalCli.Models.Signal.Message;

namespace SignalCli.Models.Signal.Groups;

/// <summary>
/// Відповідь на <c>updateGroup</c>. Всі поля nullable, оскільки upstream conditionally додає
/// їх у HashMap (<c>UpdateGroupCommand.java:206-217 @ bda4e7fc</c>).
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 2.
/// <para>
/// <b>§F14 dimorphic <see cref="GroupId"/>:</b> поле present ЛИШЕ коли upstream створив
/// нову групу (request omitted <c>groupId</c>); absent при plain update існуючої групи.
/// Це дзеркало JoinGroup'ового <see cref="JoinGroupResponse.OnlyRequested"/>.
/// </para>
/// <para>
/// <see cref="Results"/> при create-path несе concat двох send-батчів (createGroup + updateGroup
/// — <c>UpdateGroupCommand.java:172-175 @ bda4e7fc</c>); consumer повинен ітерувати, не
/// припускати single batch.
/// </para>
/// </remarks>
/// <param name="TimeStamp">Send-timestamp. <c>null</c> якщо send не відбувся.</param>
/// <param name="Results">Per-recipient send results. Може бути concat (create + update).</param>
/// <param name="GroupId">§F14: present-only-on-create.</param>
[PublicAPI]
public sealed record UpdateGroupResponse(
    [property: JsonPropertyName("timestamp")] long? TimeStamp,
    [property: JsonPropertyName("results")] List<SendMessageResult>? Results,
    [property: JsonPropertyName("groupId"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? GroupId);
