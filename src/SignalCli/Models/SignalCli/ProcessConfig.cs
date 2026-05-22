namespace SignalCli.Models.SignalCli;

/// <summary>
/// Конфігурація для запуску зовнішнього процесу.
/// </summary>
/// <remarks>
/// Містить набір налаштувань для запуску та керування процесом Signal CLI,
/// включаючи шляхи до виконуваного файлу, аргументи, робочу директорію
/// та налаштування перенаправлення потоків.
/// </remarks>
public class ProcessConfig
{
    /// <summary>
    /// Шлях до виконуваного файлу.
    /// </summary>
    /// <remarks>
    /// Повний або відносний шлях до файлу, який буде запущено (наприклад, "java").
    /// </remarks>
    public string? Executable { get; set; }
    
    /// <summary>
    /// Аргументи командного рядка для процесу.
    /// </summary>
    /// <remarks>
    /// Рядок з аргументами, які будуть передані виконуваному файлу.
    /// </remarks>
    public string? Arguments { get; set; }
    
    /// <summary>
    /// Робоча директорія для процесу.
    /// </summary>
    /// <remarks>
    /// Шлях до директорії, в якій буде запущено процес.
    /// Якщо не вказано, використовується поточна директорія.
    /// </remarks>
    public string? WorkingDirectory { get; set; }
    
    /// <summary>
    /// Вказує, чи потрібно перенаправляти стандартний ввід.
    /// </summary>
    /// <remarks>
    /// Якщо true, створюється потік для запису у стандартний ввід процесу.
    /// </remarks>
    public bool RedirectStandardInput { get; set; }
    
    /// <summary>
    /// Вказує, чи потрібно перенаправляти стандартний вивід.
    /// </summary>
    /// <remarks>
    /// Якщо true, створюється потік для читання зі стандартного виводу процесу.
    /// </remarks>
    public bool RedirectStandardOutput { get; set; }
    
    /// <summary>
    /// Вказує, чи потрібно перенаправляти стандартний потік помилок.
    /// </summary>
    /// <remarks>
    /// Якщо true, створюється потік для читання зі стандартного потоку помилок процесу.
    /// </remarks>
    public bool RedirectStandardError { get; set; }

    /// <summary>
    /// Змінні середовища для процесу.
    /// </summary>
    /// <remarks>
    /// Словник з парами ключ-значення, що визначають змінні середовища
    /// для запущеного процесу.
    /// </remarks>
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; set; }
        = new Dictionary<string, string>();
}