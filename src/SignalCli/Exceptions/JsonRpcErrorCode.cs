namespace SignalCli.Exceptions;

/// <summary>
/// JSON-RPC error codes used by signal-cli — standard JSON-RPC 2.0 codes
/// (<c>-32700..-32603</c>) plus signal-cli-specific custom codes (<c>-1..-6</c>).
/// </summary>
/// <remarks>
/// <para>
/// signal-cli-protocol-alignment (typed-rpc-errors): pinned against signal-cli source at
/// commit <c>bda4e7f</c> (after 0.14.3) — see
/// <c>src/main/java/org/asamk/signal/jsonrpc/SignalJsonRpcCommandHandler.java:35-280</c>
/// for the custom codes and
/// <c>src/main/java/org/asamk/signal/jsonrpc/JsonRpcResponse.java:75-119</c>
/// for the standard ones.
/// </para>
/// <para>
/// Consumers typically use this via <see cref="JsonRpcException.KnownCode"/> to switch on
/// the typed enum instead of integer codes. Two high-leverage codes have derived exception
/// types: <see cref="RateLimitException"/> (<c>-5</c>) and
/// <see cref="UntrustedIdentityException"/> (<c>-4</c>).
/// </para>
/// </remarks>
public enum JsonRpcErrorCode
{
    // ---- Standard JSON-RPC 2.0 ----

    /// <summary>Malformed JSON received (server could not parse).</summary>
    ParseError = -32700,

    /// <summary>Request object does not conform to JSON-RPC shape.</summary>
    InvalidRequest = -32600,

    /// <summary>Method does not exist or is not available.</summary>
    MethodNotFound = -32601,

    /// <summary>Invalid method parameters (wrong account, missing required field, etc.).</summary>
    InvalidParams = -32602,

    /// <summary>Generic server-side error during command dispatch.</summary>
    InternalError = -32603,

    // ---- signal-cli specific (SignalJsonRpcCommandHandler.java:35-280) ----

    /// <summary>User-input error (bad phone number, invalid argument, business-logic refusal).</summary>
    UserError = -1,

    /// <summary>I/O error (file system access, network failure during signal-cli execution).</summary>
    IoError = -3,

    /// <summary>
    /// Untrusted identity — key verification failure for the recipient.
    /// Surfaced as <see cref="UntrustedIdentityException"/>.
    /// </summary>
    UntrustedIdentity = -4,

    /// <summary>
    /// Server rate limit hit. Consumer should retry with exponential backoff.
    /// Surfaced as <see cref="RateLimitException"/>.
    /// </summary>
    RateLimit = -5,

    /// <summary>Captcha challenge rejected by the Signal server.</summary>
    CaptchaRejected = -6,
}
