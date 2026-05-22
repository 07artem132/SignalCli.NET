using SignalCli.Models.SignalCli;

namespace SignalCli.Interfaces.SignalCli;

/// <summary>
/// Інтерфейс для запуску зовнішніх процесів (Signal-CLI та ін.).
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Запускає процес і повертає об'єкт для управління ним та потоки для взаємодії.
    /// </summary>
    /// <param name="config">Конфігурація процесу.</param>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Кортеж із запущеним процесом та парою потоків для взаємодії з ним.</returns>
    Task<(IProcess Process, StreamPair StreamPair)> StartProcessWithHandle(
        ProcessConfig config,
        CancellationToken cancellationToken = default
    );
}