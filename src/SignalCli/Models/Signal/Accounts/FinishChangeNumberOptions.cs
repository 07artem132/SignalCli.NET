using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Опції для <c>finishChangeNumber</c> (DESTRUCTIVE; завершує phone-number change).
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 6. Pinned до
/// <c>FinishChangeNumberCommand.java @ bda4e7fc</c>.
/// <para>
/// Після успіху OLD номер більше НЕ associated з акаунтом; всі subsequent RPC envelopes
/// мусять використовувати NEW номер як <c>account</c>.
/// </para>
/// </remarks>
[PublicAPI]
public sealed record FinishChangeNumberOptions
{
    /// <summary>OLD E.164 номер (поточний account envelope).</summary>
    public required string Account { get; init; }

    /// <summary>NEW E.164 номер — той самий, що передавався у <c>StartChangeNumber</c>.</summary>
    public required string NewNumber { get; init; }

    /// <summary>6-digit verification code, отриманий через SMS/voice.</summary>
    public required string VerificationCode { get; init; }

    /// <summary>Registration-lock PIN нового номера (якщо встановлений).</summary>
    public string? Pin { get; init; }
}
