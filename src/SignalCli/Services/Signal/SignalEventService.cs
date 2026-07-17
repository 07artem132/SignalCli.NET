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
using SignalCli.Serialization;

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
    // signal-cli-api-coverage Wave 7b: 7 нових event streams.
    private readonly Subject<PollCreateEventArgs> _pollCreates = new();
    private readonly Subject<PollVoteEventArgs> _pollVotes = new();
    private readonly Subject<PollTerminateEventArgs> _pollTerminates = new();
    private readonly Subject<PaymentEventArgs> _payments = new();
    private readonly Subject<PinMessageEventArgs> _pinMessages = new();
    private readonly Subject<UnpinMessageEventArgs> _unpinMessages = new();
    private readonly Subject<AdminDeleteEventArgs> _adminDeletes = new();

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
    // Wave 7b channels.
    private readonly Channel<PollCreateEventArgs> _pollCreateChannel = Channel.CreateBounded<PollCreateEventArgs>(ChannelOptionsTemplate());
    private readonly Channel<PollVoteEventArgs> _pollVoteChannel = Channel.CreateBounded<PollVoteEventArgs>(ChannelOptionsTemplate());
    private readonly Channel<PollTerminateEventArgs> _pollTerminateChannel = Channel.CreateBounded<PollTerminateEventArgs>(ChannelOptionsTemplate());
    private readonly Channel<PaymentEventArgs> _paymentChannel = Channel.CreateBounded<PaymentEventArgs>(ChannelOptionsTemplate());
    private readonly Channel<PinMessageEventArgs> _pinMessageChannel = Channel.CreateBounded<PinMessageEventArgs>(ChannelOptionsTemplate());
    private readonly Channel<UnpinMessageEventArgs> _unpinMessageChannel = Channel.CreateBounded<UnpinMessageEventArgs>(ChannelOptionsTemplate());
    private readonly Channel<AdminDeleteEventArgs> _adminDeleteChannel = Channel.CreateBounded<AdminDeleteEventArgs>(ChannelOptionsTemplate());

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
    // Wave 7b IObservable properties.
    /// <inheritdoc/>
    public IObservable<PollCreateEventArgs> PollCreates => _pollCreates.AsObservable();
    /// <inheritdoc/>
    public IObservable<PollVoteEventArgs> PollVotes => _pollVotes.AsObservable();
    /// <inheritdoc/>
    public IObservable<PollTerminateEventArgs> PollTerminates => _pollTerminates.AsObservable();
    /// <inheritdoc/>
    public IObservable<PaymentEventArgs> Payments => _payments.AsObservable();
    /// <inheritdoc/>
    public IObservable<PinMessageEventArgs> PinMessages => _pinMessages.AsObservable();
    /// <inheritdoc/>
    public IObservable<UnpinMessageEventArgs> UnpinMessages => _unpinMessages.AsObservable();
    /// <inheritdoc/>
    public IObservable<AdminDeleteEventArgs> AdminDeletes => _adminDeletes.AsObservable();

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

    // Wave 7b IAsyncEnumerable methods.
    /// <inheritdoc/>
    public IAsyncEnumerable<PollCreateEventArgs> PollCreatesAsync(CancellationToken cancellationToken = default)
        => _pollCreateChannel.Reader.ReadAllAsync(cancellationToken);
    /// <inheritdoc/>
    public IAsyncEnumerable<PollVoteEventArgs> PollVotesAsync(CancellationToken cancellationToken = default)
        => _pollVoteChannel.Reader.ReadAllAsync(cancellationToken);
    /// <inheritdoc/>
    public IAsyncEnumerable<PollTerminateEventArgs> PollTerminatesAsync(CancellationToken cancellationToken = default)
        => _pollTerminateChannel.Reader.ReadAllAsync(cancellationToken);
    /// <inheritdoc/>
    public IAsyncEnumerable<PaymentEventArgs> PaymentsAsync(CancellationToken cancellationToken = default)
        => _paymentChannel.Reader.ReadAllAsync(cancellationToken);
    /// <inheritdoc/>
    public IAsyncEnumerable<PinMessageEventArgs> PinMessagesAsync(CancellationToken cancellationToken = default)
        => _pinMessageChannel.Reader.ReadAllAsync(cancellationToken);
    /// <inheritdoc/>
    public IAsyncEnumerable<UnpinMessageEventArgs> UnpinMessagesAsync(CancellationToken cancellationToken = default)
        => _unpinMessageChannel.Reader.ReadAllAsync(cancellationToken);
    /// <inheritdoc/>
    public IAsyncEnumerable<AdminDeleteEventArgs> AdminDeletesAsync(CancellationToken cancellationToken = default)
        => _adminDeleteChannel.Reader.ReadAllAsync(cancellationToken);

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

    /// <summary>
    /// api-coverage-audit-followup §3: Generic helper що стискує 13 near-identical
    /// presence-based union dispatch branches у <c>OnNotificationReceived</c> до one-liners.
    /// </summary>
    /// <remarks>
    /// PRESENCE-BASED UNION (CLAUDE.md rule #4): null-присутність payload'у означає "не emit'имо".
    /// Помилкова реструктуризація — early-return на per-branch level — заборонена; усі 9 inner-DataMessage
    /// branches MUST allow concurrent emission. Цей хелпер повертає bool (чи emit'нули), call-site
    /// аґреґує через <c>emitted |= ...</c>. Top-level envelope-branches (Typing/Receipt/Sync/Edit) —
    /// 4 mutually-exclusive чейн з <c>if (Dispatch(...)) return;</c>.
    /// </remarks>
    private static bool DispatchUnionMember<TPayload, TArgs>(
        TPayload? payload,
        Func<TPayload, TArgs> makeArgs,
        Subject<TArgs> subject,
        Channel<TArgs> channel,
        string kindLabel)
        where TPayload : class
    {
        if (payload is null) return false;
        var args = makeArgs(payload);
        subject.OnNext(args);
        TryWriteOrDrop(channel, args, kindLabel);
        return true;
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
                .InvokeMethodAsync(
                    "subscribeReceive",
                    new SubscribeReceiveParameters(account),
                    SignalJsonContext.Default.SubscribeReceiveParameters,
                    SignalJsonContext.Default.JsonElement,
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
            .InvokeMethodAsync(
                "unsubscribeReceive",
                new UnsubscribeReceiveParameters(subscriptionId),
                SignalJsonContext.Default.UnsubscribeReceiveParameters,
                SignalJsonContext.Default.UnsubscribeReceiveResponse,
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
            // Захист (#2059/on-start): signal-cli може віддати receive-нотіфікацію без корисного
            // навантаження — params або params.result приходять null (напр. sealed-sender конверт
            // без serverGuid, який upstream не зміг обробити до фіксу signal-cli 0.14.5). Гардимо
            // до розіменування, щоб не впасти в NRE і не спамити лог; лог-і-скіп.
            var payload = notification.Params;
            if (payload is null || payload.Result is null)
            {
                SignalEventServiceLog.NotificationPayloadMissing(_logger);
                return;
            }

            int subscriptionId = payload.Subscription;
            var eventArgs = payload.Result;
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
                ["Account"] = account,
            });

            // 4 top-level mutually-exclusive envelope branches — early-return after dispatch.
            if (DispatchUnionMember(jsonEnvelope.TypingMessage,
                    t => new TypingEventArgs(subscriptionId, account, t,
                        jsonEnvelope.Source, jsonEnvelope.SourceNumber, jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName, jsonEnvelope.SourceDevice, jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp, jsonEnvelope.ServerDeliveredTimestamp),
                    _typing, _typingChannel, "typing")) return;

            if (DispatchUnionMember(jsonEnvelope.ReceiptMessage,
                    r => new ReceiptEventArgs(subscriptionId, account, r,
                        jsonEnvelope.Source, jsonEnvelope.SourceNumber, jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName, jsonEnvelope.SourceDevice, jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp, jsonEnvelope.ServerDeliveredTimestamp),
                    _receipts, _receiptChannel, "receipt")) return;

            if (DispatchUnionMember(jsonEnvelope.SyncMessage,
                    s => new SyncEventArgs(subscriptionId, account, s,
                        jsonEnvelope.Source, jsonEnvelope.SourceNumber, jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName, jsonEnvelope.SourceDevice, jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp, jsonEnvelope.ServerDeliveredTimestamp),
                    _syncs, _syncChannel, "sync")) return;

            // F13: подія редагування — на рівні конверта окремо від DataMessage.
            if (DispatchUnionMember(jsonEnvelope.EditMessage,
                    e => new EditEventArgs(subscriptionId, account, e,
                        jsonEnvelope.Source, jsonEnvelope.SourceNumber, jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName, jsonEnvelope.SourceDevice, jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp, jsonEnvelope.ServerDeliveredTimestamp),
                    _edits, _editChannel, "edit")) return;

            // Якщо отримано подію, що містить дані повідомлення.
            // Одне повідомлення може одночасно містити кілька payload'ів
            // (наприклад, текст-підпис + вкладення), тому перевіряємо їх НЕЗАЛЕЖНО,
            // без раннього return, щоб піднялися всі відповідні події.
            if (jsonEnvelope.DataMessage is not null)
            {
                var data = jsonEnvelope.DataMessage;
                var emitted = false;

                // PRESENCE-BASED UNION (CLAUDE.md rule #4) — usі 13 inner-branches checked
                // independently через `emitted |= Dispatch(...)`; кілька payload'ів можуть
                // бути одночасно (наприклад text + attachment + quote). Quote — special-cased:
                // повертається у presence-list лише якщо НІЧОГО ще не emit'нули (quote-only fallback).

                emitted |= DispatchUnionMember(
                    string.IsNullOrEmpty(data.Message) ? null : data.Message,
                    _ => new TextMessageEventArgs(subscriptionId, account, data,
                        jsonEnvelope.Source, jsonEnvelope.SourceNumber, jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName, jsonEnvelope.SourceDevice, jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp, jsonEnvelope.ServerDeliveredTimestamp),
                    _textMessages, _textChannel, "text");

                emitted |= DispatchUnionMember(data.Reaction,
                    r => new ReactionEventArgs(subscriptionId, account, r,
                        jsonEnvelope.Source, jsonEnvelope.SourceNumber, jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName, jsonEnvelope.SourceDevice, jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp, jsonEnvelope.ServerDeliveredTimestamp),
                    _reaction, _reactionChannel, "reaction");

                emitted |= DispatchUnionMember(data.Sticker,
                    s => new StickerEventArgs(subscriptionId, account, s,
                        jsonEnvelope.Source, jsonEnvelope.SourceNumber, jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName, jsonEnvelope.SourceDevice, jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp, jsonEnvelope.ServerDeliveredTimestamp),
                    _sticker, _stickerChannel, "sticker");

                emitted |= DispatchUnionMember(
                    data.Attachments is { Count: > 0 } ? data.Attachments : null,
                    a => new AttachmentEventArgs(subscriptionId, account, a,
                        jsonEnvelope.Source, jsonEnvelope.SourceNumber, jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName, jsonEnvelope.SourceDevice, jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp, jsonEnvelope.ServerDeliveredTimestamp),
                    _attachments, _attachmentChannel, "attachment");

                // F13: RemoteDelete — окрема подія (відправник прибрав повідомлення в одержувача).
                emitted |= DispatchUnionMember(data.RemoteDelete,
                    rd => new RemoteDeleteEventArgs(subscriptionId, account, rd,
                        jsonEnvelope.Source, jsonEnvelope.SourceNumber, jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName, jsonEnvelope.SourceDevice, jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp, jsonEnvelope.ServerDeliveredTimestamp),
                    _remoteDeletes, _remoteDeleteChannel, "remote_delete");

                // F13: Quote-only fallback — emit ТІЛЬКИ якщо нічого вище не emit'нуло
                // (інакше Quote приходить parasitically як supplemental на текст/реакцію).
                // Без цієї гілки повідомлення «відповідь без власного тексту» (рідко) губиться як "unknown".
                if (!emitted)
                    emitted |= DispatchUnionMember(data.Quote,
                        _ => new QuoteEventArgs(subscriptionId, account, data,
                            jsonEnvelope.Source, jsonEnvelope.SourceNumber, jsonEnvelope.SourceUuid,
                            jsonEnvelope.SourceName, jsonEnvelope.SourceDevice, jsonEnvelope.Timestamp,
                            jsonEnvelope.ServerReceivedTimestamp, jsonEnvelope.ServerDeliveredTimestamp),
                        _quotes, _quoteChannel, "quote");

                // ===== Wave 7b: 7 нових presence-based union branches =====

                emitted |= DispatchUnionMember(data.PollCreate,
                    pc => new PollCreateEventArgs(subscriptionId, account, pc,
                        jsonEnvelope.Source, jsonEnvelope.SourceNumber, jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName, jsonEnvelope.SourceDevice, jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp, jsonEnvelope.ServerDeliveredTimestamp),
                    _pollCreates, _pollCreateChannel, "poll_create");

                emitted |= DispatchUnionMember(data.PollVote,
                    pv => new PollVoteEventArgs(subscriptionId, account, pv,
                        jsonEnvelope.Source, jsonEnvelope.SourceNumber, jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName, jsonEnvelope.SourceDevice, jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp, jsonEnvelope.ServerDeliveredTimestamp),
                    _pollVotes, _pollVoteChannel, "poll_vote");

                emitted |= DispatchUnionMember(data.PollTerminate,
                    pt => new PollTerminateEventArgs(subscriptionId, account, pt,
                        jsonEnvelope.Source, jsonEnvelope.SourceNumber, jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName, jsonEnvelope.SourceDevice, jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp, jsonEnvelope.ServerDeliveredTimestamp),
                    _pollTerminates, _pollTerminateChannel, "poll_terminate");

                emitted |= DispatchUnionMember(data.Payment,
                    pe => new PaymentEventArgs(subscriptionId, account, pe,
                        jsonEnvelope.Source, jsonEnvelope.SourceNumber, jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName, jsonEnvelope.SourceDevice, jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp, jsonEnvelope.ServerDeliveredTimestamp),
                    _payments, _paymentChannel, "payment");

                emitted |= DispatchUnionMember(data.PinMessage,
                    pm => new PinMessageEventArgs(subscriptionId, account, pm,
                        jsonEnvelope.Source, jsonEnvelope.SourceNumber, jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName, jsonEnvelope.SourceDevice, jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp, jsonEnvelope.ServerDeliveredTimestamp),
                    _pinMessages, _pinMessageChannel, "pin_message");

                emitted |= DispatchUnionMember(data.UnpinMessage,
                    upm => new UnpinMessageEventArgs(subscriptionId, account, upm,
                        jsonEnvelope.Source, jsonEnvelope.SourceNumber, jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName, jsonEnvelope.SourceDevice, jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp, jsonEnvelope.ServerDeliveredTimestamp),
                    _unpinMessages, _unpinMessageChannel, "unpin_message");

                emitted |= DispatchUnionMember(data.AdminDelete,
                    ad => new AdminDeleteEventArgs(subscriptionId, account, ad,
                        jsonEnvelope.Source, jsonEnvelope.SourceNumber, jsonEnvelope.SourceUuid,
                        jsonEnvelope.SourceName, jsonEnvelope.SourceDevice, jsonEnvelope.Timestamp,
                        jsonEnvelope.ServerReceivedTimestamp, jsonEnvelope.ServerDeliveredTimestamp),
                    _adminDeletes, _adminDeleteChannel, "admin_delete");

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
    /// <remarks>
    /// post-modernize-tuning §4.19: <see cref="System.Diagnostics.CodeAnalysis.NotNullWhenAttribute"/>
    /// сповіщає нульабельність-аналізатор, що при <c>true</c>-поверненні <paramref name="account"/>
    /// гарантовано non-null — це усуває потребу в <c>account!</c>-cast'ах на сайтах виклику.
    /// </remarks>
    private bool TryGetAccountBySubscriptionId(
        int subscriptionId,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? account)
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
        // Wave 7b channels.
        _pollCreateChannel.Writer.TryComplete();
        _pollVoteChannel.Writer.TryComplete();
        _pollTerminateChannel.Writer.TryComplete();
        _paymentChannel.Writer.TryComplete();
        _pinMessageChannel.Writer.TryComplete();
        _unpinMessageChannel.Writer.TryComplete();
        _adminDeleteChannel.Writer.TryComplete();

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
        // Wave 7b Subject disposal.
        _pollCreates.OnCompleted(); _pollCreates.Dispose();
        _pollVotes.OnCompleted(); _pollVotes.Dispose();
        _pollTerminates.OnCompleted(); _pollTerminates.Dispose();
        _payments.OnCompleted(); _payments.Dispose();
        _pinMessages.OnCompleted(); _pinMessages.Dispose();
        _unpinMessages.OnCompleted(); _unpinMessages.Dispose();
        _adminDeletes.OnCompleted(); _adminDeletes.Dispose();
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
        // post-modernize-tuning §11.B.6: реєструємо gauge-провайдер для активних підписок.
        // Безпечно читати _accountSubscriptions.Count поза локом — Dictionary.Count є потокобезпечним
        // для read-only-доступу (документація: read-thread-safe доки немає concurrent writes;
        // у нас всі writes — під _subscriptionsLock, тож worst case — на 1 застаріле значення).
        SignalCliDiagnostics.SetActiveSubscriptionsProvider(GetActiveSubscriptionCount);
        return Task.CompletedTask;
    }

    /// <summary>
    /// post-modernize-tuning §11.B.6: потокобезпечне читання кількості активних підписок
    /// для <see cref="SignalCliDiagnostics.ActiveSubscriptions"/> ObservableGauge.
    /// </summary>
    private int GetActiveSubscriptionCount()
    {
        lock (_subscriptionsLock)
        {
            return _accountSubscriptions.Count;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Симетрично StartAsync: відписуємось від нотифікацій, щоб повторний Start був чистим.
        var sub = Interlocked.Exchange(ref _notificationSubscription, null);
        sub?.Dispose();
        return Task.CompletedTask;
    }
}