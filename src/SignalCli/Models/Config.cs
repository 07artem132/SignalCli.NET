using System.Runtime.InteropServices;
using SignalCli.Models.SignalCli;

namespace SignalCli.Models;

/// <summary>
/// Конфігурація Signal CLI.
/// </summary>
/// <remarks>
/// Містить налаштування для запуску та контролю процесу Signal CLI,
/// включаючи шляхи до файлів, параметри логування та автоперезапуску.
/// </remarks>
public class Config
{
    private const string DefaultJavaPath = "java";
    private const string DefaultLibDirectory = "SignalCli/lib";

    /// <summary>
    /// Режим отримання повідомлень.
    /// </summary>
    /// <value>
    /// true - ручне отримання повідомлень (режим manual);
    /// false - автоматичне отримання при запуску (режим on-start).
    /// </value>
    public bool UseManualReceiveMode { get; init; } = true;

    /// <summary>
    /// Максимальна кількість спроб перезапуску процесу.
    /// </summary>
    /// <value>
    /// Кількість спроб; 0 - автоперезапуск відключений.
    /// </value>
    public int MaxRestartAttempts { get; set; } = 3;

    /// <summary>
    /// Інтервал між перевірками стану процесу (у секундах).
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 40;
    
    /// <summary>
    /// Максимальний час очікування відповіді при перевірці стану (у секундах).
    /// </summary>
    public int HealthCheckTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Затримка перед перезапуском процесу (у секундах).
    /// </summary>
    public int RestartDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Час очікування граційного завершення signal-cli (після команди "exit"),
    /// перш ніж процес буде завершено примусово (у секундах).
    /// </summary>
    public int StopTimeoutSeconds { get; set; } = 2;

    /// <summary>
    /// Головна директорія програми.
    /// </summary>
    /// <remarks>
    /// Директорія, де розташовані піддиректорії /config, /log, /lib та інші.
    /// </remarks>
    public string AppHome { get; set; }

    /// <summary>
    /// Рівень логування для Signal CLI.
    /// </summary>
    public CliLogLevel CliLogLevelCli { get; set; } = CliLogLevel.Info;
    
    /// <summary>
    /// Шлях до файлу логу Signal CLI.
    /// </summary>
    public string LogFileCli { get; set; } = $"{AppDomain.CurrentDomain.BaseDirectory}/signal.log";
    
    /// <summary>
    /// Шлях до директорії з даними Signal CLI.
    /// </summary>
    public string StoragePathCli { get; set; } = $"{AppDomain.CurrentDomain.BaseDirectory}/SignalCliStorageData";

    /// <summary>
    /// Шлях до виконуваного файлу Java.
    /// </summary>
    public string JavaExecutable { get; set; }

    /// <summary>
    /// Піддиректорія з JAR-файлами Signal CLI.
    /// </summary>
    public string LibDirectory { get; set; }

    /// <summary>
    /// Змінні середовища для процесу Signal CLI.
    /// </summary>
    public IDictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Створює конфігурацію процесу для запуску Signal CLI.
    /// </summary>
    /// <returns>Об'єкт конфігурації процесу з налаштованими параметрами.</returns>
    /// <exception cref="FileNotFoundException">Виникає, якщо JAR-файли не знайдено.</exception>
    public ProcessConfig ToProcessConfig()
    {
        var classpath = BuildClasspath();
        var receiveModeArg = UseManualReceiveMode
            ? "--receive-mode=manual"
            : "--receive-mode=on-start";
        var logLevelArg = "";
        switch (CliLogLevelCli)
        {
            case CliLogLevel.Info:
                logLevelArg = "-v";
                break;
            case CliLogLevel.Debug:
                logLevelArg = "-vv";
                break;
            case CliLogLevel.Verbose:
                logLevelArg = "-vvv";
                break;
        }

        // Кожен аргумент — окремий елемент: ProcessStartInfo.ArgumentList сам екранує
        // пробіли та лапки, тож шляхи з лапками не можуть зламати/інжектувати аргументи.
        var argumentList = new List<string>
        {
            "-classpath", classpath,
            "org.asamk.signal.Main"
        };
        if (!string.IsNullOrEmpty(logLevelArg))
            argumentList.Add(logLevelArg);
        argumentList.Add($"--log-file={LogFileCli}");
        argumentList.Add($"--config={StoragePathCli}");
        argumentList.Add("jsonRpc");
        argumentList.Add(receiveModeArg);

        return new ProcessConfig
        {
            Executable = JavaExecutable,
            ArgumentList = argumentList,
            WorkingDirectory = AppHome,
            CreateNewProcessGroup = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            EnvironmentVariables = (IReadOnlyDictionary<string, string>)EnvironmentVariables
        };
    }

