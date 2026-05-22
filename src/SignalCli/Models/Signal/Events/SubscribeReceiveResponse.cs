using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Events;

/// <summary>
/// Відповідь на запит підписки на отримання подій.
/// </summary>
/// <remarks>
/// Містить ідентифікатор створеної підписки, який використовується 
/// для подальшої ідентифікації подій та відписки.
/// </remarks>
/// <param name="id">Ідентифікатор підписки.</param>
[PublicAPI]
public sealed record SubscribeReceiveResponse(int id);