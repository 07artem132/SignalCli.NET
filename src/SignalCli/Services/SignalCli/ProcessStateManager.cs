using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.SignalCli;

namespace SignalCli.Services.SignalCli;

public sealed class ProcessStateManager : IProcessStateNotifier, IDisposable
{
    private readonly BehaviorSubject<ProcessStateInfo> _stateSubject;
    private ProcessStateInfo _currentStateInfo;
    private readonly object _lock = new object();
    private bool _disposed;

    private readonly ILogger<ProcessStateManager> _logger;

    public IObservable<ProcessStateInfo> ProcessState => _stateSubject.AsObservable();

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