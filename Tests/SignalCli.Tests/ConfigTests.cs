using SignalCli.Models;

namespace SignalCli.Tests;

public class ConfigTests
{
    [Fact]
    public void ToProcessConfig_ThrowsFileNotFoundException_IfNoJarFiles()
    {
        // 1) Создаём временную папку (можно через Path.GetTempPath() + Guid)
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        // 2) Настраиваем Config так, чтобы AppHome указывал на tempDir,
        //    а LibDirectory = "lib". Получим:  /tmp/xxx/lib
        // JavaExecutable заданий -> JVM-режим -> ToProcessConfig викликає BuildClasspath
        var config = new Config
        {
            AppHome = tempDir,
            LibDirectory = "lib",
            JavaExecutable = "java"
        };

        // Создадим папку lib, но НЕ кладём туда *.jar-файлы
        Directory.CreateDirectory(Path.Combine(tempDir, "lib"));

        // 3) Проверяем, что при вызове ToProcessConfig -> BuildClasspath будет FileNotFoundException
        Assert.Throws<FileNotFoundException>(() => config.ToProcessConfig());

        // 4) Очистка временной директории (в реальном коде убрать в Dispose/Finally)
        Directory.Delete(tempDir, recursive: true);
    }
    [Fact]
    public void ToProcessConfig_BuildsClassPath_IfJarFilesFound()
    {
        // 1) Создаём временную папку
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        // 2) Настраиваем Config
        var config = new Config
        {
            AppHome = tempDir,
            LibDirectory = "lib",
            JavaExecutable = "java" // пусть не важно что
        };

        // 3) Создаём папку lib
        var libDir = Path.Combine(tempDir, "lib");
        Directory.CreateDirectory(libDir);

        // 4) "Подкладываем" фейковый jar-файл (пусть даже пустой)
        var fakeJarPath = Path.Combine(libDir, "test1.jar");
        File.WriteAllText(fakeJarPath, "fake jar content");

        // 5) Вызываем ToProcessConfig и убеждаемся, что не бросает исключения
        var processConfig = config.ToProcessConfig();

        // 6) Аргументи передаються через ArgumentList (безпечне екранування)
        Assert.NotNull(processConfig.ArgumentList);
        Assert.Contains("-classpath", processConfig.ArgumentList!);
        Assert.Contains(processConfig.ArgumentList!, a => a.Contains("test1.jar"));

        // Дополнительно можно проверить другие части аргументов: jsonRpc, log-file и т.д.

        // 7) Очистка
        Directory.Delete(tempDir, recursive: true);
    }

    [Fact]
    public void ToProcessConfig_NativeMode_LaunchesBinaryDirectlyWithoutJava()
    {
        // Native-режим: задано лише SignalCliExecutable, JAR-файлів і Java немає
        var config = new Config
        {
            AppHome = Path.GetTempPath(),
            SignalCliExecutable = "/opt/signal-cli/bin/signal-cli"
        };

        var pc = config.ToProcessConfig();

        Assert.Equal("/opt/signal-cli/bin/signal-cli", pc.Executable);
        Assert.NotNull(pc.ArgumentList);
        // Жодного java/classpath/Main — бінарник запускається напряму
        Assert.DoesNotContain("-classpath", pc.ArgumentList!);
        Assert.DoesNotContain("org.asamk.signal.Main", pc.ArgumentList!);
        Assert.Contains("jsonRpc", pc.ArgumentList!);
        Assert.Contains(pc.ArgumentList!, a => a.StartsWith("--receive-mode="));
    }

    [Fact]
    public void ToProcessConfig_NoJavaAndNoNative_Throws()
    {
        var config = new Config { AppHome = Path.GetTempPath() };
        Assert.Throws<InvalidOperationException>(() => config.ToProcessConfig());
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