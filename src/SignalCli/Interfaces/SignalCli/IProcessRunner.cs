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
    /// <remarks>
    /// post-modernize-tuning §8a.4 (audit B4): повертає <see cref="ValueTask{TResult}"/>,
    /// бо реалізація синхронна (Process.Start не має async-overload'у) — <c>ValueTask</c>
    /// без alloc'и обгортки. Старий <c>Task&lt;T&gt;</c>-сigna призводила до зайвої
    /// <c>Task.FromResult</c>-обгортки у <c>ProcessRunner.StartProcessWithHandle</c>.
    /// </remarks>
    ValueTask<(IProcess Process, StreamPair StreamPair)> StartProcessWithHandle(
        ProcessConfig config,
        CancellationToken cancellationToken = default
    );
}