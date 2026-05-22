namespace SignalCli.Interfaces.SignalCli;

/// <summary>
/// Інтерфейс для абстракції процесу операційної системи.
/// Надає методи для запуску, контролю та зупинки процесу.
/// </summary>
public interface IProcess : IDisposable
{
    /// <summary>
    /// Ідентифікатор процесу.
    /// </summary>
    int Id { get; }
    
    /// <summary>
    /// Вказує, чи завершився процес.
    /// </summary>
    bool HasExited { get; }
    
    /// <summary>
    /// Потік для запису у стандартний ввід процесу.
    /// </summary>
    StreamWriter StandardInput { get; }
    
    /// <summary>
    /// Потік для читання зі стандартного виводу процесу.
    /// </summary>
    StreamReader StandardOutput { get; }
    
    /// <summary>
    /// Потік для читання зі стандартного потоку помилок процесу.
    /// </summary>
    StreamReader StandardError { get; }
    
    /// <summary>
    /// Запускає процес.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>true, якщо процес успішно запущено; інакше - false.</returns>
    public bool Start(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Асинхронно очікує завершення процесу.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    Task WaitForExitAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Подія, що викликається, коли процес завершується.
    /// </summary>
    public event EventHandler? Exited;
    
    /// <summary>
    /// Примусово завершує процес.
    /// </summary>
    /// <param name="entireProcessTree">Якщо true, також завершує всі дочірні процеси.</param>
    void Kill(bool entireProcessTree);
}