using System.Collections.Concurrent;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SignalCli.Exceptions;
using SignalCli.Interfaces.Rpc;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.Rpc;
using SignalCli.Models.Signal.Events;
using SignalCli.Models.SignalCli;
using SignalCli.Serialization;
using SignalCli.Utilities;

namespace SignalCli.Services.Rpc;

/// <summary>
/// Реалізація IJsonRpcClient — відправка запитів, отримання повідомлень.
/// </summary>
internal class JsonRpcClient : IJsonRpcClient
{
    private readonly ILogger<JsonRpcClient> _logger;
    private readonly IStreamPairProvider _streamProvider; // можливо реалізувати через HostedService
    private readonly Subject<JsonRpcNotification<SubscriptionEventArgs>> _notificationSubject = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonRpcResponse?>> _pendingRequests = new();
    private readonly Nito.AsyncEx.AsyncLock _sendLock = new();
    private readonly AtomicCounter _requestIdCounter = new();
    private readonly CompositeDisposable _disposables = new();
    private bool _disposed;

    /// <summary>
    /// Створює новий екземпляр JSON-RPC клієнта.
    /// </summary>
    /// <param name="logger">Логер для запису діагностичної інформації.</param>
    /// <param name="streamProvider">Постачальник потоків для взаємодії з зовнішнім процесом.</param>
    public JsonRpcClient(ILogger<JsonRpcClient> logger,
        IStreamPairProvider streamProvider)
    {
        _logger = logger;
        _streamProvider = streamProvider;

        // Коли StreamPair змінюється — скидаємо всі pendingRequests.
        var sub = _streamProvider.StreamPairChanged
            .Subscribe(OnStreamPairChanged);
        _disposables.Add(sub);
    }

    /// <summary>
    /// Потік повідомлень (нотифікацій) від JSON-RPC сервера.
    /// </summary>
    public IObservable<JsonRpcNotification<SubscriptionEventArgs>> Notifications => _notificationSubject.AsObservable();

    /// <summary>
    /// Обробляє зміну поточної пари потоків.
    /// </summary>
    /// <param name="pair">Нова пара потоків або null, якщо потоки стали недоступні.</param>
    private void OnStreamPairChanged(StreamPair? pair)
    {
        if (_disposed) return;

        // Скасовуємо всі очікуючі запити
        foreach (var kv in _pendingRequests)
        {
            kv.Value.TrySetCanceled();
        }

        _pendingRequests.Clear();

        if (pair != null)
        {
            StartReading(pair);
        }
    }

    /// <summary>
    /// Починає читання з пари потоків.
    /// </summary>
    /// <param name="pair">Пара потоків для читання.</param>
    private void StartReading(StreamPair pair)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var reader = new StreamReader(pair.StandardOutput.BaseStream);
                while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    if (_disposed) break;
                    // ПРИВАТНІСТЬ: сирий рядок містить вміст повідомлень/вкладення — лише Trace.
                    _logger.LogTrace("Отримано рядок від signal-cli: {Line}", line);

