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
    /// Аргументи командного рядка для процесу (єдиний рядок).
    /// </summary>
    /// <remarks>
    /// Використовується лише як запасний варіант, якщо <see cref="ArgumentList"/> порожній.
    /// Для безпечної передачі аргументів із пробілами/лапками віддавайте перевагу <see cref="ArgumentList"/>.
    /// </remarks>
    public string? Arguments { get; set; }

    /// <summary>
    /// Аргументи командного рядка як окремі елементи.
    /// </summary>
    /// <remarks>
    /// Кожен елемент передається процесу окремо (через ProcessStartInfo.ArgumentList),
    /// тож .NET сам екранує пробіли, лапки та інші спецсимволи — це усуває ризик
    /// зламати чи інжектувати аргументи через шляхи з лапками.
    /// </remarks>
    public IReadOnlyList<string>? ArgumentList { get; set; }
    
    /// <summary>
    /// Запускати процес у новій групі процесів (Windows).
    /// </summary>
    /// <remarks>
    /// .NET 10: ізолює дочірній процес від консольних сигналів батька (наприклад Ctrl+C),
    /// тож завершенням signal-cli керує лише бібліотека (через "exit" або Kill).
    /// </remarks>
    public bool CreateNewProcessGroup { get; set; }

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