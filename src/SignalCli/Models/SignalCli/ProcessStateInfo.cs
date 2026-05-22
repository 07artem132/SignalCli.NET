namespace SignalCli.Models.SignalCli;

/// <summary>
/// Інформація про стан процесу Signal CLI.
/// </summary>
/// <remarks>
/// Об'єднує поточний стан процесу, потоки для взаємодії та інформацію про помилку.
/// Використовується для повідомлення про зміни стану процесу через <see cref="ProcessStateManager"/>.
/// </remarks>
public class ProcessStateInfo
{
    /// <summary>
    /// Поточний стан процесу.
    /// </summary>
    /// <value>Одне зі значень <see cref="ProcessState"/>.</value>
    public ProcessState State { get; }
    
    /// <summary>
    /// Пара потоків для взаємодії з процесом.
    /// </summary>
    /// <value>Об'єкт <see cref="StreamPair"/> або null, якщо потоки недоступні.</value>
    public StreamPair? StreamPair { get; }
    
    /// <summary>
    /// Інформація про помилку, якщо вона сталася.
    /// </summary>
    /// <value>Об'єкт винятку або null, якщо помилки не було.</value>
    public Exception? Error { get; }

    /// <summary>
    /// Створює новий екземпляр класу ProcessStateInfo.
    /// </summary>
    /// <param name="state">Стан процесу.</param>
    /// <param name="streamPair">Пара потоків для взаємодії з процесом.</param>
    /// <param name="error">Інформація про помилку.</param>
    public ProcessStateInfo(ProcessState state, StreamPair? streamPair = null, Exception? error = null)
    {
        State = state;
        StreamPair = streamPair;
        Error = error;
    }
}