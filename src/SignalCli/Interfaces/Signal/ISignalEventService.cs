using Microsoft.Extensions.Hosting;
using SignalCli.Exceptions;
using SignalCli.Models.Signal.Events;

namespace SignalCli.Interfaces.Signal;

/// <summary>
/// Сервіс для обробки подій Signal.
/// </summary>
/// <remarks>
/// Надає методи для підписки на різні типи подій Signal та потоки Rx 
/// для їх асинхронної обробки (текстові повідомлення, реакції, вкладення, тощо).
/// Використовує підхід реактивного програмування для обробки подій.
/// </remarks>
public interface ISignalEventService : IHostedService
{
    /// <summary>
    /// Підписується на отримання подій з облікового запису Signal.
    /// </summary>
    /// <param name="account">Номер телефону акаунту для підписки.</param>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Відповідь з ідентифікатором підписки.</returns>
    /// <exception cref="ArgumentNullException">Виникає, якщо account дорівнює null або порожній.</exception>
    /// <exception cref="InvalidOperationException">Виникає, якщо акаунт вже підписаний або при помилці підписки.</exception>
    /// <exception cref="JsonRpcException">Виникає при помилці JSON-RPC запиту.</exception>
    /// <exception cref="OperationCanceledException">Виникає, якщо операцію скасовано.</exception>
    Task<SubscribeReceiveResponse> SubscribeAsync(string account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Відписується від отримання подій Signal.
    /// </summary>
    /// <param name="subscriptionId">Ідентифікатор підписки, отриманий при SubscribeAsync.</param>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Відповідь про успішність відписки.</returns>
    /// <exception cref="ArgumentException">Виникає, якщо subscriptionId не є валідним.</exception>
    /// <exception cref="JsonRpcException">Виникає при помилці JSON-RPC запиту.</exception>
    /// <exception cref="OperationCanceledException">Виникає, якщо операцію скасовано.</exception>
    Task<UnsubscribeReceiveResponse> UnsubscribeAsync(int subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Потік текстових повідомлень.
    /// </summary>
    /// <remarks>
    /// Містить всі вхідні текстові повідомлення для підписаних акаунтів.
    /// </remarks>
    IObservable<TextMessageEventArgs> TextMessages { get; }

    /// <summary>
    /// Потік подій реакцій на повідомлення.
    /// </summary>
    /// <remarks>
    /// Містить всі реакції на повідомлення (емодзі) для підписаних акаунтів.
    /// </remarks>
    IObservable<ReactionEventArgs> Reaction { get; }

    /// <summary>
    /// Потік подій з вкладеннями.
    /// </summary>
    /// <remarks>
    /// Містить всі повідомлення з вкладеннями для підписаних акаунтів.
    /// </remarks>
    IObservable<AttachmentEventArgs> Attachments { get; }

    /// <summary>
    /// Потік отриманих стікерів.
    /// </summary>
    /// <remarks>
    /// Містить всі повідомлення зі стікерами для підписаних акаунтів.
    /// </remarks>
    IObservable<StickerEventArgs> Sticker { get; }

    /// <summary>
    /// Потік подій набору тексту.
    /// </summary>
    /// <remarks>
    /// Містить сповіщення про те, що користувач набирає текст.
    /// </remarks>
    IObservable<TypingEventArgs> TypingNotifications { get; }

    /// <summary>
    /// Потік квитанцій про доставку та прочитання.
    /// </summary>
    /// <remarks>
    /// Містить інформацію про статус доставки та прочитання повідомлень.
    /// </remarks>
    IObservable<ReceiptEventArgs> Receipts { get; }

    /// <summary>
    /// Потік синхронізаційних подій.
    /// </summary>
    /// <remarks>
    /// Містить події синхронізації між пристроями користувача.
    /// </remarks>
    IObservable<SyncEventArgs> Syncs { get; }
}