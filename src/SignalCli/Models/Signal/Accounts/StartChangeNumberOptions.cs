using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Опції для <c>startChangeNumber</c> (DESTRUCTIVE; розпочинає phone-number-change flow).
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 6. Pinned до
/// <c>StartChangeNumberCommand.java @ bda4e7fc</c>.
/// <para>
/// <see cref="Account"/> — поточний (OLD) номер; <see cref="NewNumber"/> — таргет (NEW). Wire
/// field — <c>number</c>; ми називаємо <see cref="NewNumber"/> для disambiguation.
/// </para>
/// <para>
/// <b>§F4 Voice:</b> на wire — <c>"voice": bool</c> (argparse short flag <c>-v</c>/<c>--voice</c>).
/// Default <c>false</c> = SMS verification; <c>true</c> = voice call. НЕ <c>--voice-verification</c>.
/// </para>
/// </remarks>
[PublicAPI]
public sealed record StartChangeNumberOptions
{
    /// <summary>OLD E.164 номер.</summary>
    public required string Account { get; init; }

    /// <summary>NEW E.164 номер (wire: <c>number</c>).</summary>
    public required string NewNumber { get; init; }

    /// <summary>§F4: voice verification (default false = SMS).</summary>
    public bool Voice { get; init; }

    /// <summary>Captcha token (потрібен при <c>CaptchaRequiredException</c> на попередньому виклику).</summary>
    public string? Captcha { get; init; }
}
