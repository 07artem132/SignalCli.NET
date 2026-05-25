using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Message;

/// <summary>Wire-DTO для <c>sendPaymentNotification</c>. <b>Single recipient only</b>.</summary>
/// <remarks>
/// signal-cli-api-coverage Wave 7. Pinned до <c>SendPaymentNotificationCommand.java @ bda4e7fc</c>.
/// <para>
/// <see cref="Receipt"/> — base64-encoded MobileCoin receipt blob; signal-cli decodes server-side
/// через <c>Base64.getDecoder().decode</c>. NO note-to-self / notify-self / group support.
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер відправника.</param>
/// <param name="Recipient">Single recipient (NOT list). Phone/UUID.</param>
/// <param name="Receipt">Base64-encoded MobileCoin receipt bytes.</param>
/// <param name="Note">Optional plain-text note.</param>
[PublicAPI]
public sealed record SendPaymentNotificationParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("recipient")] string Recipient,
    [property: JsonPropertyName("receipt")] string Receipt,
    [property: JsonPropertyName("note")] string? Note);
