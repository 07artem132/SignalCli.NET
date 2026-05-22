using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Events;

/// <summary>
/// Аргументи події синхронізації між пристроями.
/// </summary>
/// <remarks>
/// Містить дані для синхронізації між різними пристроями одного облікового запису.
/// Включає надіслані повідомлення, прочитані повідомлення, заблоковані контакти, тощо.
/// </remarks>
/// <param name="SubscriptionId">Ідентифікатор підписки.</param>
/// <param name="Account">Ідентифікатор облікового запису одержувача.</param>
/// <param name="SyncMessage">Об'єкт з даними синхронізації.</param>
/// <param name="Source">Загальний ідентифікатор джерела повідомлення.</param>
/// <param name="SourceNumber">Номер телефону відправника.</param>
/// <param name="SourceUuid">UUID відправника.</param>
/// <param name="SourceName">Ім'я відправника.</param>
/// <param name="SourceDevice">Ідентифікатор пристрою відправника.</param>
/// <param name="Timestamp">Часова мітка повідомлення.</param>
/// <param name="ServerReceivedTimestamp">Часова мітка отримання сервером.</param>
/// <param name="ServerDeliveredTimestamp">Часова мітка доставки сервером.</param>
[PublicAPI]
public record SyncEventArgs(
    int SubscriptionId,
    string? Account,
    JsonSyncMessage SyncMessage,
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
