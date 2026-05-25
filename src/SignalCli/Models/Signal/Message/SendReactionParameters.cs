using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Message;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>sendReaction</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 1. Field names mirror Jackson record exactly — kebab-case
/// per signal-cli argparse <c>dest</c> convention (camelCase aliases теж приймаються upstream'ом
/// через <c>JsonRpcNamespace.get</c>, але kebab — canonical wire shape).
/// <para>
/// signal-cli RPC mapping: see
/// <c>src/main/java/org/asamk/signal/commands/SendReactionCommand.java</c> @ <c>bda4e7fc</c>.
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер акаунту-відправника.</param>
/// <param name="Recipients">Список recipients (опційно — конкатенується з GroupIds/Usernames/NoteToSelf).</param>
/// <param name="GroupIds">Список base64-id груп (опційно).</param>
/// <param name="Usernames">Список Signal username'ів (опційно).</param>
/// <param name="NoteToSelf">Слати у noteToSelf-thread.</param>
/// <param name="NotifySelf">§F20: регулює self-copy semantics (унікально для sendReaction).</param>
/// <param name="Emoji">Emoji-реакція (обов'язково).</param>
/// <param name="TargetAuthor">Автор оригінального повідомлення (опційно — див. quirk у <see cref="ReactionOptions"/>).</param>
/// <param name="TargetTimestamp">Send-timestamp оригіналу.</param>
/// <param name="Remove">Чи видалити попередню реакцію.</param>
/// <param name="Story">Чи реакція на Story-повідомлення.</param>
[PublicAPI]
public sealed record SendReactionParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("recipient")] IEnumerable<string>? Recipients,
    [property: JsonPropertyName("group-id")] IEnumerable<string>? GroupIds,
    [property: JsonPropertyName("username")] IEnumerable<string>? Usernames,
    [property: JsonPropertyName("note-to-self")] bool NoteToSelf,
    [property: JsonPropertyName("notify-self")] bool NotifySelf,
    [property: JsonPropertyName("emoji")] string Emoji,
    [property: JsonPropertyName("target-author")] string? TargetAuthor,
    [property: JsonPropertyName("target-timestamp")] long TargetTimestamp,
    [property: JsonPropertyName("remove")] bool Remove,
    [property: JsonPropertyName("story")] bool Story);
