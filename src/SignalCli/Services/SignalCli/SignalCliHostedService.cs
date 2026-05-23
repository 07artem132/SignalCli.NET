using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalCli.Diagnostics;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Logging;
using SignalCli.Models;
using SignalCli.Models.SignalCli;

namespace SignalCli.Services.SignalCli;

/// <summary>
/// HostedService, який:
/// 1) Запускає та зупиняє Signal-CLI при старті/зупинці застосунку.
/// 2) Автоматично перезапускає його при аварійному завершенні (до MaxRestartAttempts
///    у межах вікна <see cref="Config.RestartWindowSeconds"/>).
/// 3) Надає доступ до StreamPair через IStreamPairProvider.
/// </summary>
// post-modernize-tuning §4.8 (audit D5): sealed — inheriting from hosted-services
// is not a supported extension point; configuration via SignalCliOptions only.
public sealed class SignalCliHostedService : IHostedService, IStreamPairProvider, IDisposable
{
    private readonly ILogger<SignalCliHostedService> _logger;
    private readonly IProcessRunner _processRunner;
    private readonly ProcessStateManager _stateManager;
    // D.4: типована immutable-конфігурація, читається з IOptions один раз у конструкторі.
    private readonly SignalCliOptions _options;
    // B.5: абстракція часу — у проді System (wall-clock), у тестах FakeTimeProvider.
    // Використовується для таймера вікна стабільності рестартів (раніше — сирий Task.Run+Task.Delay).
    private readonly TimeProvider _timeProvider;

    // post-modernize-tuning §6.2 (audit B2): SemaphoreSlim замість Nito.AsyncEx.AsyncLock —
    // trim/AOT-safe + стандартний .NET-патерн. Семантика identична: 1-permit async section.
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    // Поля для управління процесом
    private IProcess? _currentProcess;
    // Тримаємо посилання на пару потоків ВИКЛЮЧНО для звільнення ресурсу в CleanupProcess.
    // Єдине джерело істини про стан/потоки — ProcessStateManager.
    private StreamPair? _currentStreamPair;

    // post-modernize-tuning §7.2 (T3): typed test-seam замість reflection.GetField("_currentProcess").
    // Internal-only — InternalsVisibleTo("SignalCli.Tests") у csproj. Прибирає ризик opaque
    // breakage при rename'і приватних полів (рефлексія мовчки повертає null).
    internal IProcess? CurrentProcessForTests => _currentProcess;
    internal StreamPair? CurrentStreamPairForTests => _currentStreamPair;
    // post-modernize-tuning §2.4: Interlocked.Exchange-based disposal flag.
    private int _disposedFlag;
    private bool _disposed => Volatile.Read(ref _disposedFlag) != 0;
    private bool _stopping;
    // _restartCount має читатися/писатися лише під _operationLock — щоб B.5 (windowed
    // budget) узгоджувалося із гонкою між ForceRestartAsync і OnProcessExited.
    private int _restartCount;

    // B.4 (F3): таймер, який скидає _restartCount у 0, якщо процес стабільно у Running
    // довше за RestartWindowSeconds. Створюється у StartProcessInternalAsyncNoLock після
    // UpdateState(Running, ...) і скасовується у CleanupProcess / при наступному рестарті.
    // B.5: тепер ITimer від TimeProvider — у тестах віртуальний годинник.
    private ITimer? _restartWindowTimer;

    // Сигнал завершення для StreamPairChanged (щоб потік завершувався при Dispose сервісу).
    private readonly Subject<bool> _disposeSignal = new();

