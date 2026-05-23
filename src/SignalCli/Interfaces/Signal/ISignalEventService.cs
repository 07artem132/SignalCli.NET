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

    /// <summary>
    /// Потік повідомлень-цитат (DataMessage містить лише <see cref="Models.Signal.JsonQuote"/>
    /// без власного тексту, реакції, стікера чи вкладень).
    /// </summary>
    /// <remarks>F13: раніше такі повідомлення дропалися як "unknown".</remarks>
    IObservable<QuoteEventArgs> Quotes { get; }

    /// <summary>
    /// Потік подій редагування — відправник змінив раніше надіслане повідомлення.
    /// </summary>
    /// <remarks>F13: новий потік для envelope.EditMessage.</remarks>
    IObservable<EditEventArgs> Edits { get; }

    /// <summary>
    /// Потік подій віддаленого видалення повідомлень.
    /// </summary>
    /// <remarks>F13: новий потік для DataMessage.RemoteDelete.</remarks>
    IObservable<RemoteDeleteEventArgs> RemoteDeletes { get; }

    // ===== E (async-stream events) =====
    // IAsyncEnumerable-парні API для кожного потоку подій. Реалізовано поверх
    // System.Threading.Channels.Channel.CreateBounded (capacity=1024, DropOldest).
    //
    // КОНТРАКТ — exclusive consumption: кожен елемент читає РІВНО ОДИН споживач (no fan-out).
    // Для broadcast/fan-out використовуйте парні IObservable<>-властивості вище.
    // При переповненні буфера найстаріший елемент мовчки вижене (DropOldest);
    // факт дропу логується на Debug із лічильником.

    /// <summary>Async-stream-варіант <see cref="TextMessages"/> (exclusive consumption).</summary>
    IAsyncEnumerable<TextMessageEventArgs> TextMessagesAsync(CancellationToken cancellationToken = default);

    /// <summary>Async-stream-варіант <see cref="Reaction"/> (exclusive consumption).</summary>
    IAsyncEnumerable<ReactionEventArgs> ReactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Async-stream-варіант <see cref="Attachments"/> (exclusive consumption).</summary>
    IAsyncEnumerable<AttachmentEventArgs> AttachmentsAsync(CancellationToken cancellationToken = default);

    /// <summary>Async-stream-варіант <see cref="Sticker"/> (exclusive consumption).</summary>
    IAsyncEnumerable<StickerEventArgs> StickerAsync(CancellationToken cancellationToken = default);

    /// <summary>Async-stream-варіант <see cref="TypingNotifications"/> (exclusive consumption).</summary>
    IAsyncEnumerable<TypingEventArgs> TypingAsync(CancellationToken cancellationToken = default);

    /// <summary>Async-stream-варіант <see cref="Receipts"/> (exclusive consumption).</summary>
    IAsyncEnumerable<ReceiptEventArgs> ReceiptsAsync(CancellationToken cancellationToken = default);

    /// <summary>Async-stream-варіант <see cref="Syncs"/> (exclusive consumption).</summary>
    IAsyncEnumerable<SyncEventArgs> SyncsAsync(CancellationToken cancellationToken = default);

    /// <summary>Async-stream-варіант <see cref="Quotes"/> (exclusive consumption).</summary>
    IAsyncEnumerable<QuoteEventArgs> QuotesAsync(CancellationToken cancellationToken = default);

    /// <summary>Async-stream-варіант <see cref="Edits"/> (exclusive consumption).</summary>
    IAsyncEnumerable<EditEventArgs> EditsAsync(CancellationToken cancellationToken = default);

    /// <summary>Async-stream-варіант <see cref="RemoteDeletes"/> (exclusive consumption).</summary>
    IAsyncEnumerable<RemoteDeleteEventArgs> RemoteDeletesAsync(CancellationToken cancellationToken = default);
}