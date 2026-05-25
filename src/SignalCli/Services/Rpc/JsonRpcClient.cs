using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SignalCli.Diagnostics;
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
/// Гарантує обмежений час життя запитів (<see cref="SignalCliOptions.RequestTimeoutSeconds"/>)
/// та коректний lifecycle читачів stdout/stderr: цикли скасовуються та чекаються до
/// завершення на зміні <see cref="StreamPair"/> й при <see cref="DisposeAsync"/>.
/// </remarks>
internal sealed class JsonRpcClient : IJsonRpcClient
{
    private readonly ILogger<JsonRpcClient> _logger;
    private readonly IStreamPairProvider _streamProvider;
    private readonly TimeSpan _requestTimeout;
    // audit N4: TimeProvider інжектиться, щоб `new CancellationTokenSource(timeout, _timeProvider)`
    // не залежав від wall-clock у тестах timeout-шляхів (паттерн уже використано в
    // SignalCliHealthMonitor.PingCliAsync — поширюємо).
    private readonly TimeProvider _timeProvider;
    private readonly Subject<JsonRpcNotification<SubscriptionEventArgs>> _notificationSubject = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonRpcResponse?>> _pendingRequests = new();
    // post-modernize-tuning §6.1 (audit B2): SemaphoreSlim замість AsyncLock — drop Nito.AsyncEx
    // (не trim-safe; крім того, SemaphoreSlim(1,1) — стандартний .NET-патерн async-locking).
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly AtomicCounter _requestIdCounter = new();
    private readonly CompositeDisposable _disposables = new();
    // A.9: маркерний токен для скасування pending-requests при DisposeAsync.
    // Тримаємо як поле, щоб TrySetCanceled нес його у OperationCanceledException.
    private readonly CancellationTokenSource _disposeCts = new();
    // post-modernize-tuning §2.4 (audit C2): _disposed — int + Interlocked.Exchange,
    // щоб DisposeAsync і паралельний reader/consumer гарантовано бачили disposal один раз.
    private int _disposedFlag; // 0 = alive, 1 = disposed
    private bool _disposed => Volatile.Read(ref _disposedFlag) != 0;

    // Стан читачів захищаємо окремим локом, бо OnStreamPairChanged, StartReading
    // та Dispose можуть конкурувати; всі переходи (старт / зупинка / заміна) серіалізуються.
    private readonly System.Threading.Lock _readerLock = new();
    private CancellationTokenSource? _readerCts;
    private Task? _stdoutTask;
    private Task? _stderrTask;
    // Час очікування на завершення попередніх читачів при заміні пари або диспоузі.
    private static readonly TimeSpan ReaderStopTimeout = TimeSpan.FromSeconds(5);

    // audit A3 / §1: Channel між stdout-парсером і fan-out-споживачем нотифікацій.
    // SingleReader=true (один Task крутить consumer-loop), SingleWriter=true (тільки stdout-task пише),
    // FullMode=Wait — якщо споживач (підписник через Subject.OnNext) повільний, WriteAsync
    // блокує stdout-цикл, чим створює back-pressure до самого signal-cli. У нас вибрано Wait,
    // а не DropOldest, бо втрата нотифікації = втрата події у консумерській логіці (а bounded-канал
    // у SignalEventService для подій вже сам обробляє DropOldest для async-stream-сторони).
    private readonly Channel<JsonRpcNotification<SubscriptionEventArgs>> _notificationChannel;
    private Task? _notificationConsumerTask;

    /// <summary>
    /// Створює новий екземпляр JSON-RPC клієнта.
    /// </summary>
    /// <param name="logger">Логер для запису діагностичної інформації.</param>
    /// <param name="streamProvider">Постачальник потоків для взаємодії з зовнішнім процесом.</param>
    /// <param name="options">Конфігурація — для отримання <see cref="SignalCliOptions.RequestTimeoutSeconds"/>.</param>
    /// <param name="timeProvider">
    /// audit N4: опціональний постачальник часу для <c>CancellationTokenSource(timeout, TimeProvider)</c>
    /// у <see cref="InvokeMethodAsync"/>. За замовчуванням <see cref="TimeProvider.System"/>; у тестах
    /// підставляється <c>FakeTimeProvider</c> — щоб віртуально провертати таймаут.
    /// </param>
    /// <remarks>D.4: приймає типовану <see cref="SignalCliOptions"/> замість legacy <c>Config</c>.</remarks>
    internal JsonRpcClient(ILogger<JsonRpcClient> logger,
        IStreamPairProvider streamProvider,
        SignalCliOptions options,
        TimeProvider? timeProvider = null)
    {
        _logger = logger;
        _streamProvider = streamProvider;
        var timeoutSeconds = Math.Max(1, options?.RequestTimeoutSeconds ?? 30);
        _requestTimeout = TimeSpan.FromSeconds(timeoutSeconds);
        _timeProvider = timeProvider ?? TimeProvider.System;

        // audit A3 / §1: bounded-канал між стdout-парсером і fan-out. FullMode=Wait —
        // повільний підписник створює back-pressure аж до самого signal-cli (через
        // блокування stdout-WriteAsync), не накопичуючи нотифікації в пам'яті.
        var capacity = Math.Max(1, options?.NotificationChannelCapacity ?? 1024);
        _notificationChannel = Channel.CreateBounded<JsonRpcNotification<SubscriptionEventArgs>>(
            new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait,
            });
        // Consumer-loop. Працює доти, доки канал не буде Complete()-ний у DisposeAsync.
        _notificationConsumerTask = Task.Run(NotificationConsumerLoopAsync);

