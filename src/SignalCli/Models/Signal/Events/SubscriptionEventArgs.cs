using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Events;

/// <summary>
/// Аргументи події підписки Signal.
/// </summary>
/// <remarks>
/// Містить інформацію про підписку та результат події.
/// Використовується для загортання повідомлень Signal при отриманні нотифікацій.
/// </remarks>
/// <param name="Subscription">Ідентифікатор підписки.</param>
/// <param name="Result">Результат події з деталями повідомлення.</param>
[method: JsonConstructor]
[PublicAPI]
public record SubscriptionEventArgs(
    [property: JsonPropertyName("subscription")] int Subscription,
    [property: JsonPropertyName("result")] SignalEventArgs Result);