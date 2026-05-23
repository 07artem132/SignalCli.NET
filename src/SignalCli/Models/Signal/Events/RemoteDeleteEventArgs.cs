using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Events;

/// <summary>
/// Аргументи події «віддалене видалення повідомлення» — відправник видалив у себе раніше
/// надіслане повідомлення; одержувач має його прибрати з UI.
/// </summary>
/// <param name="SubscriptionId">Ідентифікатор підписки.</param>
/// <param name="Account">Ідентифікатор облікового запису одержувача.</param>
/// <param name="RemoteDelete">Інформація про повідомлення, що видаляється.</param>
/// <param name="Source">Загальний ідентифікатор джерела повідомлення.</param>
/// <param name="SourceNumber">Номер телефону відправника.</param>
/// <param name="SourceUuid">UUID відправника.</param>
/// <param name="SourceName">Ім'я відправника.</param>
/// <param name="SourceDevice">Ідентифікатор пристрою відправника.</param>
/// <param name="Timestamp">Часова мітка повідомлення.</param>
/// <param name="ServerReceivedTimestamp">Часова мітка отримання сервером.</param>
/// <param name="ServerDeliveredTimestamp">Часова мітка доставки сервером.</param>
[PublicAPI]
public record RemoteDeleteEventArgs(
    int SubscriptionId,
    string Account,
    JsonRemoteDelete RemoteDelete,
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
