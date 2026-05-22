using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.SignalCli;
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
    private readonly object _lock = new object();
    private bool _disposed;

    private readonly ILogger<ProcessStateManager> _logger;

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

    public ProcessStateManager(ILogger<ProcessStateManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _currentStateInfo = new ProcessStateInfo(Models.SignalCli.ProcessState.NotStarted);
        _stateSubject = new BehaviorSubject<ProcessStateInfo>(_currentStateInfo);
    }

    public void UpdateState(ProcessState newState, StreamPair? streamPair = null, Exception? error = null)
    {
        ProcessStateInfo newInfo;
        lock (_lock)
        {
            _currentStateInfo = new ProcessStateInfo(newState, streamPair, error);
            newInfo = _currentStateInfo;
        }

        _logger.LogInformation("Стан процесу змінено на {NewState}", newState);
        _stateSubject.OnNext(newInfo);
    }

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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stateSubject.Dispose();
    }
}