    /// <summary>
    /// Створює новий екземпляр хостованого сервісу Signal CLI.
    /// </summary>
    /// <param name="logger">Логер для запису діагностичної інформації.</param>
    /// <param name="processRunner">Запускач зовнішніх процесів.</param>
    /// <param name="stateManager">Менеджер стану процесу.</param>
    /// <param name="options">Типована конфігурація через <see cref="IOptions{SignalCliOptions}"/>.</param>
    /// <param name="timeProvider">
    /// Опціональний постачальник часу для таймера вікна стабільності рестартів
    /// (<see cref="ScheduleRestartWindowReset"/>). За замовчуванням <see cref="TimeProvider.System"/>;
    /// у тестах підставляється <c>FakeTimeProvider</c>, щоб обнулення лічильника не вимагало wall-clock.
    /// </param>
    public SignalCliHostedService(
        ILogger<SignalCliHostedService> logger,
        IProcessRunner processRunner,
        ProcessStateManager stateManager,
        IOptions<SignalCliOptions> options,
        TimeProvider? timeProvider = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        ArgumentNullException.ThrowIfNull(options);
        // D.4: читаємо .Value один раз — це тригерить валідацію OptionsValidator на старті.
        _options = options.Value;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    #region IHostedService

    /// <summary>
    /// Запускає хостований сервіс.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Завдання, що представляє асинхронну операцію.</returns>
    /// <exception cref="ObjectDisposedException">Виникає, якщо об'єкт був утилізований.</exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        SignalCliHostedServiceLog.StartBegin(_logger);

        try
        {
            // Запускаємо процес (перший старт) — під операційним локом, щоб
            // конкурентний OnProcessExited не міг перезапустити в той самий момент.
            // §6.2: SemaphoreSlim-locking pattern (1-permit), той самий контракт що AsyncLock.
            await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await StartProcessInternalAsyncNoLock(cancellationToken).ConfigureAwait(false);
            }
            finally { _operationLock.Release(); }
            SignalCliHostedServiceLog.Started(_logger);
        }
        catch (Exception ex)
        {
            SignalCliHostedServiceLog.StartFailed(_logger, ex);
            throw;
        }
    }

    /// <summary>
    /// Зупиняє хостований сервіс.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Завдання, що представляє асинхронну операцію.</returns>
    /// <exception cref="ObjectDisposedException">Виникає, якщо об'єкт був утилізований.</exception>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        SignalCliHostedServiceLog.StopBegin(_logger);

        try
        {
            // §6.2: SemaphoreSlim-locking pattern (1-permit), той самий контракт що AsyncLock.
            await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var currentState = _stateManager.CurrentState;
                if (currentState != ProcessState.Running && currentState != ProcessState.Starting)
                {
                    SignalCliHostedServiceLog.StopProcessSkipped(_logger, currentState);
                    return;
                }
                // Зупиняємо процес
                await StopProcessInternalAsyncNoLock(cancellationToken).ConfigureAwait(false);
            }
            finally { _operationLock.Release(); }
            SignalCliHostedServiceLog.Stopped(_logger);
        }
        catch (Exception ex)
        {
            SignalCliHostedServiceLog.StopFailed(_logger, ex);
            throw;
        }
    }

    #endregion

    #region IStreamPairProvider

    /// <summary>
    /// Поточна пара потоків (похідна від ProcessStateManager — єдиного джерела істини).
    /// </summary>
    public StreamPair? CurrentStreamPair => _stateManager.CurrentStreamPair;

    /// <summary>
    /// Потік сповіщень про зміну пари потоків (похідний від стану; завершується при Dispose).
    /// </summary>
    public IObservable<StreamPair?> StreamPairChanged =>
        _stateManager.StreamPairChanged.TakeUntil(_disposeSignal);

    /// <summary>
    /// Асинхронно очікує, поки пара потоків стане доступною (делегує менеджеру стану).
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Завдання, що представляє очікування готовності потоків.</returns>
    /// <exception cref="ObjectDisposedException">Виникає, якщо об'єкт був утилізований.</exception>
    public Task WaitForReadyAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _stateManager.WaitForReadyAsync(cancellationToken);
    }

    #endregion

    #region ForceRestartAsync

    /// <summary>
    /// Примусовий перезапуск Signal-CLI (наприклад, при "CLI завис").
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Завдання, що представляє асинхронну операцію перезапуску.</returns>
    public async Task ForceRestartAsync(CancellationToken cancellationToken)
    {
        if (_disposed || _stopping)
        {
            SignalCliHostedServiceLog.ForceRestartIgnored(_logger);
            return;
        }

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disposed || _stopping)
            {
                SignalCliHostedServiceLog.ForceRestartLockLost(_logger);
                return;
            }

            // Перевіримо, що поточний стан — Running або Failed, інакше рестарт марний
            var currentState = _stateManager.CurrentState;
            if (currentState != ProcessState.Running &&
                currentState != ProcessState.Failed)
            {
                SignalCliHostedServiceLog.ForceRestartSkipped(_logger, currentState);
                return;
            }

            // B.5: інкремент під локом — гонка з OnProcessExited неможлива.
            _restartCount++;
            if (_options.MaxRestartAttempts > 0 && _restartCount > _options.MaxRestartAttempts)
            {
                SignalCliHostedServiceLog.ForceRestartBudgetExceeded(_logger, _options.MaxRestartAttempts, _options.RestartWindowSeconds);
                return;
            }

            SignalCliHostedServiceLog.ForceRestartAttempt(_logger, _restartCount, _options.MaxRestartAttempts);
            // post-modernize-tuning §11.B.5: process-restart counter for observability.
            SignalCliDiagnostics.ProcessRestarts.Add(1, new KeyValuePair<string, object?>("trigger", "force"));

            // 1) Зупинка
            await StopProcessInternalAsyncNoLock(cancellationToken).ConfigureAwait(false);

            // 2) Невелика затримка (B.5: через _timeProvider — у тестах віртуальний).
            await Task.Delay(TimeSpan.FromSeconds(_options.RestartDelaySeconds), _timeProvider, cancellationToken).ConfigureAwait(false);

            // 3) Перезапуск
            await StartProcessInternalAsyncNoLock(cancellationToken).ConfigureAwait(false);
        }
        finally { _operationLock.Release(); }
    }

    #endregion

    #region Внутрішня логіка запуску/зупинки процесу

    /// <summary>
    /// Внутрішній метод для запуску процесу без блокування.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Завдання, що представляє асинхронну операцію.</returns>
    private async Task StartProcessInternalAsyncNoLock(CancellationToken cancellationToken)
    {
        if (_disposed) return;
        cancellationToken.ThrowIfCancellationRequested();

        // post-modernize-tuning §11.A.3 (audit N1): process.start span.
        using var activity = SignalCliDiagnostics.ActivitySource.StartActivity(
            SignalCliDiagnostics.ProcessStartActivityName, ActivityKind.Internal);

        var currentState = _stateManager.CurrentState;
        if (currentState != ProcessState.NotStarted &&
            currentState != ProcessState.Stopped &&
            currentState != ProcessState.Failed)
        {
            SignalCliHostedServiceLog.CannotStartInState(_logger, currentState);
            return;
        }

        SignalCliHostedServiceLog.StartingProcess(_logger);
        _stopping = false;
        // Лічильник скидаємо ЛИШЕ при початковому старті (з NotStarted); рестарти зберігають
        // лічильник, який потім скине таймер RestartWindow (див. ScheduleRestartWindowReset).
        if (currentState == ProcessState.NotStarted)
            _restartCount = 0;

        _stateManager.UpdateState(ProcessState.Starting);

        try
        {
            var procConfig = _options.ToProcessConfig();
            var (proc, streams) = await _processRunner.StartProcessWithHandle(procConfig, cancellationToken).ConfigureAwait(false);

            _currentProcess = proc;
            _currentStreamPair = streams;

            _currentProcess.Exited += OnProcessExited;

            // ProcessStateManager — єдине джерело істини; з нього похідні
            // CurrentStreamPair / StreamPairChanged / WaitForReadyAsync.
            _stateManager.UpdateState(ProcessState.Running, streams);
            // §11.A.3: basename only — full path is PII-adjacent (reveals host layout).
            activity?.SetTag("signal.process.executable", Path.GetFileName(procConfig.Executable));
            activity?.SetStatus(ActivityStatusCode.Ok);
            SignalCliHostedServiceLog.ProcessStarted(_logger, _currentProcess.Id);

            // B.4 (F3): стартуємо вікно стабільності — якщо процес проживе RestartWindowSeconds
            // у Running, _restartCount скинеться в 0, тобто поодинокі поодинокі збої перестануть
            // спалювати "бюджет" перезапусків назавжди.
            ScheduleRestartWindowReset();
        }
        catch (Exception ex)
        {
            SignalCliHostedServiceLog.ProcessStartFailed(_logger, ex);
            _stateManager.UpdateState(ProcessState.Failed, error: ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Внутрішній метод для зупинки процесу без блокування.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Завдання, що представляє асинхронну операцію.</returns>
    private async Task StopProcessInternalAsyncNoLock(CancellationToken cancellationToken)
    {
        if (_disposed) return;
        cancellationToken.ThrowIfCancellationRequested();

        var currentState = _stateManager.CurrentState;
        if (currentState != ProcessState.Running && currentState != ProcessState.Starting)
        {
            SignalCliHostedServiceLog.StopProcessSkipped(_logger, currentState);
            return;
        }

        SignalCliHostedServiceLog.StoppingProcess(_logger);
        _stopping = true;
        // Скасовуємо таймер вікна стабільності — далі stable-running не буде.
        CancelRestartWindowTimer();
        _stateManager.UpdateState(ProcessState.Stopping);

        try
        {
            if (_currentProcess != null && !_currentProcess.HasExited)
            {
                // B.9: stdin "exit" — у власному try/catch (наприклад, потік уже міг закритися).
                if (_currentStreamPair != null)
                {
                    try
                    {
                        await _currentStreamPair.StandardInput.WriteLineAsync("exit").ConfigureAwait(false);
                        await _currentStreamPair.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
                    {
                        SignalCliHostedServiceLog.ExitWriteFailed(_logger, ex);
                    }
                }

                // B.9: замість фіксованого Task.Delay чекаємо реального виходу процесу
                // через WaitForExitAsync + linkedCts(timeout) — швидко за нормального exit.
                // audit N4: TimeProvider-aware overload — тести з FakeTimeProvider можуть
                // провернути StopTimeoutSeconds віртуально, без wall-clock-залежності.
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.StopTimeoutSeconds), _timeProvider);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                try
                {
                    await _currentProcess.WaitForExitAsync(linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    // Timeout під WaitForExitAsync — далі fall through до Kill із попередженням.
                }

                if (!_currentProcess.HasExited)
                {
                    SignalCliHostedServiceLog.ProcessKillTimeout(_logger, _options.StopTimeoutSeconds);
                    _currentProcess.Kill(entireProcessTree: true);
                }
            }

            _stateManager.UpdateState(ProcessState.Stopped);
            SignalCliHostedServiceLog.ProcessStopped(_logger);
        }
        catch (Exception ex)
        {
            SignalCliHostedServiceLog.ProcessStopFailed(_logger, ex);
            _stateManager.UpdateState(ProcessState.Failed, error: ex);
            throw;
        }
        finally
        {
            CleanupProcess();
            _stopping = false;
        }
    }

    /// <summary>
    /// Очищує ресурси, пов'язані з процесом.
    /// </summary>
    private void CleanupProcess()
    {
        CancelRestartWindowTimer();
        if (_currentProcess != null)
        {
            _currentProcess.Exited -= OnProcessExited;
            try
            {
                _currentProcess.Dispose();
            }
            catch { /* ігноруємо */ }
            _currentProcess = null;
        }
        _currentStreamPair?.Dispose();
        _currentStreamPair = null;
    }

    /// <summary>
    /// Стартує таймер, який після <see cref="Config.RestartWindowSeconds"/> у стані Running
    /// скине лічильник перезапусків у 0. Викликається тільки тоді, коли процес щойно перейшов
    /// у Running; будь-який наступний рестарт/стоп має скасувати цей таймер.
    /// </summary>
    /// <remarks>
    /// B.5: реалізовано через <see cref="TimeProvider.CreateTimer"/> (а не <c>Task.Run</c> +
    /// <c>Task.Delay</c>) — тести з <c>FakeTimeProvider</c> провертають час віртуально.
    /// </remarks>
    private void ScheduleRestartWindowReset()
    {
        CancelRestartWindowTimer();
        var window = TimeSpan.FromSeconds(Math.Max(1, _options.RestartWindowSeconds));
        // Якірний об'єкт — щоб callback міг переконатися, що таймер не змінено.
        var localTimer = new TimerSlot();
        ITimer timer = _timeProvider.CreateTimer(
            static state => ((TimerSlot)state!).Fire(),
            localTimer,
            window,
            Timeout.InfiniteTimeSpan);
        localTimer.Bind(timer, this);
        _restartWindowTimer = timer;
    }

    /// <summary>
    /// Внутрішній slot, який тримає <see cref="ITimer"/> + посилання на сервіс,
    /// щоб <see cref="TimerCallback"/> міг безпечно виконати reset під локом.
    /// </summary>
    private sealed class TimerSlot
    {
        private ITimer? _timer;
        private SignalCliHostedService? _owner;

        public void Bind(ITimer timer, SignalCliHostedService owner)
        {
            _timer = timer;
            _owner = owner;
        }

        public void Fire()
        {
            var owner = _owner;
            var timer = _timer;
            if (owner == null || timer == null) return;
            // Запускаємо async-роботу: callback ITimer-а синхронний, тож обгортаємо.
            _ = owner.OnRestartWindowElapsedAsync(timer);
        }
    }

    /// <summary>
    /// Колбек таймера вікна стабільності: під операційним локом обнуляє
    /// <c>_restartCount</c>, якщо процес досі <c>Running</c> і таймер не замінено новим.
    /// </summary>
    private async Task OnRestartWindowElapsedAsync(ITimer firingTimer)
    {
        try
        {
            await _operationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_disposed) return;
                if (!ReferenceEquals(_restartWindowTimer, firingTimer)) return;
                if (_stateManager.CurrentState != ProcessState.Running) return;

                if (_restartCount > 0)
                {
                    SignalCliHostedServiceLog.RestartCountReset(_logger, Math.Max(1, _options.RestartWindowSeconds), _restartCount);
                    _restartCount = 0;
                }
            }
            finally { _operationLock.Release(); }
        }
        catch (Exception ex)
        {
            // Колбек ITimer — фоновий потік; не дамо вилетіти.
            SignalCliHostedServiceLog.RestartWindowCallbackFailed(_logger, ex);
        }
    }

    /// <summary>
    /// Скасовує таймер вікна стабільності (якщо запущено).
    /// </summary>
    private void CancelRestartWindowTimer()
    {
        var timer = Interlocked.Exchange(ref _restartWindowTimer, null);
        if (timer == null) return;
        try { timer.Dispose(); }
        catch (ObjectDisposedException) { /* ок */ }
    }

    #endregion

    #region Обробка Exited та автоперезапуск

    /// <summary>
    /// Обробник події Exited процесу. Тонка async-void обгортка, що в try/catch делегує
    /// важку логіку <see cref="OnProcessExitedAsync"/> — щоб виняток не зриває хост.
    /// </summary>
    /// <param name="sender">Джерело події.</param>
    /// <param name="e">Аргументи події.</param>
    private async void OnProcessExited(object? sender, EventArgs e)
    {
        // B.1/B.2 (F2): не мутуємо стан до взяття локу і ловимо все, що могло б "втекти"
        // в SynchronizationContext (async void) — щоб не вбити процес-хост.
        try
        {
            await OnProcessExitedAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            SignalCliHostedServiceLog.OnExitedUnhandled(_logger, ex);
        }
    }

    /// <summary>
    /// Реальна логіка обробки Exited: серіалізована через <see cref="_operationLock"/>,
    /// щоб не змагатися з <see cref="StopAsync"/> / <see cref="ForceRestartAsync"/>.
    /// </summary>
    private async Task OnProcessExitedAsync()
    {
        // Ранні дешеві перевірки (без локу) — не змінюємо стан тут.
        if (_disposed) return;
        if (_stopping) return; // умисна зупинка

        await _operationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Повторна перевірка під локом — стан міг змінитися, поки чекали.
            if (_disposed) return;
            if (_stopping) return;

            // Якщо StopProcessInternalAsync уже встиг перевести стан у Stopped — нічого робити.
            if (_stateManager.CurrentState == ProcessState.Stopped)
            {
                SignalCliHostedServiceLog.OnExitedAlreadyStopped(_logger);
                return;
            }

            SignalCliHostedServiceLog.ProcessExitedUnexpectedly(_logger);
            _stateManager.UpdateState(ProcessState.Failed);
            CleanupProcess();

            if (_options.MaxRestartAttempts <= 0)
            {
                SignalCliHostedServiceLog.AutoRestartDisabled(_logger);
                return;
            }

            // B.5: під локом. B.4: лічильник скидатиметься таймером вікна стабільності
            // (ScheduleRestartWindowReset), коли наступний старт проживе RestartWindowSeconds.
            _restartCount++;
            if (_restartCount > _options.MaxRestartAttempts)
            {
                SignalCliHostedServiceLog.AutoRestartBudgetExceeded(_logger, _options.MaxRestartAttempts, _options.RestartWindowSeconds);
                return;
            }

            SignalCliHostedServiceLog.AutoRestartAttempt(_logger, _restartCount, _options.MaxRestartAttempts);
            // post-modernize-tuning §11.B.5: process-restart counter (crash-triggered).
            SignalCliDiagnostics.ProcessRestarts.Add(1, new KeyValuePair<string, object?>("trigger", "crash"));
            try
            {
                // B.5: через _timeProvider для консистентності з ForceRestartAsync.
                await Task.Delay(TimeSpan.FromSeconds(_options.RestartDelaySeconds), _timeProvider, CancellationToken.None).ConfigureAwait(false);
                await StartProcessInternalAsyncNoLock(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SignalCliHostedServiceLog.AutoRestartFailed(_logger, ex);
            }
        }
        finally { _operationLock.Release(); }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Звільняє ресурси, пов'язані з об'єктом.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposedFlag, 1) != 0) return;
        GC.SuppressFinalize(this);

        SignalCliHostedServiceLog.Disposing(_logger);

        try
        {
            // Якщо процес все ще живий, краще його зупинити
            if (_currentProcess != null && !_currentProcess.HasExited)
            {
                _currentProcess.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            SignalCliHostedServiceLog.DisposeFailed(_logger, ex);
        }
        finally
        {
            CleanupProcess();

            // Завершуємо потік StreamPairChanged для підписників цього сервісу.
            _disposeSignal.OnNext(true);
            _disposeSignal.OnCompleted();
            _disposeSignal.Dispose();

            // Переводимо стан у Failed з помилкою — це провалює всі очікуючі WaitForReadyAsync
            // (єдине джерело істини — менеджер стану).
            if (_stateManager.CurrentState is ProcessState.Running
                or ProcessState.Starting or ProcessState.Stopping or ProcessState.NotStarted)
            {
                _stateManager.UpdateState(ProcessState.Failed,
                    error: new InvalidOperationException("SignalCliHostedService утилізовано."));
            }
        }
    }

    #endregion
}
