using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Message;

/// <summary>
/// Відповідь на запит відправки повідомлення.
/// </summary>
/// <remarks>
/// Містить результати відправки для кожного отримувача та часову мітку відправлення.
/// </remarks>
/// <param name="Results">Список результатів відправки для кожного отримувача.</param>
/// <param name="TimeStamp">Часова мітка відправлення повідомлення.</param>
[PublicAPI]
public sealed record SendMessageResponse(
    [property: JsonPropertyName("results")] List<SendMessageResult>? Results,
    [property: JsonPropertyName("timestamp")] long TimeStamp);

/// <summary>
/// Адреса отримувача повідомлення.
/// </summary>
/// <remarks>
/// Містить ідентифікаційну інформацію про отримувача.
/// </remarks>
/// <param name="Uuid">UUID отримувача.</param>
/// <param name="Number">Номер телефону отримувача.</param>
[PublicAPI]
public sealed record RecipientAddress(
    [property: JsonPropertyName("uuid")] string Uuid,
    [property: JsonPropertyName("number")] string Number
);

/// <summary>
/// Результат відправки повідомлення конкретному отримувачу.
/// </summary>
/// <remarks>
/// Містить інформацію про статус доставки повідомлення.
/// </remarks>
/// <param name="RecipientAddress">Адреса отримувача повідомлення.</param>
/// <param name="Type">Тип результату (наприклад, "SUCCESS", "ERROR").</param>
[PublicAPI]
public sealed record SendMessageResult(
    [property: JsonPropertyName("recipientAddress")] RecipientAddress RecipientAddress,
    [property: JsonPropertyName("type")] string Type);