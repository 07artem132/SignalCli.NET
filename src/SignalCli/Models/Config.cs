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
    /// Піддиректорія (відносно вихідної папки застосунку), куди пакети
    /// SignalCli.Runtime.Jre.* розпаковують вбудований JRE.
    /// </summary>
    private const string DefaultBundledJreDirectory = "jre";

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
    /// Максимальний час очікування на відповідь JSON-RPC запиту (у секундах).
    /// </summary>
    /// <remarks>
    /// Кожен запит обмежується цим таймаутом; за його перевищення таск
    /// фолтиться <see cref="TimeoutException"/>, що відрізняється від
    /// <see cref="OperationCanceledException"/> ініційованого викликачем.
    /// За замовчуванням 30 с.
    /// </remarks>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Вікно стабільності, після якого лічильник перезапусків скидається в 0 (у секундах).
    /// </summary>
    /// <remarks>
    /// Якщо процес залишається у стані Running протягом цього інтервалу,
    /// "бюджет" перезапусків (<see cref="MaxRestartAttempts"/>) відновлюється
    /// — щоб поодинокі транзитивні збої за тривалу роботу не вимикали авто-рестарт назавжди.
    /// За замовчуванням 60 с.
    /// </remarks>
    public int RestartWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Головна директорія програми.
    /// </summary>
    /// <remarks>
    /// Директорія, де розташовані піддиректорії /config, /log, /lib та інші.
    /// </remarks>
    public required string AppHome { get; set; }

    /// <summary>
    /// Рівень логування для Signal CLI.
    /// </summary>
    public CliLogLevel CliLogLevelCli { get; set; } = CliLogLevel.Info;

    /// <summary>
    /// Шлях до файлу логу Signal CLI.
    /// </summary>
    /// <remarks>
    /// За замовчуванням (через <see cref="CreateDefault"/>) обчислюється
    /// як <c>Path.Combine(AppHome, "signal.log")</c> — змінення <see cref="AppHome"/>
    /// автоматично переносить лог-файл.
    /// </remarks>
    public string? LogFileCli { get; set; }

    /// <summary>
    /// Шлях до директорії з даними Signal CLI.
    /// </summary>
    /// <remarks>
    /// За замовчуванням (через <see cref="CreateDefault"/>) обчислюється
    /// як <c>Path.Combine(AppHome, "SignalCliStorageData")</c> — змінення
    /// <see cref="AppHome"/> автоматично переносить сховище даних.
    /// </remarks>
    public string? StoragePathCli { get; set; }

    /// <summary>
    /// Шлях до виконуваного файлу Java.
    /// </summary>
    public required string JavaExecutable { get; set; }

    /// <summary>
    /// Шлях до нативного (GraalVM) виконуваного файлу signal-cli.
    /// </summary>
    /// <remarks>
    /// Якщо задано, бібліотека запускає цей бінарник напряму і <b>Java не потрібна</b>
    /// (режим native). Інакше використовується JVM-режим через <see cref="JavaExecutable"/>
    /// та classpath із JAR-файлів. Нативні білди signal-cli офіційно доступні лише для Linux x64.
    /// </remarks>
    public string? SignalCliExecutable { get; set; }

    /// <summary>
    /// Піддиректорія з JAR-файлами Signal CLI.
    /// </summary>
    public required string LibDirectory { get; set; }

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
        var receiveModeArg = UseManualReceiveMode
            ? "--receive-mode=manual"
            : "--receive-mode=on-start";
        var logLevelArg = CliLogLevelCli switch
        {
            CliLogLevel.Info => "-v",
            CliLogLevel.Debug => "-vv",
            CliLogLevel.Verbose => "-vvv",
            _ => ""
        };

        // Власні аргументи signal-cli — однакові в обох режимах.
        var signalCliArgs = new List<string>();
        if (!string.IsNullOrEmpty(logLevelArg))
            signalCliArgs.Add(logLevelArg);
        // F11: якщо користувач не задав свої шляхи — обчислюємо від AppHome,
        // щоб зміна AppHome завжди мала ефект (не лишався baseDirectory часів CreateDefault).
        var logFile = string.IsNullOrEmpty(LogFileCli)
            ? Path.Combine(AppHome, "signal.log")
            : LogFileCli;
        var storagePath = string.IsNullOrEmpty(StoragePathCli)
            ? Path.Combine(AppHome, "SignalCliStorageData")
            : StoragePathCli;
        signalCliArgs.Add($"--log-file={logFile}");
        signalCliArgs.Add($"--config={storagePath}");
        signalCliArgs.Add("jsonRpc");
        signalCliArgs.Add(receiveModeArg);

        string executable;
        List<string> argumentList;

        if (!string.IsNullOrEmpty(SignalCliExecutable))
        {
            // NATIVE-режим: запускаємо нативний бінарник напряму, Java не потрібна.
            executable = SignalCliExecutable;
            argumentList = signalCliArgs;
        }
        else if (!string.IsNullOrEmpty(JavaExecutable))
        {
            // JVM-режим: java -classpath <jars> org.asamk.signal.Main <signalCliArgs>.
            // Кожен аргумент — окремий елемент: ArgumentList сам екранує пробіли/лапки.
            executable = JavaExecutable;
            argumentList = ["-classpath", BuildClasspath(), "org.asamk.signal.Main", .. signalCliArgs];
        }
        else
        {
            throw new InvalidOperationException(
                "Не задано спосіб запуску signal-cli: вкажіть SignalCliExecutable (native, без Java) " +
                "або JavaExecutable (+ JAR-файли у LibDirectory для JVM-режиму).");
        }

        return new ProcessConfig
        {
            Executable = executable,
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
            // Java резолвимо «м'яко»: якщо її немає, лишаємо порожнім — користувач
            // може працювати в native-режимі (SignalCliExecutable). Помилка про
            // відсутність способу запуску виникне пізніше, у ToProcessConfig.
            JavaExecutable = TryResolveJavaPath(),
            LibDirectory = DefaultLibDirectory,
            MaxRestartAttempts = 3,
            RestartDelaySeconds = 5
        };
    }

    /// <summary>
    /// Намагається знайти Java; повертає порожній рядок, якщо її немає (без винятку).
    /// </summary>
    private static string TryResolveJavaPath()
    {
        try
        {
            return ResolveJavaPath();
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
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

        // 0) Вбудований JRE із пакета SignalCli.Runtime.Jre.* (Java не потрібна в системі).
        var bundled = ResolveBundledJava(AppDomain.CurrentDomain.BaseDirectory);
        if (bundled != null)
            return bundled;

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
            "Встановіть JDK 25+ і задайте JAVA_HOME, додайте java до PATH, " +
            "вкажіть шлях явно через Config.JavaExecutable, або підключіть пакет " +
            "SignalCli.Runtime.Jre.* зі вбудованим JRE (Java в системі не потрібна).");
    }

    /// <summary>
    /// Шукає вбудований JRE, який пакети SignalCli.Runtime.Jre.* розпаковують
    /// у піддиректорію <c>jre/</c> вихідної папки застосунку.
    /// </summary>
    /// <param name="baseDirectory">Вихідна (базова) директорія застосунку.</param>
    /// <returns>Повний шлях до <c>jre/bin/java[.exe]</c>, якщо знайдено; інакше null.</returns>
    internal static string? ResolveBundledJava(string baseDirectory)
    {
        if (string.IsNullOrEmpty(baseDirectory))
            return null;

        var executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "java.exe"
            : DefaultJavaPath; // "java"

        var candidate = Path.Combine(baseDirectory, DefaultBundledJreDirectory, "bin", executable);
        return File.Exists(candidate) ? candidate : null;
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
