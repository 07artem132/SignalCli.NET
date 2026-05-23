using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.Rpc;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.Rpc;
using SignalCli.Models.Signal;
using SignalCli.Models.Signal.Events;

namespace SignalCli.Services.Signal;

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

    public async Task<SubscribeReceiveResponse> SubscribeAsync(string account,
        CancellationToken cancellationToken = default)
    {
        // Перевіряємо наявність існуючої підписки
        lock (_subscriptionsLock)
        {
            if (_accountSubscriptions.ContainsKey(account))
            {
                throw new InvalidOperationException(
                    $"Обліковий запис '{account}' вже підписаний на події. Спочатку відпишіться.");
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
            if (_accountSubscriptions.ContainsKey(account))
            {
                throw new InvalidOperationException(
                    $"Обліковий запис '{account}' вже підписаний (гонка).");
            }

            _accountSubscriptions[account] = subscriptionId;
        }

        _logger.LogInformation("SubscribeAsync: обліковий запис={Account}, ідентифікатор підписки={SubId}",
            account, subscriptionId);

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
            _logger.LogWarning("Не знайдено підписку з ідентифікатором={SubId}", subscriptionId);
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

        _logger.LogInformation("Відписка успішна: Обліковий запис={Account}, ІдПідписки={SubscriptionId}", account, subscriptionId);

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
                _logger.LogWarning("Сповіщення без Envelope, пропускаємо...");
                return;
            }

            if (!TryGetAccountBySubscriptionId(subscriptionId, out string? account))
            {
                _logger.LogDebug("Подія для неактуальної підписки {SubId}, ігноруємо.", subscriptionId);
                return;
            }

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
                    emitted = true;
                }

                if (!emitted)
                    _logger.LogDebug("Невідомий тип DataMessage, пропускаємо...");
                return;
            }

            _logger.LogDebug("Невідомий тип події, пропускаємо...");
        }
        // Навмисний широкий catch: межа диспетчера сповіщень — одне погане
        // сповіщення не повинно зривати потік подій (логуємо й продовжуємо).
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при обробці вхідного сповіщення від Signal");
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

        _textMessages.OnCompleted();
        _reaction.OnCompleted();
        _attachments.OnCompleted();
        _sticker.OnCompleted();
        _typing.OnCompleted();
        _receipts.OnCompleted();
        _syncs.OnCompleted();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Підписуємося на потік сповіщень від JSON-RPC клієнта
        _rpcClient = _rpcClientProvider.Client;
        // Зберігаємо підписку, щоб коректно звільнити її у Dispose (раніше IDisposable губився).
        _notificationSubscription = _rpcClient.Notifications.Subscribe(OnNotificationReceived);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}