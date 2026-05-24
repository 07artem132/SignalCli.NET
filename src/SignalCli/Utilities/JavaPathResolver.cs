using System.Runtime.InteropServices;

namespace SignalCli.Utilities;

/// <summary>
/// deprecated-shim-removal §1 (config-auto-resolve-migration): bundled-JRE / JAVA_HOME / PATH
/// lookup logic, мігровано з legacy <c>Models.Config</c>. Static internal — щоб AOT-shape
/// був reflection-free і consumer'и не могли випадково розширити цю логіку. Використовується
/// extension'ом <see cref="Extensions.ServiceCollectionExtensions.AddSignalCliWithBundledRuntimeDefaults"/>.
/// </summary>
internal static class JavaPathResolver
{
    private const string DefaultJavaName = "java";
    private const string DefaultBundledJreDirectory = "jre";

    private static string JavaExecutableName =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : DefaultJavaName;

    /// <summary>
    /// Шукає вбудований JRE у <c>&lt;baseDirectory&gt;/jre/bin/java[.exe]</c> (розпакований
    /// пакетом <c>SignalCli.Runtime.Jre.*</c>). Повертає <c>null</c> якщо не знайдено.
    /// </summary>
    internal static string? TryResolveBundledJre(string baseDirectory)
    {
        if (string.IsNullOrEmpty(baseDirectory)) return null;
        var candidate = Path.Combine(baseDirectory, DefaultBundledJreDirectory, "bin", JavaExecutableName);
        return File.Exists(candidate) ? candidate : null;
    }

    /// <summary>
    /// Шукає виконуваний файл у каталогах змінної середовища <c>PATH</c>.
    /// Повертає перший знайдений шлях або <c>null</c>.
    /// </summary>
    internal static string? TryResolveOnPath(string executable)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar)) return null;

        var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        foreach (var dir in pathVar.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), executable);
                if (File.Exists(candidate)) return candidate;
            }
            catch
            {
                // Ігноруємо некоректні елементи PATH
            }
        }
        return null;
    }

    /// <summary>
    /// Soft-resolve Java у такому порядку: bundled JRE → JAVA_HOME/bin → Windows Oracle javapath
    /// → PATH. Якщо нічого не знайдено — повертає <see cref="string.Empty"/> (НЕ throw'ить),
    /// щоб consumer міг вибрати native-режим (<c>SignalCliExecutable</c>) без винятку.
    /// </summary>
    internal static string TryResolveJavaPath(string baseDirectory)
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var executable = JavaExecutableName;

        // 0) Bundled JRE
        var bundled = TryResolveBundledJre(baseDirectory);
        if (bundled != null) return bundled;

        // 1) JAVA_HOME/bin/java[.exe]
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome))
        {
            var javaPath = Path.Combine(javaHome, "bin", executable);
            if (File.Exists(javaPath)) return javaPath;
        }

        // 2) Windows: Oracle javapath
        if (isWindows)
        {
            var oracleJavaPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Common Files", "Oracle", "Java", "javapath", "java.exe");
            if (File.Exists(oracleJavaPath)) return oracleJavaPath;
        }

        // 3) PATH search
        var onPath = TryResolveOnPath(executable);
        if (onPath != null) return onPath;

        // No Java found — return empty so native-mode path stays valid.
        return string.Empty;
    }
}
