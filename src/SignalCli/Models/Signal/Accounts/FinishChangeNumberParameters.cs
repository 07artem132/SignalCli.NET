using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Wire-DTO для <c>finishChangeNumber</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 6. Pinned до
/// <c>FinishChangeNumberCommand.java @ bda4e7fc</c>.
/// </remarks>
/// <param name="Account">OLD E.164.</param>
/// <param name="Number">NEW E.164.</param>
/// <param name="VerificationCode">SMS/voice code.</param>
/// <param name="Pin">Registration-lock PIN нового номера (nullable).</param>
[PublicAPI]
public sealed record FinishChangeNumberParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("number")] string Number,
    [property: JsonPropertyName("verificationCode")] string VerificationCode,
    [property: JsonPropertyName("pin")] string? Pin);
