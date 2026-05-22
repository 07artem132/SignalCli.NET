using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Events;

/// <summary>
/// Відповідь на запит скасування підписки на події.
/// </summary>
/// <remarks>
/// Порожній запис, який підтверджує успішне скасування підписки.
/// </remarks>
[PublicAPI]
public sealed record UnsubscribeReceiveResponse();