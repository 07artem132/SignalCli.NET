using SignalCli.Models.SignalCli;

namespace SignalCli.Interfaces.SignalCli;

/// <summary>
/// Інтерфейс, що надає потік подій про стан процесу.
/// </summary>
public interface IProcessStateNotifier
{
    /// <summary>
    /// Потік інформації про стан процесу.
    /// </summary>
    IObservable<ProcessStateInfo> ProcessState { get; }
}