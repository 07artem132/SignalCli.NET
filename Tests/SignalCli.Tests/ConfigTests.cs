using System.Runtime.InteropServices;
using SignalCli.Models;

namespace SignalCli.Tests;

public class ConfigTests
{
    private static string BundledJavaExecutableName =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : "java";

    [Fact]
    public void ResolveBundledJava_ReturnsPath_WhenBundledJrePresent()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var binDir = Path.Combine(baseDir, "jre", "bin");
        Directory.CreateDirectory(binDir);
        var javaPath = Path.Combine(binDir, BundledJavaExecutableName);
        File.WriteAllText(javaPath, "fake java");
        try
        {
            var resolved = Config.ResolveBundledJava(baseDir);

            Assert.Equal(javaPath, resolved);
        }
        finally
        {
            Directory.Delete(baseDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveBundledJava_ReturnsNull_WhenNoBundledJre()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(baseDir);
        try
        {
            Assert.Null(Config.ResolveBundledJava(baseDir));
        }
        finally
        {
            Directory.Delete(baseDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ResolveBundledJava_ReturnsNull_ForEmptyBaseDirectory(string? baseDir)
    {
        Assert.Null(Config.ResolveBundledJava(baseDir!));
    }

    [Fact]
    public void ToProcessConfig_ThrowsFileNotFoundException_IfNoJarFiles()
    {
        // 1) Створюємо тимчасову папку (через Path.GetTempPath() + Guid)
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        // 2) Налаштовуємо Config так, щоб AppHome вказував на tempDir,
        //    а LibDirectory = "lib". Отримаємо:  /tmp/xxx/lib
        // JavaExecutable заданий -> JVM-режим -> ToProcessConfig викликає BuildClasspath
        var config = new Config
        {
            AppHome = tempDir,
            LibDirectory = "lib",
            JavaExecutable = "java"
        };

        // Створимо папку lib, але НЕ кладемо туди *.jar-файли
        Directory.CreateDirectory(Path.Combine(tempDir, "lib"));

        // 3) Перевіряємо, що при виклику ToProcessConfig -> BuildClasspath буде FileNotFoundException
        Assert.Throws<FileNotFoundException>(() => config.ToProcessConfig());

        // 4) Очищення тимчасової директорії (у реальному коді винести у Dispose/Finally)
        Directory.Delete(tempDir, recursive: true);
    }
    [Fact]
    public void ToProcessConfig_BuildsClassPath_IfJarFilesFound()
    {
        // 1) Створюємо тимчасову папку
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        // 2) Налаштовуємо Config
        var config = new Config
        {
            AppHome = tempDir,
            LibDirectory = "lib",
            JavaExecutable = "java" // значення не важливе
        };

        // 3) Створюємо папку lib
        var libDir = Path.Combine(tempDir, "lib");
        Directory.CreateDirectory(libDir);

        // 4) "Підкладаємо" фейковий jar-файл (нехай навіть порожній)
        var fakeJarPath = Path.Combine(libDir, "test1.jar");
        File.WriteAllText(fakeJarPath, "fake jar content");

        // 5) Викликаємо ToProcessConfig і переконуємось, що він не кидає винятків
        var processConfig = config.ToProcessConfig();

        // 6) Аргументи передаються через ArgumentList (безпечне екранування)
        Assert.NotNull(processConfig.ArgumentList);
        Assert.Contains("-classpath", processConfig.ArgumentList!);
        Assert.Contains(processConfig.ArgumentList!, a => a.Contains("test1.jar"));

        // Додатково можна перевірити інші частини аргументів: jsonRpc, log-file тощо.

        // 7) Очищення
        Directory.Delete(tempDir, recursive: true);
    }

    [Fact]
    public void ToProcessConfig_NativeMode_LaunchesBinaryDirectlyWithoutJava()
    {
        // Native-режим: задано лише SignalCliExecutable, JAR-файлів і Java немає
        var config = new Config
        {
            AppHome = Path.GetTempPath(),
            JavaExecutable = string.Empty,
            LibDirectory = string.Empty,
            SignalCliExecutable = "/opt/signal-cli/bin/signal-cli"
        };

        var pc = config.ToProcessConfig();

        Assert.Equal("/opt/signal-cli/bin/signal-cli", pc.Executable);
        Assert.NotNull(pc.ArgumentList);
        // Жодного java/classpath/Main — бінарник запускається напряму
        Assert.DoesNotContain("-classpath", pc.ArgumentList!);
        Assert.DoesNotContain("org.asamk.signal.Main", pc.ArgumentList!);
        Assert.Contains("jsonRpc", pc.ArgumentList!);
        Assert.Contains(pc.ArgumentList!, a => a.StartsWith("--receive-mode=", StringComparison.Ordinal));
    }

    [Fact]
    public void ToProcessConfig_NoJavaAndNoNative_Throws()
    {
        // Жоден зі способів запуску не заданий (JavaExecutable порожній, SignalCliExecutable null).
        var config = new Config
        {
            AppHome = Path.GetTempPath(),
            JavaExecutable = string.Empty,
            LibDirectory = string.Empty
        };
        Assert.Throws<InvalidOperationException>(() => config.ToProcessConfig());
    }

    [Fact]
    public void CreateDefault_AppHomeChangeAffectsLogAndStoragePaths()
    {
        // F11: дефолти LogFileCli/StoragePathCli мають обчислюватися від AppHome
        // в момент виклику ToProcessConfig, а не зафіксованого baseDirectory часів CreateDefault.
        var customHome = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(customHome);
        try
        {
            var libDir = Path.Combine(customHome, "lib");
            Directory.CreateDirectory(libDir);
            File.WriteAllText(Path.Combine(libDir, "test1.jar"), "fake");

            var config = Config.CreateDefault();
            config.AppHome = customHome;
            config.LibDirectory = "lib";
            config.JavaExecutable = "java";
            // LogFileCli/StoragePathCli залишаємо null — мають обчислитися від AppHome.

            var pc = config.ToProcessConfig();

            Assert.NotNull(pc.ArgumentList);
            Assert.Contains($"--log-file={Path.Combine(customHome, "signal.log")}", pc.ArgumentList!);
            Assert.Contains($"--config={Path.Combine(customHome, "SignalCliStorageData")}", pc.ArgumentList!);
        }
        finally
        {
            Directory.Delete(customHome, recursive: true);
        }
    }

    [Fact]
    public void ToProcessConfig_PathsWithSpaces_StayAsSingleArguments()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dir with spaces " + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            var libDir = Path.Combine(tempDir, "lib");
            Directory.CreateDirectory(libDir);
            File.WriteAllText(Path.Combine(libDir, "test1.jar"), "fake");

            var storage = Path.Combine(tempDir, "storage data");
            var config = new Config
            {
                AppHome = tempDir,
                LibDirectory = "lib",
                JavaExecutable = "java",
                StoragePathCli = storage
            };

            var processConfig = config.ToProcessConfig();

            Assert.NotNull(processConfig.ArgumentList);
            // Шлях зі пробілами має бути єдиним аргументом (без розщеплення)
            Assert.Contains($"--config={storage}", processConfig.ArgumentList!);
            Assert.Contains("org.asamk.signal.Main", processConfig.ArgumentList!);
            Assert.Contains("jsonRpc", processConfig.ArgumentList!);
            // Класичний рядок Arguments не використовується
            Assert.True(string.IsNullOrEmpty(processConfig.Arguments));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}