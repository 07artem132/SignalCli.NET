using SignalCli.Models.SignalCli;

namespace SignalCli.Interfaces.SignalCli;

/// <summary>
/// Інтерфейс, що надає доступ до поточної пари потоків для взаємодії з процесом 
/// та сповіщає про їх зміну.
/// </summary>
public interface IStreamPairProvider
{
    /// <summary>
    /// Поточна пара потоків для взаємодії з процесом.
    /// </summary>
    StreamPair? CurrentStreamPair { get; }
    
    /// <summary>
    /// Потік сповіщень про зміну пари потоків.
    /// </summary>
    IObservable<StreamPair?> StreamPairChanged { get; }
    
    /// <summary>
    /// Асинхронно очікує, поки пара потоків стане доступною.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    Task WaitForReadyAsync(CancellationToken cancellationToken = default);
}