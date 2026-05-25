using JetBrains.Annotations;
using System.Text.Json.Serialization;
using SignalCli.Models.Signal.Message;

namespace SignalCli.Models.Signal.Groups;

/// <summary>
/// Відповідь на <c>joinGroup</c>: timestamp send'у "join group" повідомлення, per-recipient
/// статуси, та новий <see cref="GroupId"/>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 2. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/JoinGroupCommand.java:58-77 @ bda4e7fc</c>.
/// <para>
/// <b>§F13 dimorphic <see cref="OnlyRequested"/>:</b> поле present-and-<c>true</c> ЛИШЕ
/// коли group вимагає admin-approval і ми ще не повноправний member; повністю
/// <i>відсутнє</i> (NOT <c>false</c>!) при прямому join. Тому тип — <c>bool?</c>, не <c>bool</c>:
/// <c>null</c> = direct join, <c>true</c> = pending-admin-approval. Wire ніколи не приймає
/// <c>false</c> для цього поля.
/// </para>
/// </remarks>
/// <param name="TimeStamp">Send-timestamp (epoch ms) "join group"-повідомлення.</param>
/// <param name="Results">Per-recipient send results. Може бути <c>null</c> якщо send не відбувся.</param>
/// <param name="GroupId">Base64 id групи, до якої приєдналися (новий або існуючий).</param>
/// <param name="OnlyRequested">§F13: dimorphic. <c>null</c> = direct join, <c>true</c> = pending approval.</param>
[PublicAPI]
public sealed record JoinGroupResponse(
    [property: JsonPropertyName("timestamp")] long TimeStamp,
    [property: JsonPropertyName("results")] List<SendMessageResult>? Results,
    [property: JsonPropertyName("groupId")] string GroupId,
    [property: JsonPropertyName("onlyRequested"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? OnlyRequested);