        // Коли StreamPair змінюється — скидаємо всі pendingRequests і перезапускаємо читачів.
        var sub = _streamProvider.StreamPairChanged
            .Subscribe(OnStreamPairChanged);
        _disposables.Add(sub);
    }

    /// <summary>
    /// audit A3 / §1: окремий споживач каналу нотифікацій. Працює в одному Task'у
    /// (SingleReader=true) і викликає <c>_notificationSubject.OnNext</c>. Будь-який
    /// синхронний exception підписника не повинен зривати цикл — ловимо й логуємо.
    /// </summary>
    private async Task NotificationConsumerLoopAsync()
    {
        try
        {
            await foreach (var notification in _notificationChannel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    _notificationSubject.OnNext(notification);
                }
                // Навмисний широкий catch: межа fan-out — синхронний exception підписника
                // не повинен зривати споживач каналу (логуємо й продовжуємо).
                catch (Exception ex) when (!_disposed)
                {
                    JsonRpcClientLog.NotificationDispatchFailed(_logger, ex);
                }
            }
        }
        // Навмисний широкий catch: межа фонової задачі — логуємо й виходимо.
        catch (Exception ex) when (!_disposed)
        {
            JsonRpcClientLog.NotificationConsumerCrashed(_logger, ex);
        }
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
                    // audit A3 / §1: парсимо синхронно, але fan-out нотифікацій іде через
                    // bounded-канал — повільний підписник заблокує WriteAsync, чим створить
                    // back-pressure до самого stdout-циклу.
                    await ProcessMessageAsync(line, token).ConfigureAwait(false);
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
    /// <remarks>
    /// audit A3 / §1: для нотифікацій fan-out іде через bounded-канал
    /// (<see cref="_notificationChannel"/>) — повільний підписник створює back-pressure
    /// до stdout-циклу через очікування на <c>WriteAsync</c>. Відповіді (з id) залишаються
    /// синхронними — лише <c>tcs.TrySetResult</c>, без потенціалу блокування.
    /// </remarks>
    /// <param name="jsonLine">JSON-рядок для обробки.</param>
    /// <param name="cancellationToken">Токен скасування — пов'язаний із stdout-циклом.</param>
    private async Task ProcessMessageAsync(string jsonLine, CancellationToken cancellationToken)
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
                    // §6.7: AOT-safe — `JsonElement.Deserialize<T>(JsonTypeInfo<T>)`.
                    var response = rootElement.Deserialize(SignalJsonContext.Default.JsonRpcResponse);
                    if (!tcs.TrySetResult(response))
                    {
                        JsonRpcClientLog.TrySetResultFailed(_logger);
                    }

                    return;
                }
            }
            else if (rootElement.TryGetProperty("method", out _))
            {
                var notificationRaw = rootElement.Deserialize(SignalJsonContext.Default.JsonRpcNotificationRaw);
                if (notificationRaw != null)
                {
                    // ПРИВАТНІСТЬ: RawParams містить вміст повідомлення — не логуємо його.
                    // На рівні Debug — лише метод; повний JSON доступний лише на Trace.
                    JsonRpcClientLog.NotificationMethod(_logger, notificationRaw.Method);
                    JsonRpcClientLog.NotificationRawParams(_logger, notificationRaw.Params.GetRawText());

                    // Далі «до-десеріалізуємо» Params у типізований об'єкт
                    var subscriptionEventArgs =
                        notificationRaw.Params.Deserialize(SignalJsonContext.Default.SubscriptionEventArgs);
                    if (subscriptionEventArgs == null) return;
                    var typedNotification = new JsonRpcNotification<SubscriptionEventArgs>
                    {
                        JsonRpc = notificationRaw.JsonRpc,
                        Method = notificationRaw.Method,
                        Params = subscriptionEventArgs
                    };

                    // audit A3 / §1: НЕ викликаємо OnNext тут. Пишемо в канал —
                    // повільний підписник заблокує WriteAsync і створить back-pressure.
                    await _notificationChannel.Writer.WriteAsync(typedNotification, cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            JsonRpcClientLog.UnknownMessage(_logger, jsonLine);
        }
        catch (OperationCanceledException) { /* очікувано при stdout-cancel */ }
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
    /// <typeparam name="TRequest">Тип об'єкта запиту.</typeparam>
    /// <typeparam name="TResponse">Тип об'єкта відповіді.</typeparam>
    /// <param name="method">Назва методу, який потрібно викликати.</param>
    /// <param name="parameters">Параметри для виклику методу.</param>
    /// <param name="requestTypeInfo">Source-gen метадані для серіалізації запиту.</param>
    /// <param name="responseTypeInfo">Source-gen метадані для десеріалізації відповіді.</param>
    /// <param name="cancellationToken">Токен скасування для переривання операції.</param>
    /// <returns>Об'єкт відповіді від сервера JSON-RPC.</returns>
    /// <exception cref="ObjectDisposedException">Виникає, якщо об'єкт був утилізований.</exception>
    /// <exception cref="ArgumentNullException">Виникає, якщо параметри дорівнюють null.</exception>
    /// <exception cref="TimeoutException">Виникає, якщо відповідь не отримано за <see cref="SignalCliOptions.RequestTimeoutSeconds"/>.</exception>
    /// <exception cref="OperationCanceledException">Виникає, якщо викликач скасував запит через <paramref name="cancellationToken"/>.</exception>
    /// <exception cref="InvalidOperationException">Виникає, якщо отримано нульову відповідь або не вдалося перетворити результат.</exception>
    /// <exception cref="JsonRpcException">Виникає, якщо сервер повернув помилку.</exception>
    public async Task<TResponse> InvokeMethodAsync<TRequest, TResponse>(
        string method,
        TRequest parameters,
        JsonTypeInfo<TRequest> requestTypeInfo,
        JsonTypeInfo<TResponse> responseTypeInfo,
        CancellationToken cancellationToken = default)
        where TResponse : notnull
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (parameters is null)
            throw new ArgumentNullException(nameof(parameters));
        ArgumentNullException.ThrowIfNull(requestTypeInfo);
        ArgumentNullException.ThrowIfNull(responseTypeInfo);

        // §8c.4 скасовано: CA1305-аналізатор не визнає що digits 0-9 culture-invariant,
        // тож лишаємо InvariantCulture, але через named-constant — без using.
        var requestId = _requestIdCounter.Increment().ToString(System.Globalization.CultureInfo.InvariantCulture);

        // audit N13: structured scope — усі вкладені JsonRpcClientLog.* (SentRequest,
        // TrySetResultFailed тощо) автоматично несуть RpcMethod/RpcRequestId. Узгоджено
        // з A.11 (BeginScope у SignalEventService.OnNotificationReceived).
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["RpcMethod"] = method,
            ["RpcRequestId"] = requestId,
        });

        // post-modernize-tuning §11.A.2 (audit N1): tracing span. Без активного listener'а
        // StartActivity повертає null — нульова накладна, ?.SetTag — no-op.
        using var activity = SignalCliDiagnostics.ActivitySource.StartActivity(
            SignalCliDiagnostics.RpcActivityNamePrefix + method, ActivityKind.Client);
        activity?.SetTag("signal.rpc.method", method);
        activity?.SetTag("signal.rpc.request_id", requestId);

        // post-modernize-tuning §11.B.3: histogram-таймер під весь lifecycle запиту.
        var sw = Stopwatch.GetTimestamp();
        var status = "error"; // overwritten below; "error" — default-fail-stance per §11.A.2
        // ВАЖЛИВО: серіалізуємо параметри за КОНКРЕТНИМ типом TRequest у JsonElement.
        // Інакше STJ серіалізує властивість Params (тип object) як "{}" і всі параметри
        // запиту втрачаються (на відміну від Newtonsoft, який брав runtime-тип).
        // §6.7: AOT-safe overload `SerializeToElement<T>(value, JsonTypeInfo<T>)` — без reflection.
        var paramsElement = JsonSerializer.SerializeToElement(parameters, requestTypeInfo);
        var request = new JsonRpcRequest(Method: method, Params: paramsElement, Id: requestId);

        var tcs = new TaskCompletionSource<JsonRpcResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[requestId] = tcs;

        // F1: окрема timeout-CTS, щоб відрізнити таймаут від callerCancel.
        // audit N4: TimeProvider-aware overload (.NET 8+) — тести з FakeTimeProvider
        // можуть провернути таймаут віртуально, без wall-clock-залежності.
        using var timeoutCts = new CancellationTokenSource(_requestTimeout, _timeProvider);
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
                    {
                        // signal-cli-protocol-alignment (typed-rpc-errors): high-leverage error
                        // codes (RateLimit -5, UntrustedIdentity -4, CaptchaRejected -6) surface
                        // як derived exceptions, щоб consumer'и могли catch-by-type замість
                        // inspect-message-text.
                        //
                        // signal-cli-api-coverage Wave 1: додано CaptchaRequiredException (код -6)
                        // та GroupAdminRequiredException (код -1 + message-match "admin"). Останній
                        // — heuristic: upstream не виділяє admin-required у окремий код, тож
                        // message-substring дозволяє typed-catch у обмін на можливі false-positive
                        // (інші UserError повідомлення які згадують "admin").
                        throw response.Error.Code switch
                        {
                            (int)JsonRpcErrorCode.RateLimit => new RateLimitException(response.Error),
                            (int)JsonRpcErrorCode.UntrustedIdentity => new UntrustedIdentityException(response.Error),
                            (int)JsonRpcErrorCode.CaptchaRejected => new CaptchaRequiredException(response.Error),
                            (int)JsonRpcErrorCode.UserError when response.Error.Message?
                                .Contains("admin", StringComparison.OrdinalIgnoreCase) == true
                                => new GroupAdminRequiredException(response.Error),
                            _ => new JsonRpcException(response.Error),
                        };
                    }

                    // §6.7: AOT-safe extension overload `JsonElement.Deserialize<T>(JsonTypeInfo<T>)`.
                    var typedResult = response.Result.Deserialize(responseTypeInfo);
                    if (typedResult is null)
                        throw new InvalidOperationException($"Не вдалося перетворити JSON-результат на {typeof(TResponse).Name}");

                    status = "ok";
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    return typedResult;
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    // Спрацював лише таймаут (а не callerCancel) — фолтимо TimeoutException, щоб
                    // викликач міг відрізнити «нема відповіді» від власного скасування.
                    status = "timeout";
                    activity?.SetStatus(ActivityStatusCode.Error, nameof(TimeoutException));
                    throw new TimeoutException(
                        $"JSON-RPC метод '{method}' не отримав відповіді за {_requestTimeout.TotalSeconds:F0} с");
                }
            }
        }
        catch (Exception ex) when (status == "error")
        {
            // §11.A.2 (audit N1): Type-name only — повідомлення може нести RPC-error-текст (PII-ризик).
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            throw;
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
            // §11.B.2-3: лічильник + гістограма.
            var elapsedMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            SignalCliDiagnostics.RpcRequests.Add(1,
                new KeyValuePair<string, object?>("method", method),
                new KeyValuePair<string, object?>("status", status));
            SignalCliDiagnostics.RpcDuration.Record(elapsedMs,
                new KeyValuePair<string, object?>("method", method));
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
        // §6.1: SemaphoreSlim замість AsyncLock — той самий контракт (1-permit critical section),
        // але без Nito.AsyncEx-залежності й trim/AOT-safe.
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var pair = _streamProvider.CurrentStreamPair
                       ?? throw new InvalidOperationException("Немає активної пари потоків");

            // Серіалізація запиту в JSON з використанням System.Text.Json.
            // §6.7: AOT-safe — `JsonSerializer.Serialize<T>(value, JsonTypeInfo<T>)`.
            var json = JsonSerializer.Serialize(req, SignalJsonContext.Default.JsonRpcRequest);
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
        finally
        {
            _sendLock.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// A.6: <c>IDisposable</c> прибрано з контракту — асинхронний cleanup має тут
    /// канонічний шлях через <see cref="IAsyncDisposable.DisposeAsync"/>.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        // §2.4: Interlocked.Exchange — лише один виклик доходить до disposal.
        if (Interlocked.Exchange(ref _disposedFlag, 1) != 0) return;

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

        // 3) audit A3 / §1: закриваємо writer-кінець каналу, чекаємо завершення
        //    consumer-loop'у (він дочитає буфер і вийде природньо), і тільки потім
        //    диспозуємо Subject — щоб остання нотифікація точно пройшла OnNext.
        _notificationChannel.Writer.TryComplete();
        if (_notificationConsumerTask != null)
        {
            try
            {
                // Невеликий таймаут, щоб гарантовано не залипнути на slow-subscriber'і.
                await _notificationConsumerTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                JsonRpcClientLog.NotificationConsumerStopTimeout(_logger);
            }
            catch (Exception)
            {
                // Помилка в consumer уже залогована; ковтаємо тут — мета DisposeAsync лиш зачекати.
            }
        }

        // 4) Звільняємо потік нотифікацій.
        _notificationSubject.OnCompleted();
        _notificationSubject.Dispose();

        _disposeCts.Dispose();
    }
}
