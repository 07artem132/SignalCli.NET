using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Wire-DTO для <c>unregister</c> (DESTRUCTIVE).
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 6. Pinned до
/// <c>UnregisterCommand.java @ bda4e7fc</c>.
/// <para>
/// <see cref="DeleteAccount"/>=<c>false</c> → unregister (reversible через re-register; ламає
/// існуючі secure sessions). <see cref="DeleteAccount"/>=<c>true</c> → irreversibly deletes
/// account з Signal серверів; номер стає available для re-registration будь-ким.
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер.</param>
/// <param name="DeleteAccount">Якщо <c>true</c> — irreversibly delete account server-side.</param>
[PublicAPI]
public sealed record UnregisterParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("deleteAccount")] bool DeleteAccount);
