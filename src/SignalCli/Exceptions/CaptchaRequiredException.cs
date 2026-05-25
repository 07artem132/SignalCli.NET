using SignalCli.Models.Rpc;

namespace SignalCli.Exceptions;

/// <summary>
/// Виняток, що виникає коли signal-cli повертає JSON-RPC error code <c>-6</c>
/// (CaptchaRejected) — Signal-сервер вимагає від акаунту розв'язати CAPTCHA challenge
/// перед продовженням операції (зазвичай — щось пов'язане з rate-limit-розблокуванням).
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 1 (`messaging-interactive`): typed-catch для високо-leverage
/// коду <see cref="JsonRpcErrorCode.CaptchaRejected"/>. Consumer flow:
/// <list type="number">
/// <item>Відкрити URL <see href="https://signalcaptchas.org/registration/generate.html"/>
/// (для registration) або <see href="https://signalcaptchas.org/challenge/generate.html"/>
/// (для challenge — submitRateLimitChallenge у Wave 8) у браузері.</item>
/// <item>Розв'язати CAPTCHA; URL в адресному рядку матиме формат
/// <c>signalcaptcha://signal-recaptcha-v2.&lt;token&gt;</c>.</item>
/// <item>Передати token у відповідний submit-метод (Wave 8 — <c>SubmitRateLimitChallengeAsync</c>;
/// для register-flow — за межами JSON-RPC, через CLI).</item>
/// </list>
/// <para>
/// signal-cli RPC mapping: see <c>src/main/java/org/asamk/signal/jsonrpc/SignalJsonRpcCommandHandler.java</c>
/// @ <c>bda4e7fc</c> (lines 35-280 — error-code constants).
/// </para>
/// </remarks>
public sealed class CaptchaRequiredException : JsonRpcException
{
    /// <summary>Створює новий екземпляр <see cref="CaptchaRequiredException"/> з RPC-error payload.</summary>
    /// <param name="error">JSON-RPC error від signal-cli (Code = <c>-6</c>).</param>
    public CaptchaRequiredException(JsonRpcError error) : base(error) { }
}
