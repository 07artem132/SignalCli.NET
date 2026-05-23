using System.Collections.Concurrent;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SignalCli.Exceptions;
using SignalCli.Interfaces.Rpc;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Logging;
using SignalCli.Models;
using SignalCli.Models.Rpc;
using SignalCli.Models.Signal.Events;
using SignalCli.Models.SignalCli;
using SignalCli.Serialization;
using SignalCli.Utilities;

namespace SignalCli.Services.Rpc;

/// <summary>
/// Реалізація IJsonRpcClient — відправка запитів, отримання повідомлень.
/// </summary>
/// <remarks>
/// Гарантує обмежений час життя запитів (<see cref="Config.RequestTimeoutSeconds"/>)
/// та коректний lifecycle читачів stdout/stderr: цикли скасовуються та чекаються до
/// завершення на зміні <see cref="StreamPair"/> й при <see cref="DisposeAsync"/>.
/// </remarks>
internal sealed class JsonRpcClient : IJsonRpcClient
{
    private readonly ILogger<JsonRpcClient> _logger;
    private readonly IStreamPairProvider _streamProvider;
    private readonly TimeSpan _requestTimeout;
    private readonly Subject<JsonRpcNotification<SubscriptionEventArgs>> _notificationSubject = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonRpcResponse?>> _pendingRequests = new();
    private readonly Nito.AsyncEx.AsyncLock _sendLock = new();
    private readonly AtomicCounter _requestIdCounter = new();
    private readonly CompositeDisposable _disposables = new();
    // A.9: маркерний токен для скасування pending-requests при DisposeAsync.
    // Тримаємо як поле, щоб TrySetCanceled нес його у OperationCanceledException.
    private readonly CancellationTokenSource _disposeCts = new();
    private bool _disposed;

    // Стан читачів захищаємо окремим локом, бо OnStreamPairChanged, StartReading
    // та Dispose можуть конкурувати; всі переходи (старт / зупинка / заміна) серіалізуються.
    private readonly System.Threading.Lock _readerLock = new();
    private CancellationTokenSource? _readerCts;
    private Task? _stdoutTask;
    private Task? _stderrTask;
    // Час очікування на завершення попередніх читачів при заміні пари або диспоузі.
    private static readonly TimeSpan ReaderStopTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Створює новий екземпляр JSON-RPC клієнта.
    /// </summary>
    /// <param name="logger">Логер для запису діагностичної інформації.</param>
    /// <param name="streamProvider">Постачальник потоків для взаємодії з зовнішнім процесом.</param>
    /// <param name="options">Конфігурація — для отримання <see cref="SignalCliOptions.RequestTimeoutSeconds"/>.</param>
    /// <remarks>D.4: приймає типовану <see cref="SignalCliOptions"/> замість legacy <c>Config</c>.</remarks>
    internal JsonRpcClient(ILogger<JsonRpcClient> logger,
        IStreamPairProvider streamProvider,
        SignalCliOptions options)
    {
        _logger = logger;
        _streamProvider = streamProvider;
        var timeoutSeconds = Math.Max(1, options?.RequestTimeoutSeconds ?? 30);
        _requestTimeout = TimeSpan.FromSeconds(timeoutSeconds);

        // Коли StreamPair змінюється — скидаємо всі pendingRequests і перезапускаємо читачів.
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

        // A.9: скасовуємо pending-requests маркерним токеном з причини «stream pair замінено».
        // Власник запиту отримає OperationCanceledException, де CancellationToken == streamChangedCts.Token.
        using var streamChangedCts = new CancellationTokenSource();
        streamChangedCts.Cancel();
        foreach (var kv in _pendingRequests)
        {
            kv.Value.TrySetCanceled(streamChangedCts.Token);
        }

        _pendingRequests.Clear();

        // Зупиняємо попередні читачі та запускаємо нові (синхронно: спершу зупинка, потім старт).
        StopReadersSync();

        if (pair != null)
        {
            StartReading(pair);
        }
    }

    /// <summary>
    /// Зупиняє поточні читачі stdout/stderr: скасовує токен і чекає на завершення задач
    /// (з обмеженим таймаутом, щоб гарантовано не залипнути в OnStreamPairChanged).
    /// </summary>
    private void StopReadersSync()
    {
        CancellationTokenSource? cts;
        Task? stdoutTask;
        Task? stderrTask;
        lock (_readerLock)
        {
            cts = _readerCts;
            stdoutTask = _stdoutTask;
            stderrTask = _stderrTask;
            _readerCts = null;
            _stdoutTask = null;
            _stderrTask = null;
        }

        if (cts == null) return;

        try { cts.Cancel(); } catch (ObjectDisposedException) { /* race з Dispose — ок */ }

        // Чекаємо завершення з обмеженим таймаутом — інакше залипання stream-Read могло б
        // заблокувати OnStreamPairChanged.
        try
        {
            var tasks = new List<Task>(capacity: 2);
            if (stdoutTask != null) tasks.Add(stdoutTask);
            if (stderrTask != null) tasks.Add(stderrTask);
            if (tasks.Count > 0)
            {
                Task.WhenAll(tasks).Wait(ReaderStopTimeout);
            }
        }
        catch (AggregateException)
        {
            // Уже зловлено всередині циклів; не пропихаємо помилки в підписника StreamPairChanged.
        }

        cts.Dispose();
    }

