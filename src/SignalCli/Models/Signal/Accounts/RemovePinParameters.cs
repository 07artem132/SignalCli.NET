using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Wire-DTO для <c>removePin</c> (DESTRUCTIVE; significantly weakens account security).
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 6. Pinned до
/// <c>RemovePinCommand.java @ bda4e7fc</c>.
/// <para>
/// Idempotent при upstream-рівні (no-op якщо PIN не set). Після виклику account можна
/// re-register з будь-якого device БЕЗ PIN'у.
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер.</param>
[PublicAPI]
public sealed record RemovePinParameters(
    [property: JsonPropertyName("account")] string Account);
