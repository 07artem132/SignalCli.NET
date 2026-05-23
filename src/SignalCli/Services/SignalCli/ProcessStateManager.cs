using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Logging;
using SignalCli.Models.SignalCli;

namespace SignalCli.Services.SignalCli;

/// <summary>
/// Єдине джерело істини про стан процесу Signal CLI. Зберігає стан, пару потоків і
/// помилку; з нього похідні: <see cref="CurrentStreamPair"/>, <see cref="StreamPairChanged"/>
/// та <see cref="WaitForReadyAsync"/> (раніше це дублювалося окремими механізмами в hosted-сервісі).
/// </summary>
public sealed class ProcessStateManager : IProcessStateNotifier, IDisposable
{
    private readonly BehaviorSubject<ProcessStateInfo> _stateSubject;
    private ProcessStateInfo _currentStateInfo;
    // C# 13 / .NET 9+: System.Threading.Lock — швидший за Monitor на object (див. IDE0330).
    private readonly System.Threading.Lock _lock = new();
    // post-modernize-tuning §2.2: _disposed — int + Interlocked.Exchange, щоб мати
    // lock-free short-circuit у UpdateState ще до взяття _lock; це знімає ризик
    // re-entrancy deadlock із синхронним Rx-підписником (System.Threading.Lock не reentrant).
    private int _disposed; // 0 = alive, 1 = disposed

    private readonly ILogger<ProcessStateManager> _logger;

    /// <summary>
    /// Потік (Rx) повних знімків стану процесу — стан + поточна пара потоків + остання помилка.
    /// </summary>
    public IObservable<ProcessStateInfo> ProcessState => _stateSubject.AsObservable();

    /// <summary>
    /// Поточна пара потоків (похідна від стану).
    /// </summary>
    public StreamPair? CurrentStreamPair
    {
        get
        {
            lock (_lock)
            {
                return _currentStateInfo.StreamPair;
            }
        }
    }

    /// <summary>
    /// Потік змін пари потоків (похідний від стану; послідовні однакові значення згортаються).
    /// </summary>
    public IObservable<StreamPair?> StreamPairChanged =>
        _stateSubject.Select(s => s.StreamPair).DistinctUntilChanged();

    /// <summary>
    /// Очікує, поки процес стане готовим (Running). Кидає виняток, якщо процес перейшов у Failed.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування очікування.</param>
    public Task WaitForReadyAsync(CancellationToken cancellationToken = default) =>
        _stateSubject
            .Where(s => s.State is Models.SignalCli.ProcessState.Running
                                or Models.SignalCli.ProcessState.Failed)
            .Take(1)
            .SelectMany(s => s.State == Models.SignalCli.ProcessState.Failed
                ? Observable.Throw<bool>(s.Error ?? new InvalidOperationException("Сервіс не готовий"))
                : Observable.Return(true))
            .ToTask(cancellationToken);

    /// <summary>Створює менеджер стану в початковому стані <see cref="Models.SignalCli.ProcessState.NotStarted"/>.</summary>
    /// <param name="logger">Логер для діагностики переходів стану.</param>
    public ProcessStateManager(ILogger<ProcessStateManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _currentStateInfo = new ProcessStateInfo(Models.SignalCli.ProcessState.NotStarted);
        _stateSubject = new BehaviorSubject<ProcessStateInfo>(_currentStateInfo);
    }

    /// <summary>
    /// Атомарно оновлює стан процесу і публікує новий знімок у потік <see cref="ProcessState"/>.
    /// Запізнілі виклики після <see cref="Dispose"/> тихо ігноруються (F25/B.25).
    /// </summary>
    /// <remarks>
    /// post-modernize-tuning §2.1-2.3 (audit A2): знімок стану робиться під <see cref="_lock"/>,
    /// але <c>OnNext</c> викликається <b>поза</b> локом — це усуває ризик re-entrancy
    /// deadlock із синхронним Rx-підписником, що міг би повернутися назад у
    /// <see cref="UpdateState"/> (<see cref="System.Threading.Lock"/> не реентрантний,
    /// на відміну від <c>Monitor</c>). Disposal-race window: між зняттям _lock і
    /// викликом OnNext паралельний <see cref="Dispose"/> міг встигнути закрити
    /// Subject — ловимо <see cref="ObjectDisposedException"/> і логуємо.
    /// </remarks>
    /// <param name="newState">Новий стан процесу.</param>
    /// <param name="streamPair">Поточна пара потоків (за наявності — для Running).</param>
    /// <param name="error">Помилка, що супроводжує перехід (для Failed).</param>
    public void UpdateState(ProcessState newState, StreamPair? streamPair = null, Exception? error = null)
    {
        // §2.2: lock-free short-circuit ще до взяття _lock — економимо contention
        // та усуваємо ризик деадлоку через одночасний Dispose, що захопив _lock.
        if (Volatile.Read(ref _disposed) != 0)
        {
            ProcessStateManagerLog.UpdateStateAfterDispose(_logger, newState);
            return;
        }

        ProcessStateInfo snapshot;
        lock (_lock)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                ProcessStateManagerLog.UpdateStateAfterDispose(_logger, newState);
                return;
            }

            _currentStateInfo = new ProcessStateInfo(newState, streamPair, error);
            snapshot = _currentStateInfo;
            ProcessStateManagerLog.StateChanged(_logger, newState);
        }

        // §2.1: OnNext поза локом — синхронний Rx-підписник може робити що завгодно,
        // включно з зворотним викликом UpdateState; ми не тримаємо лок, тож reentrancy ok.
        // §2.3: можливий race — між exit-локу і OnNext паралельний Dispose міг встигнути.
        try
        {
            _stateSubject.OnNext(snapshot);
        }
        catch (ObjectDisposedException)
        {
            // Документована вузька гонка: Dispose між exit-локу і OnNext. Тихо ігноруємо.
            ProcessStateManagerLog.UpdateStateAfterDispose(_logger, newState);
        }
    }

    /// <summary>Поточний стан процесу (потокобезпечне читання).</summary>
    public ProcessState CurrentState
    {
        get
        {
            lock (_lock)
            {
                return _currentStateInfo.State;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // §2.2/§2.4: Interlocked.Exchange гарантує, що тільки один Dispose-виклик
        // реально дійде до disposal. _disposed уже стає 1 ДО взяття _lock, тож
        // конкурентний UpdateState побачить його через Volatile.Read короткий шлях.
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        // Беремо _lock коротко — щоб усі UpdateState'и, що вже зайшли в lock-section
        // (взяли snapshot, але ще не викликали OnNext), завершили snapshot-частину
        // і ми не disposнули посеред неї.
        lock (_lock) { /* sync barrier */ }
        _stateSubject.Dispose();
    }
}