                    ProcessMessage(line);
                }
            }
            // Навмисний широкий catch: межа фонового циклу читання stdout —
            // одна помилка не повинна зупиняти читач (логуємо й завершуємо читання).
            catch (Exception ex) when (!_disposed)
            {
                _logger.LogError(ex, "Помилка читання з виходу процесу");
            }
        });
        _ = Task.Run(async () =>
        {
            string? line;
            while ((line = await pair.StandardError.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                _logger.LogTrace("STDERR> {Line}", line);
                // Можливо, також ProcessMessage(line) або хоча б лог.
            }
        });
    }

    /// <summary>
    /// Обробляє отримане JSON-повідомлення.
    /// </summary>
    /// <param name="jsonLine">JSON-рядок для обробки.</param>
    private void ProcessMessage(string jsonLine)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonLine,
                new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var rootElement = doc.RootElement;

            if (rootElement.TryGetProperty("id", out var idToken))
            {
                var id = idToken.ValueKind == JsonValueKind.String
                    ? idToken.GetString()
                    : idToken.GetRawText();
                if (!string.IsNullOrEmpty(id) && _pendingRequests.TryRemove(id, out var tcs))
                {
                    var response = rootElement.Deserialize<JsonRpcResponse>(SignalJson.Options);
                    if (!tcs.TrySetResult(response))
                    {
                        _logger.LogWarning("Не вдалося встановити результат");
                    }

                    return;
                }
            }
            else if (rootElement.TryGetProperty("method", out _))
            {
                var notificationRaw = rootElement.Deserialize<JsonRpcNotificationRaw>(SignalJson.Options);
                if (notificationRaw != null)
                {
                    // ПРИВАТНІСТЬ: RawParams містить вміст повідомлення — не логуємо його.
                    // На рівні Debug — лише метод; повний JSON доступний лише на Trace.
                    _logger.LogDebug("Отримано повідомлення: Method={Method}", notificationRaw.Method);
                    _logger.LogTrace("RawParams={Json}", notificationRaw.Params.GetRawText());

                    // Далі «до-десеріалізуємо» Params у типізований об'єкт
                    var subscriptionEventArgs =
                        notificationRaw.Params.Deserialize<SubscriptionEventArgs>(SignalJson.Options);
                    if (subscriptionEventArgs == null) return;
                    var typedNotification = new JsonRpcNotification<SubscriptionEventArgs>
                    {
                        JsonRpc = notificationRaw.JsonRpc,
                        Method = notificationRaw.Method,
                        Params = subscriptionEventArgs
                    };

                    _notificationSubject.OnNext(typedNotification);
                    return;
                }
            }

            _logger.LogWarning("Невідоме повідомлення: {Json}", jsonLine);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Помилка розбору JSON: {Line}", jsonLine);
        }
        // Навмисний широкий catch: одне некоректне повідомлення не повинно
        // зривати обробку наступних (логуємо й продовжуємо).
        catch (Exception ex)
        {
            _logger.LogError(ex, "Неочікувана помилка обробки JSON-рядка: {Line}", jsonLine);
        }
    }

    /// <summary>
    /// Асинхронно викликає вказаний метод JSON-RPC з переданими параметрами.
    /// </summary>
    /// <typeparam name="TResponse">Тип об'єкта відповіді.</typeparam>
    /// <typeparam name="TRequest">Тип об'єкта запиту.</typeparam>
    /// <param name="method">Назва методу, який потрібно викликати.</param>
    /// <param name="parameters">Параметри для виклику методу.</param>
    /// <param name="cancellationToken">Токен скасування для переривання операції.</param>
    /// <returns>Об'єкт відповіді від сервера JSON-RPC.</returns>
    /// <exception cref="ObjectDisposedException">Виникає, якщо об'єкт був утилізований.</exception>
    /// <exception cref="ArgumentNullException">Виникає, якщо параметри дорівнюють null.</exception>
    /// <exception cref="InvalidOperationException">Виникає, якщо отримано нульову відповідь або не вдалося перетворити результат.</exception>
    /// <exception cref="JsonRpcException">Виникає, якщо сервер повернув помилку.</exception>
    public async Task<TResponse> InvokeMethodAsync<TResponse, TRequest>(
        string method,
        TRequest parameters,
        CancellationToken cancellationToken = default)
        where TResponse : notnull
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (parameters is null)
            throw new ArgumentNullException(nameof(parameters));

        var requestId = _requestIdCounter.Increment().ToString(System.Globalization.CultureInfo.InvariantCulture);
        // ВАЖЛИВО: серіалізуємо параметри за КОНКРЕТНИМ типом TRequest у JsonElement.
        // Інакше STJ серіалізує властивість Params (тип object) як "{}" і всі параметри
        // запиту втрачаються (на відміну від Newtonsoft, який брав runtime-тип).
        var paramsElement = JsonSerializer.SerializeToElement(parameters, SignalJson.Options);
        var request = new JsonRpcRequest(Method: method, Params: paramsElement, Id: requestId);

        var tcs = new TaskCompletionSource<JsonRpcResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[requestId] = tcs;

        try
        {
            await SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

            await using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                var response = await tcs.Task.ConfigureAwait(false) ??
                               throw new InvalidOperationException("Отримано нульову відповідь");

                if (response.Error != null)
                    throw new JsonRpcException(response.Error);

                var typedResult = response.Result.Deserialize<TResponse>(SignalJson.Options);
                if (typedResult is null)
                    throw new InvalidOperationException($"Не вдалося перетворити JSON-результат на {typeof(TResponse).Name}");

                return typedResult;
            }
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    /// <summary>
    /// Відправляє JSON-RPC запит серверу.
    /// </summary>
    /// <param name="req">Запит для відправки.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <returns>Завдання, що представляє асинхронну операцію.</returns>
    /// <exception cref="InvalidOperationException">Виникає, якщо немає активної пари потоків або JSON занадто довгий.</exception>
    private async Task SendRequestAsync(JsonRpcRequest req, CancellationToken cancellationToken)
    {
        using (await _sendLock.LockAsync(cancellationToken))
        {
            var pair = _streamProvider.CurrentStreamPair
                       ?? throw new InvalidOperationException("Немає активної пари потоків");

            // Серіалізація запиту в JSON з використанням System.Text.Json
            var json = JsonSerializer.Serialize(req, SignalJson.Options);
            // signal-cli парсить вхідний JSON через Jackson, у якого
            // StreamReadConstraints.maxStringLength за замовчуванням = 20 000 000 символів.
            // Тому великі вкладення передаються через temp-файли (див. SignalMessage),
            // а тут — остання перевірка довжини всього рядка запиту.
            if (json.Length > 20_000_000)
                throw new InvalidOperationException("JSON параметри мають бути коротшими за 20000000 символів");
            // Відправка JSON у стандартний ввід
            await pair.StandardInput.WriteLineAsync(json).ConfigureAwait(false);
            await pair.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);

            // ПРИВАТНІСТЬ: json містить тіло повідомлення/вкладення — лише Trace.
            _logger.LogTrace("Відправлено JSON-RPC запит: {Json}", json);
        }
    }

    /// <summary>
    /// Виконує очищення та вивільнення ресурсів.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _disposables.Dispose();
        _notificationSubject.Dispose();

        foreach (var kv in _pendingRequests.Values)
        {
            kv.TrySetCanceled();
        }

        _pendingRequests.Clear();
    }
}