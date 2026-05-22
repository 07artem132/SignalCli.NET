using JetBrains.Annotations;
using Newtonsoft.Json;

namespace SignalCli.Models.Signal.Events;

/// <summary>
/// Параметри для запиту скасування підписки на події.
/// </summary>
/// <remarks>
/// Містить ідентифікатор підписки, яку потрібно скасувати.
/// Використовується в методі unsubscribeReceive.
/// </remarks>
/// <param name="Id">Ідентифікатор підписки, отриманий при subscribeReceive.</param>
[PublicAPI]
public sealed record UnsubscribeReceiveParameters(
    [property: JsonProperty("id")] int Id);