    /// <summary>
    /// Асинхронний варіант <see cref="StopReadersSync"/> — використовується у <see cref="DisposeAsync"/>.
    /// </summary>
    private async Task StopReadersAsync()
    {
        CancellationTokenSource? cts;
        Task? stdoutTask;
        Task? stderrTask;
        lock (_readerLock)
        {
            cts = _readerCts;
            stdoutTask = _stdoutTask;
            stderrTask = _stderrTask;
            _readerCts = null;
            _stdoutTask = null;
            _stderrTask = null;
        }

        if (cts == null) return;

        try { cts.Cancel(); } catch (ObjectDisposedException) { /* ок */ }

        try
        {
            var tasks = new List<Task>(capacity: 2);
            if (stdoutTask != null) tasks.Add(stdoutTask);
            if (stderrTask != null) tasks.Add(stderrTask);
            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks).WaitAsync(ReaderStopTimeout).ConfigureAwait(false);
            }
        }
        catch (TimeoutException)
        {
            JsonRpcClientLog.ReaderStopTimeout(_logger);
        }
        catch (Exception)
        {
            // Помилки в циклах уже залоговано; ковтаємо тут — мета DisposeAsync лиш зачекати.
        }

        cts.Dispose();
    }

    /// <summary>
    /// Починає читання з пари потоків. Поточні читачі мають бути попередньо зупинені
    /// (через <see cref="StopReadersSync"/>).
    /// </summary>
    /// <param name="pair">Пара потоків для читання.</param>
    private void StartReading(StreamPair pair)
    {
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        // ВАЖЛИВО: НЕ обгортаємо pair.StandardOutput.BaseStream у новий StreamReader (як було),
        // бо StreamReader при Dispose закриває baseStream — а власник потоку це StreamPair/Process.
        // Читаємо безпосередньо з reader-а, який надає пара (StandardOutput — це StreamReader).
        var stdoutTask = Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var line = await pair.StandardOutput.ReadLineAsync(token).ConfigureAwait(false);
                    if (line is null) break;
                    // ПРИВАТНІСТЬ: сирий рядок містить вміст повідомлень/вкладення — лише Trace.
                    JsonRpcClientLog.StdoutLine(_logger, line);
                    ProcessMessage(line);
                }
            }
            catch (OperationCanceledException) { /* очікувано на скасуванні */ }
            // Навмисний широкий catch: межа фонового циклу читання stdout —
            // одна помилка не повинна зупиняти процес (логуємо й виходимо).
            catch (Exception ex) when (!_disposed && !token.IsCancellationRequested)
            {
                JsonRpcClientLog.StdoutReadFailed(_logger, ex);
            }
        }, token);

        // F4: симетричний try/catch на stderr-циклі — раніше його не було взагалі.
        var stderrTask = Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var line = await pair.StandardError.ReadLineAsync(token).ConfigureAwait(false);
                    if (line is null) break;
                    JsonRpcClientLog.StderrLine(_logger, line);
                }
            }
            catch (OperationCanceledException) { /* очікувано на скасуванні */ }
            catch (Exception ex) when (!_disposed && !token.IsCancellationRequested)
            {
                JsonRpcClientLog.StderrReadFailed(_logger, ex);
            }
        }, token);

        lock (_readerLock)
        {
            _readerCts = cts;
            _stdoutTask = stdoutTask;
            _stderrTask = stderrTask;
        }
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
                        JsonRpcClientLog.TrySetResultFailed(_logger);
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
                    JsonRpcClientLog.NotificationMethod(_logger, notificationRaw.Method);
                    JsonRpcClientLog.NotificationRawParams(_logger, notificationRaw.Params.GetRawText());

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

            JsonRpcClientLog.UnknownMessage(_logger, jsonLine);
        }
        catch (JsonException ex)
        {
            JsonRpcClientLog.JsonParseFailed(_logger, ex, jsonLine);
        }
        // Навмисний широкий catch: одне некоректне повідомлення не повинно
        // зривати обробку наступних (логуємо й продовжуємо).
        catch (Exception ex)
        {
            JsonRpcClientLog.UnexpectedProcessMessage(_logger, ex, jsonLine);
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
    /// <exception cref="TimeoutException">Виникає, якщо відповідь не отримано за <see cref="Config.RequestTimeoutSeconds"/>.</exception>
    /// <exception cref="OperationCanceledException">Виникає, якщо викликач скасував запит через <paramref name="cancellationToken"/>.</exception>
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

        // F1: окрема timeout-CTS, щоб відрізнити таймаут від callerCancel.
        using var timeoutCts = new CancellationTokenSource(_requestTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

            // F19: TrySetCanceled(token) — щоб OperationCanceledException ніс відповідний токен.
            await using (linkedCts.Token.Register(() => tcs.TrySetCanceled(linkedCts.Token)))
            {
                try
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
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    // Спрацював лише таймаут (а не callerCancel) — фолтимо TimeoutException, щоб
                    // викликач міг відрізнити «нема відповіді» від власного скасування.
                    throw new TimeoutException(
                        $"JSON-RPC метод '{method}' не отримав відповіді за {_requestTimeout.TotalSeconds:F0} с");
                }
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
            JsonRpcClientLog.SentRequest(_logger, json);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// A.6: <c>IDisposable</c> прибрано з контракту — асинхронний cleanup має тут
    /// канонічний шлях через <see cref="IAsyncDisposable.DisposeAsync"/>.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // 1) Зупиняємо підписку на StreamPairChanged та читачі.
        _disposables.Dispose();
        await StopReadersAsync().ConfigureAwait(false);

        // 2) A.9: скасовуємо очікуючі запити з токеном диспозу — викликач побачить
        //    OperationCanceledException, де CancellationToken == _disposeCts.Token.
        _disposeCts.Cancel();
        foreach (var kv in _pendingRequests.Values)
        {
            kv.TrySetCanceled(_disposeCts.Token);
        }
        _pendingRequests.Clear();

        // 3) Звільняємо потік нотифікацій.
        _notificationSubject.OnCompleted();
        _notificationSubject.Dispose();

        _disposeCts.Dispose();
    }
}
