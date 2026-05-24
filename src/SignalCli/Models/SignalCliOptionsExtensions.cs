using System.Runtime.InteropServices;
using SignalCli.Models.SignalCli;

namespace SignalCli.Models;

/// <summary>
/// Internal-розширення для <see cref="SignalCliOptions"/> — побудова <see cref="ProcessConfig"/>.
/// </summary>
/// <remarks>
/// deprecated-shim-removal §5 (remove-config-type): уся логіка побудови signal-cli launch-command
/// (native vs JVM, classpath, env, args) живе тут напряму. Раніше делегувалося через
/// <c>Config.ToProcessConfig</c>; legacy Config-type видалено.
/// </remarks>
internal static class SignalCliOptionsExtensions
{
    /// <summary>
    /// Будує <see cref="ProcessConfig"/> для запуску signal-cli з типованих опцій.
    /// </summary>
    /// <exception cref="FileNotFoundException">JAR-файли не знайдено у JVM-режимі.</exception>
    /// <exception cref="InvalidOperationException">Ні JavaExecutable, ні SignalCliExecutable не задано.</exception>
    public static ProcessConfig ToProcessConfig(this SignalCliOptions options)
    {
        var receiveModeArg = options.UseManualReceiveMode
            ? "--receive-mode=manual"
            : "--receive-mode=on-start";
        var logLevelArg = options.CliLogLevelCli switch
        {
            CliLogLevel.Info => "-v",
            CliLogLevel.Debug => "-vv",
            CliLogLevel.Verbose => "-vvv",
            _ => ""
        };

        var signalCliArgs = new List<string>();
        if (!string.IsNullOrEmpty(logLevelArg))
            signalCliArgs.Add(logLevelArg);
        // F11: якщо споживач не задав свої шляхи — обчислюємо від AppHome.
        var logFile = string.IsNullOrEmpty(options.LogFileCli)
            ? Path.Combine(options.AppHome, "signal.log")
            : options.LogFileCli;
        var storagePath = string.IsNullOrEmpty(options.StoragePathCli)
            ? Path.Combine(options.AppHome, "SignalCliStorageData")
            : options.StoragePathCli;
        signalCliArgs.Add($"--log-file={logFile}");
        signalCliArgs.Add($"--config={storagePath}");
        signalCliArgs.Add("jsonRpc");
        signalCliArgs.Add(receiveModeArg);

        string executable;
        List<string> argumentList;

        if (!string.IsNullOrEmpty(options.SignalCliExecutable))
        {
            // NATIVE-режим: запускаємо нативний бінарник напряму, Java НЕ потрібна.
            executable = options.SignalCliExecutable;
            argumentList = signalCliArgs;
        }
        else if (!string.IsNullOrEmpty(options.JavaExecutable))
        {
            // JVM-режим: java -classpath <jars> org.asamk.signal.Main <signalCliArgs>.
            executable = options.JavaExecutable;
            argumentList = ["-classpath", BuildClasspath(options), "org.asamk.signal.Main", .. signalCliArgs];
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
            WorkingDirectory = options.AppHome,
            CreateNewProcessGroup = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            EnvironmentVariables = options.EnvironmentVariables
        };
    }

    /// <summary>
    /// Будує classpath-рядок для JVM-mode'у на основі JAR-файлів у <c>AppHome/LibDirectory</c>.
    /// </summary>
    /// <remarks>
    /// deprecated-shim-removal §5: classpath cache (раніше на Config-instance'і) видалено —
    /// <see cref="ToProcessConfig"/> викликається рідко (раз на startup + на restart), кеш
    /// не давав measurable виграшу. Якщо профайлинг колись покаже cost — додамо
    /// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/> keyed на (AppHome, LibDirectory).
    /// </remarks>
    private static string BuildClasspath(SignalCliOptions options)
    {
        var libPath = Path.Combine(options.AppHome, options.LibDirectory);
        var jarFiles = Directory.GetFiles(libPath, "*.jar");
        if (jarFiles.Length == 0)
        {
            throw new FileNotFoundException($"No JAR files found in {libPath}");
        }

        var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ";" : ":";
        return string.Join(separator, jarFiles);
    }
}
