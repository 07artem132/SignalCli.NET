namespace SignalCli.Models.SignalCli;

/// <summary>
/// Обгортка над потоками вводу/виводу зовнішнього процесу.
/// </summary>
/// <remarks>
/// Інкапсулює стандартні потоки (stdin, stdout, stderr) для взаємодії
/// з зовнішнім процесом. Реалізує <see cref="IDisposable"/> для
/// коректного звільнення ресурсів.
/// post-modernize-tuning §8c.15 (audit N18): sealed — інхеріт не є точкою розширення.
/// </remarks>
public sealed class StreamPair : IDisposable
{
    // post-modernize-tuning §8c.15: idempotent Dispose guard — захист від подвійного
    // виклику. StreamWriter/StreamReader самі по собі ідемпотентні, але плутанина
    // власності між StreamPair і Process (який теж тримає stream-and) може призвести
    // до double-dispose-шляхів. Прапор робить другий Dispose() no-op-ом.
    private int _disposedFlag;
    /// <summary>
    /// Створює новий екземпляр класу StreamPair.
    /// </summary>
    /// <param name="input">Потік для запису у стандартний ввід процесу.</param>
    /// <param name="output">Потік для читання зі стандартного виводу процесу.</param>
    /// <param name="error">Потік для читання зі стандартного потоку помилок процесу.</param>
    /// <exception cref="ArgumentNullException">Виникає, якщо будь-який з параметрів є null.</exception>
    public StreamPair(StreamWriter input, StreamReader output, StreamReader error)
    {
        StandardInput = input ?? throw new ArgumentNullException(nameof(input));
        StandardOutput = output ?? throw new ArgumentNullException(nameof(output));
        StandardError = error ?? throw new ArgumentNullException(nameof(error));
    }

    /// <summary>
    /// Потік для запису у стандартний ввід процесу.
    /// </summary>
    public StreamWriter StandardInput { get; }
    
    /// <summary>
    /// Потік для читання зі стандартного виводу процесу.
    /// </summary>
    public StreamReader StandardOutput { get; }
    
    /// <summary>
    /// Потік для читання зі стандартного потоку помилок процесу.
    /// </summary>
    public StreamReader StandardError { get; }

    /// <summary>
    /// Звільняє всі ресурси, пов'язані з потоками.
    /// </summary>
    /// <remarks>
    /// Закриває всі три потоки: StandardInput, StandardOutput та StandardError.
    /// Повторні виклики — no-op (§8c.15 guard через Interlocked.Exchange).
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposedFlag, 1) != 0) return;
        StandardInput.Dispose();
        StandardOutput.Dispose();
        StandardError.Dispose();
        GC.SuppressFinalize(this);
    }
}
