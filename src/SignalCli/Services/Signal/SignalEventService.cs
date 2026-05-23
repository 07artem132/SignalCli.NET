using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SignalCli.Diagnostics;
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
// post-modernize-tuning §8c.1 (audit C5/N17): sealed — інхеріт не підтримується.
internal sealed class SignalEventService(
    ILogger<SignalEventService> logger,
    IJsonRpcClientProvider rpcClientProvider,
    ISignalCliClient signalCliClient)
    : ISignalEventService, IDisposable
{
    private readonly ILogger<SignalEventService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    // post-modernize-tuning §8c.2 (audit C4): _rpcClient field removed — використовуємо
    // _rpcClientProvider.Client напряму при потребі (поточний RPC-клієнт може
    // змінитися після рестарту процесу; кешувати посилання — баг-prone).
    private readonly ISignalCliClient _signalCliClient = signalCliClient ?? throw new ArgumentNullException(nameof(signalCliClient));
    private readonly IJsonRpcClientProvider _rpcClientProvider = rpcClientProvider ?? throw new ArgumentNullException(nameof(rpcClientProvider));

    // Зберігаємо "account -> subscriptionId"
    private readonly Dictionary<string, int> _accountSubscriptions = new();
    // post-modernize-tuning §3.1-3.5 (audit A4): reservation placeholder pattern.
    // Перший виклик SubscribeAsync(account) кладе TCS у _pendingSubscribes; кокурентні
    // виклики бачать той самий TCS і await'ять його замість того, щоб робити власний
    // subscribeReceive-RPC. Це усуває orphan-subscriptions на signal-cli (N RPC при
    // одночасних 10 викликах → завжди 1 RPC), і паралельні викликачі отримують
    // ОДИН і той самий subscriptionId. Захищено тим самим _subscriptionsLock.
    private readonly Dictionary<string, TaskCompletionSource<int>> _pendingSubscribes = new();

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

    // post-modernize-tuning §8c.21 + §11.B.4: drop accounting перенесено повністю
    // на Meter (`signalcli.events.dropped` counter з `event_type` тегом).
    // Приватний _droppedCount field видалено.

    // post-modernize-tuning §2.4: Interlocked.Exchange-based disposal flag.
    private int _disposedFlag;
    private bool _disposed => Volatile.Read(ref _disposedFlag) != 0;

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
    private static void TryWriteOrDrop<T>(Channel<T> channel, T item, string eventType)
    {
        // Якщо канал вже повний — DropOldest вижене найстаріший. Інкрементуємо
        // Meter-counter — drop accounting тепер виходить назовні через OTel.
        if (channel.Reader.Count >= ChannelCapacity)
        {
            SignalCliDiagnostics.EventsDropped.Add(1,
                new KeyValuePair<string, object?>("event_type", eventType));
        }
        channel.Writer.TryWrite(item);
    }

    public async Task<SubscribeReceiveResponse> SubscribeAsync(string account,
        CancellationToken cancellationToken = default)
    {
        // audit N5: вхідні рядки валідуємо типізовано — ArgumentException, не NRE.
        ArgumentException.ThrowIfNullOrEmpty(account);

        // §3.5 (audit C6): rejected-after-dispose throws ObjectDisposedException
        // (типізовано, не NRE через _disposed-перевірки в downstream).
        ObjectDisposedException.ThrowIf(_disposed, this);

        // post-modernize-tuning §11.A.5 (audit N1): subscribe span.
        // signal.subscription.id (int) — не PII; account — НЕ ставимо як тег (PII — номер).
        using var activity = SignalCliDiagnostics.ActivitySource.StartActivity(
            SignalCliDiagnostics.SubscribeActivityName, ActivityKind.Internal);

        // post-modernize-tuning §3.1-3.5 (audit A4) + audit N5: під одним локом
        // вирішуємо ВСЕ — committed, in-flight (placeholder), або стаємо leader-ом.
        TaskCompletionSource<int>? myTcs = null;
        Task<int>? waitOn = null;
        lock (_subscriptionsLock)
        {
            // 1. Уже зареєстрована → ідемпотентно повертаємо існуючий ID без RPC.
            if (_accountSubscriptions.TryGetValue(account, out var existingId))
            {
                SignalEventServiceLog.SubscribeIdempotent(_logger, account, existingId);
                return new SubscribeReceiveResponse(existingId);
            }

            // 2. RPC уже летить — чекаємо на існуючий placeholder, замість дублювати RPC.
            if (_pendingSubscribes.TryGetValue(account, out var existingTcs))
            {
                waitOn = existingTcs.Task;
            }
            else
            {
                // 3. Ми — leader, ставимо placeholder ATOMICALLY.
                myTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingSubscribes[account] = myTcs;
            }
        }

        // Шлях для follower-ів — чекаємо на leader.
        if (waitOn != null)
        {
            var id = await waitOn.WaitAsync(cancellationToken).ConfigureAwait(false);
            SignalEventServiceLog.SubscribeIdempotent(_logger, account, id);
            return new SubscribeReceiveResponse(id);
        }

        // Шлях для leader — робимо RPC, потім commit-ить placeholder.
        try
        {
            var responseToken = await _signalCliClient
                .InvokeMethodAsync<JsonElement, SubscribeReceiveParameters>(
                    "subscribeReceive",
                    new SubscribeReceiveParameters(account),
                    cancellationToken).ConfigureAwait(false);

            int subscriptionId = responseToken.GetInt32();

            lock (_subscriptionsLock)
            {
                _accountSubscriptions[account] = subscriptionId;
                _pendingSubscribes.Remove(account);
            }

            // Будимо всіх follower-ів — вони отримають той самий ID.
            myTcs!.TrySetResult(subscriptionId);

            // §11.A.5: subscriptionId — integer, безпечно як тег; account — НЕ ставимо (PII).
            activity?.SetTag("signal.subscription.id", subscriptionId);
            activity?.SetStatus(ActivityStatusCode.Ok);
            SignalEventServiceLog.Subscribed(_logger, account, subscriptionId);
            return new SubscribeReceiveResponse(subscriptionId);
        }
        catch (Exception ex)
        {
            // §3.2: на помилку RPC — rollback placeholder і прокинути виняток follower-ам теж.
            lock (_subscriptionsLock)
            {
                _pendingSubscribes.Remove(account);
            }
            myTcs!.TrySetException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            throw;
        }
    }

    public async Task<UnsubscribeReceiveResponse> UnsubscribeAsync(int subscriptionId,
        CancellationToken cancellationToken = default)
    {
        // §3.5 (audit C6): typed dispose guard.
        ObjectDisposedException.ThrowIf(_disposed, this);

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
                TryWriteOrDrop(_typingChannel, typingEvent, "typing");
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
                TryWriteOrDrop(_receiptChannel, receiptEvent, "receipt");
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
                TryWriteOrDrop(_syncChannel, syncEvent, "sync");
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
                TryWriteOrDrop(_editChannel, editEvent, "edit");
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
                    TryWriteOrDrop(_textChannel, textEvent, "text");
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
                    TryWriteOrDrop(_reactionChannel, reactionEvent, "reaction");
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
                    TryWriteOrDrop(_stickerChannel, stickerEvent, "sticker");
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
                    TryWriteOrDrop(_attachmentChannel, attachmentEvent, "attachment");
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
                    TryWriteOrDrop(_remoteDeleteChannel, rd, "remote_delete");
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
                    TryWriteOrDrop(_quoteChannel, qe, "quote");
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
        if (Interlocked.Exchange(ref _disposedFlag, 1) != 0) return;

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

        // §8c.2: читаємо .Client раз тут (вже після того як rpc-провайдер ініціалізував його
        // в порядку hosted-service startup), але НЕ зберігаємо у полі — клієнт міг бути
        // disposнутим до Dispose() через рестарт процесу.
        _notificationSubscription = _rpcClientProvider.Client.Notifications.Subscribe(OnNotificationReceived);
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