    /// <summary>
    /// Будує рядок classpath для JVM на основі JAR-файлів.
    /// </summary>
    /// <returns>Рядок classpath для JVM.</returns>
    /// <exception cref="FileNotFoundException">Виникає, якщо JAR-файли не знайдено.</exception>
    private string BuildClasspath()
    {
        var libPath = Path.Combine(AppHome, LibDirectory);
        var jarFiles = Directory.GetFiles(libPath, "*.jar");
        if (jarFiles.Length == 0)
        {
            throw new FileNotFoundException($"No JAR files found in {libPath}");
        }

        var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ";" : ":";
        return string.Join(separator, jarFiles);
    }

    /// <summary>
    /// Створює конфігурацію за замовчуванням.
    /// </summary>
    /// <returns>Об'єкт конфігурації з параметрами за замовчуванням.</returns>
    public static Config CreateDefault()
    {
        return new Config
        {
            AppHome = AppDomain.CurrentDomain.BaseDirectory,
            JavaExecutable = ResolveJavaPath(),
            LibDirectory = DefaultLibDirectory,
            MaxRestartAttempts = 3,
            RestartDelaySeconds = 5
        };
    }

    /// <summary>
    /// Знаходить шлях до виконуваного файлу Java в системі
    /// (Windows, Linux та macOS).
    /// </summary>
    /// <returns>Шлях до виконуваного файлу Java.</returns>
    /// <exception cref="InvalidOperationException">Виникає, якщо не вдалося знайти Java.</exception>
    private static string ResolveJavaPath()
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var executable = isWindows ? "java.exe" : DefaultJavaPath; // "java"

        // 1) JAVA_HOME/bin/java[.exe]
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome))
        {
            var javaPath = Path.Combine(javaHome, "bin", executable);
            if (File.Exists(javaPath))
                return javaPath;
        }

        // 2) Windows: типовий шлях Oracle javapath
        if (isWindows)
        {
            var oracleJavaPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Common Files", "Oracle", "Java", "javapath", "java.exe"
            );
            if (File.Exists(oracleJavaPath))
                return oracleJavaPath;
        }

        // 3) Пошук у PATH
        var onPath = ResolveOnPath(executable);
        if (onPath != null)
            return onPath;

        throw new InvalidOperationException(
            $"Не вдалося знайти Java для платформи {RuntimeInformation.OSDescription}. " +
            "Встановіть JDK 21+ і задайте JAVA_HOME, додайте java до PATH, " +
            "або вкажіть шлях явно через Config.JavaExecutable.");
    }

    /// <summary>
    /// Шукає виконуваний файл у каталогах змінної середовища PATH.
    /// </summary>
    /// <param name="executable">Назва виконуваного файлу (наприклад, "java" або "java.exe").</param>
    /// <returns>Повний шлях, якщо знайдено; інакше null.</returns>
    internal static string? ResolveOnPath(string executable)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar))
            return null;

        var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        foreach (var dir in pathVar.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), executable);
                if (File.Exists(candidate))
                    return candidate;
            }
            catch
            {
                // Ігноруємо некоректні елементи PATH
            }
        }

        return null;
    }
}
