using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Events;

/// <summary>
/// Відповідь на запит підписки на отримання подій.
/// </summary>
/// <remarks>
/// Містить ідентифікатор створеної підписки, який використовується
/// для подальшої ідентифікації подій та відписки.
/// post-modernize-tuning §4.2 (audit D1): PascalCase property;
/// wire-level JSON field `id` зберігається через <c>[JsonPropertyName]</c>.
/// </remarks>
/// <param name="Id">Ідентифікатор підписки.</param>
[PublicAPI]
public sealed record SubscribeReceiveResponse([property: JsonPropertyName("id")] int Id);