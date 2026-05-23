using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Events;

/// <summary>
/// Базовий клас для всіх аргументів подій Signal.
/// </summary>
/// <remarks>
/// Містить спільні властивості, які присутні у всіх типах подій Signal.
/// Використовується як базовий клас для спеціалізованих подій.
/// </remarks>
/// <param name="SubscriptionId">Ідентифікатор підписки, через яку отримано подію.</param>
/// <param name="Account">Ідентифікатор облікового запису одержувача. Завжди заповнений: <c>SignalEventService.OnNotificationReceived</c> резолвить акаунт із <c>SubscriptionId</c> до того, як побудувати event-args.</param>
/// <param name="Source">Загальний ідентифікатор джерела повідомлення.</param>
/// <param name="SourceNumber">Номер телефону відправника.</param>
/// <param name="SourceUuid">UUID відправника.</param>
/// <param name="SourceName">Ім'я відправника.</param>
/// <param name="SourceDevice">Ідентифікатор пристрою відправника.</param>
/// <param name="Timestamp">Часова мітка події.</param>
/// <param name="ServerReceivedTimestamp">Часова мітка отримання сервером.</param>
/// <param name="ServerDeliveredTimestamp">Часова мітка доставки сервером.</param>
/// <remarks>
/// post-modernize-tuning §4.19 (audit N9): <c>Account</c> більше не nullable. На сайті
/// побудови events (<c>SignalEventService.OnNotificationReceived</c>) акаунт резолвиться
/// з SubscriptionId до того, як ми створюємо event-args; nullable раніше змушував кожного
/// підписника null-чекати, попри те що значення гарантовано є.
/// </remarks>
[PublicAPI]
public record BaseSignalEventArgs(
    int SubscriptionId,
    string Account,
    string? Source,
    string? SourceNumber,
    string? SourceUuid,
    string? SourceName,
    int? SourceDevice,
    long Timestamp,
    long ServerReceivedTimestamp,
    long ServerDeliveredTimestamp
);