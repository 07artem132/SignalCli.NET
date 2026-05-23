using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.Rpc;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Logging;
using SignalCli.Models.Rpc;
using SignalCli.Models.Signal;
using SignalCli.Models.Signal.Events;

namespace SignalCli.Services.Signal;

/// <summary>
/// Внутрішня реалізація <see cref="ISignalEventService"/>: розбирає RPC-нотифікації
/// signal-cli й роздає їх двома паралельними поверхнями — Rx <see cref="IObservable{T}"/>
/// (broadcast/fan-out) та <see cref="IAsyncEnumerable{T}"/> поверх bounded-каналів
/// (exclusive consumption, back-pressure через DropOldest).
/// </summary>
/// <remarks>
/// <para>
/// <b>SingleWriter invariant (audit N14).</b> Усі канали створюються з
/// <c>BoundedChannelOptions.SingleWriter = true</c>. Це валідно ЛИШЕ доки
/// <c>OnNotificationReceived</c> викликається ЛИШЕ з одного потоку RPC-нотифікацій
/// (один <c>_rpcClient.Notifications.Subscribe(…)</c> у <c>StartAsync</c>).
/// Якщо колись додасться другий писач (наприклад, multi-RPC fan-in або повторний
/// <c>Subscribe</c> без диспозу попереднього) — <c>ChannelOptions.SingleWriter</c>
/// має змінитися на <c>false</c>, інакше поведінка — undefined per
/// <see href="https://learn.microsoft.com/dotnet/api/system.threading.channels.channeloptions.singlewriter">ChannelOptions.SingleWriter</see>.
/// Підтримуємо інваріант через ідемпотентний <c>StartAsync</c> (Interlocked.Exchange
/// + Dispose попередньої підписки) — другий-одночасний писач неможливий.
/// </para>
/// </remarks>
internal class SignalEventService(
    ILogger<SignalEventService> logger,
    IJsonRpcClientProvider rpcClientProvider,
    ISignalCliClient signalCliClient)
    : ISignalEventService, IDisposable
{
    private readonly ILogger<SignalEventService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private  IJsonRpcClient? _rpcClient;
    private readonly ISignalCliClient _signalCliClient = signalCliClient ?? throw new ArgumentNullException(nameof(signalCliClient));
    private readonly IJsonRpcClientProvider _rpcClientProvider = rpcClientProvider ?? throw new ArgumentNullException(nameof(rpcClientProvider));

    // Зберігаємо "account -> subscriptionId"
    private readonly Dictionary<string, int> _accountSubscriptions = new();

    // C# 13 / .NET 9+: окремий System.Threading.Lock замість блокування на самому словнику
    // (не блокуємося на структурі даних, яку захищаємо — див. IDE0330).
    private readonly System.Threading.Lock _subscriptionsLock = new();

    // Підписка на потік нотифікацій JSON-RPC; звільняється у Dispose.
    private IDisposable? _notificationSubscription;

    // Потоки подій для різних типів сповіщень
    private readonly Subject<TextMessageEventArgs> _textMessages = new();
    private readonly Subject<ReactionEventArgs> _reaction = new();
    private readonly Subject<AttachmentEventArgs> _attachments = new();
    private readonly Subject<StickerEventArgs> _sticker = new();
    private readonly Subject<TypingEventArgs> _typing = new();
    private readonly Subject<ReceiptEventArgs> _receipts = new();
    private readonly Subject<SyncEventArgs> _syncs = new();
    // F13: окремі потоки для Quote/Edit/RemoteDelete — раніше дропалися як "unknown".
    private readonly Subject<QuoteEventArgs> _quotes = new();
    private readonly Subject<EditEventArgs> _edits = new();
    private readonly Subject<RemoteDeleteEventArgs> _remoteDeletes = new();

    // E (async-stream events): bounded channels, парні до Subject-ів.
    // DropOldest на переповненні + лічильник дропів (Debug-лог).
    private const int ChannelCapacity = 1024;
    private static BoundedChannelOptions ChannelOptionsTemplate() => new(ChannelCapacity)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = false,
        SingleWriter = true,
    };
    private readonly Channel<TextMessageEventArgs> _textChannel = Channel.CreateBounded<TextMessageEventArgs>(ChannelOptionsTemplate());
    private readonly Channel<ReactionEventArgs> _reactionChannel = Channel.CreateBounded<ReactionEventArgs>(ChannelOptionsTemplate());
    private readonly Channel<AttachmentEventArgs> _attachmentChannel = Channel.CreateBounded<AttachmentEventArgs>(ChannelOptionsTemplate());
    private readonly Channel<StickerEventArgs> _stickerChannel = Channel.CreateBounded<StickerEventArgs>(ChannelOptionsTemplate());
    private readonly Channel<TypingEventArgs> _typingChannel = Channel.CreateBounded<TypingEventArgs>(ChannelOptionsTemplate());
    private readonly Channel<ReceiptEventArgs> _receiptChannel = Channel.CreateBounded<ReceiptEventArgs>(ChannelOptionsTemplate());
    private readonly Channel<SyncEventArgs> _syncChannel = Channel.CreateBounded<SyncEventArgs>(ChannelOptionsTemplate());
    private readonly Channel<QuoteEventArgs> _quoteChannel = Channel.CreateBounded<QuoteEventArgs>(ChannelOptionsTemplate());
    private readonly Channel<EditEventArgs> _editChannel = Channel.CreateBounded<EditEventArgs>(ChannelOptionsTemplate());
    private readonly Channel<RemoteDeleteEventArgs> _remoteDeleteChannel = Channel.CreateBounded<RemoteDeleteEventArgs>(ChannelOptionsTemplate());

    // Сумарний лічильник «дропів через переповнення». Періодично логується на Debug
    // у TryWrite (раз на 100 дропів — щоб не спамити).
    private long _droppedCount;

    private bool _disposed;

    // AsObservable() приховує Subject: споживач не може зробити downcast і самостійно
    // викликати OnNext/OnError/OnCompleted на наших потоках подій.
    public IObservable<TextMessageEventArgs> TextMessages => _textMessages.AsObservable();

    public IObservable<ReactionEventArgs> Reaction => _reaction.AsObservable();

    public IObservable<AttachmentEventArgs> Attachments => _attachments.AsObservable();

    public IObservable<StickerEventArgs> Sticker => _sticker.AsObservable();

    public IObservable<TypingEventArgs> TypingNotifications => _typing.AsObservable();

    public IObservable<ReceiptEventArgs> Receipts => _receipts.AsObservable();

    public IObservable<SyncEventArgs> Syncs => _syncs.AsObservable();

    public IObservable<QuoteEventArgs> Quotes => _quotes.AsObservable();

    public IObservable<EditEventArgs> Edits => _edits.AsObservable();

    public IObservable<RemoteDeleteEventArgs> RemoteDeletes => _remoteDeletes.AsObservable();

    // ===== E (async-stream API) =====

    /// <inheritdoc />
    public IAsyncEnumerable<TextMessageEventArgs> TextMessagesAsync(CancellationToken cancellationToken = default)
        => _textChannel.Reader.ReadAllAsync(cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<ReactionEventArgs> ReactionAsync(CancellationToken cancellationToken = default)
        => _reactionChannel.Reader.ReadAllAsync(cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<AttachmentEventArgs> AttachmentsAsync(CancellationToken cancellationToken = default)
        => _attachmentChannel.Reader.ReadAllAsync(cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<StickerEventArgs> StickerAsync(CancellationToken cancellationToken = default)
        => _stickerChannel.Reader.ReadAllAsync(cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<TypingEventArgs> TypingAsync(CancellationToken cancellationToken = default)
        => _typingChannel.Reader.ReadAllAsync(cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<ReceiptEventArgs> ReceiptsAsync(CancellationToken cancellationToken = default)
        => _receiptChannel.Reader.ReadAllAsync(cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<SyncEventArgs> SyncsAsync(CancellationToken cancellationToken = default)
        => _syncChannel.Reader.ReadAllAsync(cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<QuoteEventArgs> QuotesAsync(CancellationToken cancellationToken = default)
        => _quoteChannel.Reader.ReadAllAsync(cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<EditEventArgs> EditsAsync(CancellationToken cancellationToken = default)
        => _editChannel.Reader.ReadAllAsync(cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<RemoteDeleteEventArgs> RemoteDeletesAsync(CancellationToken cancellationToken = default)
        => _remoteDeleteChannel.Reader.ReadAllAsync(cancellationToken);

    /// <summary>
    /// E (async-stream): записує елемент у канал; у DropOldest-режимі TryWrite завжди
    /// успішне, але якщо канал перед записом був повним — найстаріший елемент вижений.
    /// Інкрементує лічильник і періодично логує на Debug для діагностики backpressure.
    /// </summary>
    private void TryWriteOrDrop<T>(Channel<T> channel, T item)
    {
        // Якщо канал вже повний — DropOldest вижене найстаріший. Лічимо це як drop.
        if (channel.Reader.Count >= ChannelCapacity)
        {
            var dropped = Interlocked.Increment(ref _droppedCount);
            if (dropped % 100 == 1)
            {
                SignalEventServiceLog.ChannelOverflowed(_logger, typeof(T).Name, dropped);
            }
        }
        channel.Writer.TryWrite(item);
    }

    public async Task<SubscribeReceiveResponse> SubscribeAsync(string account,
        CancellationToken cancellationToken = default)
    {
        // audit N5: вхідні рядки валідуємо типізовано — ArgumentException, не NRE.
        ArgumentException.ThrowIfNullOrEmpty(account);

        // audit N5: ідемпотентність — повторний виклик для того самого облікового запису
        // повертає існуючий subscriptionId замість того, щоб кидати локалізаційно-крихкий
        // InvalidOperationException. Це усуває потребу в `catch(IOE) when (msg.Contains(...))`
        // у викликачів і робить SubscribeAsync безпечним для повторного виклику —
        // одна з ключових agent-friendly характеристик (Microsoft *Idempotency*).
        lock (_subscriptionsLock)
        {
            if (_accountSubscriptions.TryGetValue(account, out var existingId))
            {
                SignalEventServiceLog.SubscribeIdempotent(_logger, account, existingId);
                return new SubscribeReceiveResponse(existingId);
            }
        }

        var responseToken = await _signalCliClient
            .InvokeMethodAsync<JsonElement, SubscribeReceiveParameters>(
                "subscribeReceive",
                new SubscribeReceiveParameters(account),
                cancellationToken).ConfigureAwait(false);

        int subscriptionId = responseToken.GetInt32();

        lock (_subscriptionsLock)
        {
            // Гонка: інший виклик міг вступити за час RPC. Поважаємо існуюче й
            // не перетираємо — повертаємо саме той ID, який зараз у мапі.
            if (_accountSubscriptions.TryGetValue(account, out var raceWinnerId))
            {
                SignalEventServiceLog.SubscribeIdempotent(_logger, account, raceWinnerId);
                return new SubscribeReceiveResponse(raceWinnerId);
            }

            _accountSubscriptions[account] = subscriptionId;
        }

        SignalEventServiceLog.Subscribed(_logger, account, subscriptionId);

        return new SubscribeReceiveResponse(subscriptionId);
    }

    public async Task<UnsubscribeReceiveResponse> UnsubscribeAsync(int subscriptionId,
        CancellationToken cancellationToken = default)
    {
        string? account;
        lock (_subscriptionsLock)
        {
            account = _accountSubscriptions.FirstOrDefault(x => x.Value == subscriptionId).Key;
        }

        if (account == null)
        {
            SignalEventServiceLog.UnsubscribeMissing(_logger, subscriptionId);
            return new UnsubscribeReceiveResponse();
        }

        var resp = await _signalCliClient
            .InvokeMethodAsync<UnsubscribeReceiveResponse, UnsubscribeReceiveParameters>(
                "unsubscribeReceive",
                new UnsubscribeReceiveParameters(subscriptionId),
                cancellationToken).ConfigureAwait(false);

        lock (_subscriptionsLock)
        {
            _accountSubscriptions.Remove(account);
        }

        SignalEventServiceLog.Unsubscribed(_logger, account, subscriptionId);

        return resp;
    }

    /// <summary>
    /// Обробка вхідного сповіщення від JSON-RPC.
    /// Використовує новий DTO JsonMessageEnvelope для уніфікованої обробки.
    /// </summary>
    /// <param name="notification">Сповіщення для обробки.</param>
    private void OnNotificationReceived(JsonRpcNotification<SubscriptionEventArgs> notification)
    {
        try
        {
            int subscriptionId = notification.Params.Subscription;
            var eventArgs = notification.Params.Result;
            // Припускається, що eventArgs.Envelope вже має тип JsonMessageEnvelope
            JsonMessageEnvelope? jsonEnvelope = eventArgs.Envelope;
            if (jsonEnvelope == null)
            {
                SignalEventServiceLog.EnvelopeMissing(_logger);
                return;
            }

            if (!TryGetAccountBySubscriptionId(subscriptionId, out string? account))
            {
                SignalEventServiceLog.StaleSubscription(_logger, subscriptionId);
                return;
            }

            // A.11: structured-scope, щоб усі логи цієї нотифікації несли SubscriptionId/Account
            // як structured properties (а не повторювалися в шаблонах кожного повідомлення).
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["SubscriptionId"] = subscriptionId,
                ["Account"] = account!,
            });

            // Якщо отримано подію набору тексту
            if (jsonEnvelope.TypingMessage is not null)
            {
                var typingEvent = new TypingEventArgs(
                    subscriptionId,
                    account,
                    jsonEnvelope.TypingMessage,
                    jsonEnvelope.Source,
                    jsonEnvelope.SourceNumber,
                    jsonEnvelope.SourceUuid,
                    jsonEnvelope.SourceName,
                    jsonEnvelope.SourceDevice,
                    jsonEnvelope.Timestamp,
                    jsonEnvelope.ServerReceivedTimestamp,
                    jsonEnvelope.ServerDeliveredTimestamp);
                _typing.OnNext(typingEvent);
                TryWriteOrDrop(_typingChannel, typingEvent);
                return;
            }

            // Якщо отримано квитанцію
            if (jsonEnvelope.ReceiptMessage is not null)
            {
                var receiptEvent = new ReceiptEventArgs(
                    subscriptionId,
                    account,
                    jsonEnvelope.ReceiptMessage,
                    jsonEnvelope.Source,
                    jsonEnvelope.SourceNumber,
                    jsonEnvelope.SourceUuid,
                    jsonEnvelope.SourceName,
                    jsonEnvelope.SourceDevice,
                    jsonEnvelope.Timestamp,
                    jsonEnvelope.ServerReceivedTimestamp,
                    jsonEnvelope.ServerDeliveredTimestamp);
                _receipts.OnNext(receiptEvent);
                TryWriteOrDrop(_receiptChannel, receiptEvent);
                return;
            }

            // Якщо отримано подію синхронізації
            if (jsonEnvelope.SyncMessage is not null)
            {
                var syncEvent = new SyncEventArgs(
                    subscriptionId,
                    account,
                    jsonEnvelope.SyncMessage,
                    jsonEnvelope.Source,
                    jsonEnvelope.SourceNumber,
                    jsonEnvelope.SourceUuid,
                    jsonEnvelope.SourceName,
                    jsonEnvelope.SourceDevice,
                    jsonEnvelope.Timestamp,
                    jsonEnvelope.ServerReceivedTimestamp,
                    jsonEnvelope.ServerDeliveredTimestamp);
                _syncs.OnNext(syncEvent);
                TryWriteOrDrop(_syncChannel, syncEvent);
                return;
            }

            // F13: подія редагування — на рівні конверта окремо від DataMessage.
            if (jsonEnvelope.EditMessage is not null)
            {
                var editEvent = new EditEventArgs(
                    subscriptionId,
                    account,
                    jsonEnvelope.EditMessage,
                    jsonEnvelope.Source,
                    jsonEnvelope.SourceNumber,
                    jsonEnvelope.SourceUuid,
                    jsonEnvelope.SourceName,
                    jsonEnvelope.SourceDevice,
                    jsonEnvelope.Timestamp,
                    jsonEnvelope.ServerReceivedTimestamp,
                    jsonEnvelope.ServerDeliveredTimestamp);
                _edits.OnNext(editEvent);
                TryWriteOrDrop(_editChannel, editEvent);
                return;
            }

            // Якщо отримано подію, що містить дані повідомлення.
            // Одне повідомлення може одночасно містити кілька payload'ів
            // (наприклад, текст-підпис + вкладення), тому перевіряємо їх НЕЗАЛЕЖНО,
            // без раннього return, щоб піднялися всі відповідні події.
            if (jsonEnvelope.DataMessage is not null)
            {
                var data = jsonEnvelope.DataMessage;
                var emitted = false;
                // Якщо задано текст повідомлення, формуємо подію текстового повідомлення
                if (!string.IsNullOrEmpty(data.Message))
                {
                    var textEvent = new TextMessageEventArgs(
                        subscriptionId,
                        account,
                        data,
                        jsonEnvelope.Source,
                        jsonEnvelope.SourceNumber,
                        jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName,
                        jsonEnvelope.SourceDevice,
                        jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp,
                        jsonEnvelope.ServerDeliveredTimestamp);
                    _textMessages.OnNext(textEvent);
                    TryWriteOrDrop(_textChannel, textEvent);
                    emitted = true;
                }

                // Якщо задано реакцію, передаємо повний об'єкт реакції
                if (data.Reaction is not null)
                {
                    var reactionEvent = new ReactionEventArgs(
                        subscriptionId,
                        account,
                        data.Reaction,
                        jsonEnvelope.Source,
                        jsonEnvelope.SourceNumber,
                        jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName,
                        jsonEnvelope.SourceDevice,
                        jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp,
                        jsonEnvelope.ServerDeliveredTimestamp);

                    _reaction.OnNext(reactionEvent);
                    TryWriteOrDrop(_reactionChannel, reactionEvent);
                    emitted = true;
                }

                // Якщо задано стікер, передаємо його дані
                if (data.Sticker is not null)
                {
                    var stickerEvent = new StickerEventArgs(
                        subscriptionId,
                        account,
                        data.Sticker,
                        jsonEnvelope.Source,
                        jsonEnvelope.SourceNumber,
                        jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName,
                        jsonEnvelope.SourceDevice,
                        jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp,
                        jsonEnvelope.ServerDeliveredTimestamp);
                    _sticker.OnNext(stickerEvent);
                    TryWriteOrDrop(_stickerChannel, stickerEvent);
                    emitted = true;
                }

                // Якщо задано вкладення, передаємо повний список вкладень
                if (data.Attachments is not null && data.Attachments.Count > 0)
                {
                    var attachmentEvent = new AttachmentEventArgs(
                        subscriptionId,
                        account,
                        data.Attachments,
                        jsonEnvelope.Source,
                        jsonEnvelope.SourceNumber,
                        jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName,
                        jsonEnvelope.SourceDevice,
                        jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp,
                        jsonEnvelope.ServerDeliveredTimestamp);
                    _attachments.OnNext(attachmentEvent);
                    TryWriteOrDrop(_attachmentChannel, attachmentEvent);
                    emitted = true;
                }

                // F13: RemoteDelete — окрема подія (відправник прибрав повідомлення в одержувача).
                if (data.RemoteDelete is not null)
                {
                    var rd = new RemoteDeleteEventArgs(
                        subscriptionId,
                        account,
                        data.RemoteDelete,
                        jsonEnvelope.Source,
                        jsonEnvelope.SourceNumber,
                        jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName,
                        jsonEnvelope.SourceDevice,
                        jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp,
                        jsonEnvelope.ServerDeliveredTimestamp);
                    _remoteDeletes.OnNext(rd);
                    TryWriteOrDrop(_remoteDeleteChannel, rd);
                    emitted = true;
                }

                // F13: Quote-only — DataMessage без тіла/реакції/стікера/вкладень, але з Quote.
                // Без цієї гілки повідомлення «відповідь без власного тексту» (рідко, але буває)
                // мовчки губилося як "unknown".
                if (!emitted && data.Quote is not null)
                {
                    var qe = new QuoteEventArgs(
                        subscriptionId,
                        account,
                        data,
                        jsonEnvelope.Source,
                        jsonEnvelope.SourceNumber,
                        jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName,
                        jsonEnvelope.SourceDevice,
                        jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp,
                        jsonEnvelope.ServerDeliveredTimestamp);
                    _quotes.OnNext(qe);
                    TryWriteOrDrop(_quoteChannel, qe);
                    emitted = true;
                }

                if (!emitted)
                    SignalEventServiceLog.DataMessageEmpty(_logger);
                return;
            }

            SignalEventServiceLog.UnknownEnvelope(_logger);
        }
        // Навмисний широкий catch: межа диспетчера сповіщень — одне погане
        // сповіщення не повинно зривати потік подій (логуємо й продовжуємо).
        catch (Exception ex)
        {
            SignalEventServiceLog.NotificationDispatchFailed(_logger, ex);
        }
    }

    /// <summary>
    /// Знаходить обліковий запис за ідентифікатором підписки.
    /// </summary>
    /// <param name="subscriptionId">Ідентифікатор підписки.</param>
    /// <param name="account">Знайдений обліковий запис або null, якщо підписка не існує.</param>
    /// <returns>true, якщо підписка знайдена; інакше - false.</returns>
    private bool TryGetAccountBySubscriptionId(int subscriptionId, out string? account)
    {
        lock (_subscriptionsLock)
        {
            foreach (var kv in _accountSubscriptions)
            {
                if (kv.Value == subscriptionId)
                {
                    account = kv.Key;
                    return true;
                }
            }
        }

        account = null;
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Спершу припиняємо приймати нові нотифікації, потім завершуємо потоки подій.
        _notificationSubscription?.Dispose();
        _notificationSubscription = null;

        // E (async-stream): спершу закриваємо канали — будь-який активний `await foreach`
        // дочитає буфер і завершиться нормально без винятків.
        _textChannel.Writer.TryComplete();
        _reactionChannel.Writer.TryComplete();
        _attachmentChannel.Writer.TryComplete();
        _stickerChannel.Writer.TryComplete();
        _typingChannel.Writer.TryComplete();
        _receiptChannel.Writer.TryComplete();
        _syncChannel.Writer.TryComplete();
        _quoteChannel.Writer.TryComplete();
        _editChannel.Writer.TryComplete();
        _remoteDeleteChannel.Writer.TryComplete();

        // F17 (H.17): OnCompleted ПЛЮС Dispose — раніше Subject не диспоузувся
        // (раніше теж так було, але якщо StartAsync викликався двічі — _notificationSubscription
        // підмінювалася, і попередня губилась; нижче в StartAsync це теж виправлено).
        _textMessages.OnCompleted(); _textMessages.Dispose();
        _reaction.OnCompleted(); _reaction.Dispose();
        _attachments.OnCompleted(); _attachments.Dispose();
        _sticker.OnCompleted(); _sticker.Dispose();
        _typing.OnCompleted(); _typing.Dispose();
        _receipts.OnCompleted(); _receipts.Dispose();
        _syncs.OnCompleted(); _syncs.Dispose();
        _quotes.OnCompleted(); _quotes.Dispose();
        _edits.OnCompleted(); _edits.Dispose();
        _remoteDeletes.OnCompleted(); _remoteDeletes.Dispose();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // F17 (H.17): ідемпотентність — другий StartAsync не повинен «згубити» попередню підписку.
        // Диспоузимо стару, якщо була, і встановлюємо нову атомарно.
        var oldSub = Interlocked.Exchange(ref _notificationSubscription, null);
        oldSub?.Dispose();

        _rpcClient = _rpcClientProvider.Client;
        _notificationSubscription = _rpcClient.Notifications.Subscribe(OnNotificationReceived);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Симетрично StartAsync: відписуємось від нотифікацій, щоб повторний Start був чистим.
        var sub = Interlocked.Exchange(ref _notificationSubscription, null);
        sub?.Dispose();
        return Task.CompletedTask;
    }
}