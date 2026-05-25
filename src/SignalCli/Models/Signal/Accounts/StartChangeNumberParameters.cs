using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Wire-DTO для <c>startChangeNumber</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 6. Pinned до
/// <c>StartChangeNumberCommand.java @ bda4e7fc</c>.
/// </remarks>
/// <param name="Account">OLD E.164.</param>
/// <param name="Number">NEW E.164 (argparse positional named "number").</param>
/// <param name="Voice">§F4 voice verification (bool).</param>
/// <param name="Captcha">Captcha token (nullable).</param>
[PublicAPI]
public sealed record StartChangeNumberParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("number")] string Number,
    [property: JsonPropertyName("voice")] bool Voice,
    [property: JsonPropertyName("captcha")] string? Captcha);
