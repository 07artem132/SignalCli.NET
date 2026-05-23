using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Events;

/// <summary>
/// Аргументи події «вхідна цитата» (повідомлення містить лише <see cref="JsonDataMessage.Quote"/>
/// без власного тексту, реакції, стікера чи вкладень).
/// </summary>
/// <remarks>
/// Раніше такі повідомлення класифікувалися як "unknown" і відкидалися; тепер вони мають власний потік.
/// </remarks>
/// <param name="SubscriptionId">Ідентифікатор підписки.</param>
/// <param name="Account">Ідентифікатор облікового запису одержувача.</param>
/// <param name="DataMessage">Повний DataMessage (містить <see cref="JsonDataMessage.Quote"/>).</param>
/// <param name="Source">Загальний ідентифікатор джерела повідомлення.</param>
/// <param name="SourceNumber">Номер телефону відправника.</param>
/// <param name="SourceUuid">UUID відправника.</param>
/// <param name="SourceName">Ім'я відправника.</param>
/// <param name="SourceDevice">Ідентифікатор пристрою відправника.</param>
/// <param name="Timestamp">Часова мітка повідомлення.</param>
/// <param name="ServerReceivedTimestamp">Часова мітка отримання сервером.</param>
/// <param name="ServerDeliveredTimestamp">Часова мітка доставки сервером.</param>
[PublicAPI]
public record QuoteEventArgs(
    int SubscriptionId,
    string? Account,
    JsonDataMessage DataMessage,
    string? Source,
    string? SourceNumber,
    string? SourceUuid,
    string? SourceName,
    int? SourceDevice,
    long Timestamp,
    long ServerReceivedTimestamp,
    long ServerDeliveredTimestamp
) : BaseSignalEventArgs(SubscriptionId, Account,
    Source,
    SourceNumber,
    SourceUuid,
    SourceName,
    SourceDevice,
    Timestamp,
    ServerReceivedTimestamp,
    ServerDeliveredTimestamp);
