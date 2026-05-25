using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>submitRateLimitChallenge</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 8. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/SubmitRateLimitChallengeCommand.java @ bda4e7fc</c>.
/// <para>
/// <b>Workflow:</b> challenge token приходить з <c>error.data</c> попереднього <c>send</c> що
/// failed з code <c>-5 RateLimit</c>; consumer розв'язує CAPTCHA на
/// <see href="https://signalcaptchas.org/challenge/generate.html"/> і submit'ить token+captcha.
/// </para>
/// <para>
/// <b>§F24:</b> CaptchaRejected (<c>-6</c>) — <see cref="Exceptions.CaptchaRequiredException"/>
/// уже dispatch'иться через <see cref="Services.Rpc.JsonRpcClient"/> з Wave 1.
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер.</param>
/// <param name="Challenge">Opaque challenge token з prior rate-limit error.</param>
/// <param name="Captcha">Captcha solution token.</param>
[PublicAPI]
public sealed record SubmitRateLimitChallengeParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("challenge")] string Challenge,
    [property: JsonPropertyName("captcha")] string Captcha);
