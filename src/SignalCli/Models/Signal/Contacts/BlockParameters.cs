using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Contacts;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>block</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 3. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/BlockCommand.java @ bda4e7fc</c>.
/// <para>
/// <b>Partial-application caveat:</b> upstream виконує recipient-block та group-block
/// послідовно як 2 окремі Manager calls. Якщо recipient-block кидає виняток,
/// group-block НЕ виконується. Якщо group-block кидає, recipient-block уже commit'нуто.
/// </para>
/// <para>
/// <b>Unknown group-id — лише warn-лог, не помилка.</b> Block для інших known-груп
/// продовжується. Consumer не отримує сигналу які group-ids не застосувались
/// — це upstream-quirk без типового API recovery-path'у.
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
/// <param name="Recipients">Recipient'и заблокувати.</param>
/// <param name="GroupIds">Group-id'и заблокувати.</param>
[PublicAPI]
public sealed record BlockParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("recipient")] IEnumerable<string>? Recipients,
    [property: JsonPropertyName("groupId")] IEnumerable<string>? GroupIds);
