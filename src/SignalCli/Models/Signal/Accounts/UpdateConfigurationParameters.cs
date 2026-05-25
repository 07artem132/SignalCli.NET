using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Wire-DTO для <c>updateConfiguration</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 6. Pinned до
/// <c>UpdateConfigurationCommand.java @ bda4e7fc</c>.
/// </remarks>
[PublicAPI]
public sealed record UpdateConfigurationParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("readReceipts")] bool? ReadReceipts,
    [property: JsonPropertyName("unidentifiedDeliveryIndicators")] bool? UnidentifiedDeliveryIndicators,
    [property: JsonPropertyName("typingIndicators")] bool? TypingIndicators,
    [property: JsonPropertyName("linkPreviews")] bool? LinkPreviews);
