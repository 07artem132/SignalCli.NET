using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Опції для <c>updateConfiguration</c> (DESTRUCTIVE; syncs config до linked devices).
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 6. Pinned до
/// <c>UpdateConfigurationCommand.java @ bda4e7fc</c>. Поля — nullable bool;
/// <c>null</c> = "не міняти".
/// </remarks>
[PublicAPI]
public sealed record UpdateConfigurationOptions
{
    /// <summary>E.164 номер.</summary>
    public required string Account { get; init; }

    /// <summary>Чи send read receipts. <c>null</c> = не міняти.</summary>
    public bool? ReadReceipts { get; init; }

    /// <summary>Чи show unidentified-delivery indicators. <c>null</c> = не міняти.</summary>
    public bool? UnidentifiedDeliveryIndicators { get; init; }

    /// <summary>Чи send/show typing indicators. <c>null</c> = не міняти.</summary>
    public bool? TypingIndicators { get; init; }

    /// <summary>Чи auto-generate link previews. <c>null</c> = не міняти.</summary>
    public bool? LinkPreviews { get; init; }
}
