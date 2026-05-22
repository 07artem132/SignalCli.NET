using System.Reactive.Subjects;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
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

    // Потоки подій для різних типів сповіщень
    private readonly Subject<TextMessageEventArgs> _textMessages = new();
    private readonly Subject<ReactionEventArgs> _reaction = new();
    private readonly Subject<AttachmentEventArgs> _attachments = new();
    private readonly Subject<StickerEventArgs> _sticker = new();
    private readonly Subject<TypingEventArgs> _typing = new();
    private readonly Subject<ReceiptEventArgs> _receipts = new();
    private readonly Subject<SyncEventArgs> _syncs = new();

    private bool _disposed;

    public IObservable<TextMessageEventArgs> TextMessages => _textMessages;
    
    public IObservable<ReactionEventArgs> Reaction => _reaction;
    
    public IObservable<AttachmentEventArgs> Attachments => _attachments;
    
    public IObservable<StickerEventArgs> Sticker => _sticker;
    
    public IObservable<TypingEventArgs> TypingNotifications => _typing;
    
    public IObservable<ReceiptEventArgs> Receipts => _receipts;
    
    public IObservable<SyncEventArgs> Syncs => _syncs;

    public async Task<SubscribeReceiveResponse> SubscribeAsync(string account,
        CancellationToken cancellationToken = default)
    {
        // Перевіряємо наявність існуючої підписки
        lock (_accountSubscriptions)
        {
            if (_accountSubscriptions.ContainsKey(account))
            {
                throw new InvalidOperationException(
                    $"Обліковий запис '{account}' вже підписаний на події. Спочатку відпишіться.");
            }
        }

        var responseToken = await _signalCliClient
            .InvokeMethodAsync<JToken, SubscribeReceiveParameters>(
                "subscribeReceive",
                new SubscribeReceiveParameters(account),
                cancellationToken).ConfigureAwait(false);

        int subscriptionId = responseToken.Value<int>();

        lock (_accountSubscriptions)
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
        lock (_accountSubscriptions)
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

        lock (_accountSubscriptions)
        {
            _accountSubscriptions.Remove(account);
        }

        _logger.LogInformation("Відписка успішна: Обліковий запис={acc}, ІдПідписки={sid}", account, subscriptionId);

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

            // Якщо отримано подію, що містить дані повідомлення
            if (jsonEnvelope.DataMessage is not null)
            {
                var data = jsonEnvelope.DataMessage;
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
                    return;
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
                    return;
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
                    return;
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
                    return;
                }

                _logger.LogDebug("Невідомий тип DataMessage, пропускаємо...");
                return;
            }

            _logger.LogDebug("Невідомий тип події, пропускаємо...");
        }
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
        lock (_accountSubscriptions)
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
        _rpcClient.Notifications.Subscribe(OnNotificationReceived);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}