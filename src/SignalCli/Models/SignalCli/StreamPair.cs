namespace SignalCli.Models.SignalCli;

/// <summary>
/// Обгортка над потоками вводу/виводу зовнішнього процесу.
/// </summary>
/// <remarks>
/// Інкапсулює стандартні потоки (stdin, stdout, stderr) для взаємодії
/// з зовнішнім процесом. Реалізує <see cref="IDisposable"/> для
/// коректного звільнення ресурсів.
/// </remarks>
public class StreamPair : IDisposable
{
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
    /// </remarks>
    public void Dispose()
    {
        StandardInput.Dispose();
        StandardOutput.Dispose();
        StandardError.Dispose();
    }
}
