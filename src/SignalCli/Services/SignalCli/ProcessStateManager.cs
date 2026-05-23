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
    private bool _disposed;

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
    /// <param name="newState">Новий стан процесу.</param>
    /// <param name="streamPair">Поточна пара потоків (за наявності — для Running).</param>
    /// <param name="error">Помилка, що супроводжує перехід (для Failed).</param>
    public void UpdateState(ProcessState newState, StreamPair? streamPair = null, Exception? error = null)
    {
        // F25 (B.25): Dispose і UpdateState мають серіалізуватися — інакше OnNext
        // може потрапити в уже задиспоужений BehaviorSubject (-> ObjectDisposedException).
        // Тримаємо _lock на час OnNext (Rx-підписники з нашої програми відпрацьовують
        // швидко й не блокують одне одного на цьому самому локі).
        lock (_lock)
        {
            if (_disposed)
            {
                // Тихо ігноруємо запізнілі переходи, щоб не зривати ні викликача,
                // ні фоновий handler (наприклад, OnProcessExited після Dispose).
                ProcessStateManagerLog.UpdateStateAfterDispose(_logger, newState);
                return;
            }

            _currentStateInfo = new ProcessStateInfo(newState, streamPair, error);
            ProcessStateManagerLog.StateChanged(_logger, newState);
            _stateSubject.OnNext(_currentStateInfo);
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
        // F25 (B.25): беремо ТОЙ САМИЙ лок, що й UpdateState, щоб конкурентний
        // UpdateState не міг викликати OnNext на задиспоуженому Subject.
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _stateSubject.Dispose();
        }
    }
}