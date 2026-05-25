using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>sendContacts</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 8. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/SendContactsCommand.java @ bda4e7fc</c>.
/// <para>
/// <b>One-way push</b> до linked devices через <c>SyncMessage.Contacts</c>. НЕ fetch'ить —
/// inverse direction; для fetch use <see cref="Interfaces.Signal.ISignalAccounts.SyncAccountAsync"/>.
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер.</param>
[PublicAPI]
public sealed record SendContactsParameters(
    [property: JsonPropertyName("account")] string Account);
