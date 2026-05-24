using SignalCli.Models.Rpc;

namespace SignalCli.Exceptions;

/// <summary>
/// Виняток, що виникає коли signal-cli повертає JSON-RPC error code <c>-5</c> (rate limit).
/// </summary>
/// <remarks>
/// signal-cli-protocol-alignment (typed-rpc-errors): derived тип для high-leverage коду,
/// щоб consumer'и могли робити <c>catch (RateLimitException)</c> + retry-with-backoff
/// замість inspect-message-text. <see cref="JsonRpcException.Error"/>.Code завжди <c>-5</c>
/// (= <see cref="JsonRpcErrorCode.RateLimit"/>) by construction.
/// </remarks>
public sealed class RateLimitException : JsonRpcException
{
    /// <summary>Створює новий екземпляр <see cref="RateLimitException"/> з RPC-error payload.</summary>
    /// <param name="error">JSON-RPC error від signal-cli.</param>
    public RateLimitException(JsonRpcError error) : base(error) { }
}
