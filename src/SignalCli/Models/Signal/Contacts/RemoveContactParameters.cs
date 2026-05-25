using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Contacts;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>removeContact</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 3. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/RemoveContactCommand.java @ bda4e7fc</c>.
/// <para>
/// <b>§F9 mutex (client-side):</b> service-метод <c>RemoveContactAsync</c> гарантує XOR;
/// при direct-construction цього DTO consumer несе відповідальність за wire-correctness.
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
/// <param name="Recipient">Recipient identifier.</param>
/// <param name="Hide">Якщо <c>true</c> — сховати, зберегти дані. Mutex з <see cref="Forget"/>.</param>
/// <param name="Forget">Якщо <c>true</c> — hard delete (keys/sessions/profile). Mutex з <see cref="Hide"/>.</param>
[PublicAPI]
public sealed record RemoveContactParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("recipient")] string Recipient,
    [property: JsonPropertyName("hide")] bool Hide,
    [property: JsonPropertyName("forget")] bool Forget);
