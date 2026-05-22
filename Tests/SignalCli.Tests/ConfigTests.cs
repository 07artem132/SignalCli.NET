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
        var config = new Config
        {
            AppHome = tempDir,
            LibDirectory = "lib"
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
    
        // 6) Проверяем, что processConfig.Arguments содержит "-classpath \"...test1.jar\""
        Assert.Contains("-classpath", processConfig.Arguments);
        Assert.Contains("test1.jar", processConfig.Arguments);

        // Дополнительно можно проверить другие части аргументов: jsonRpc, log-file и т.д.

        // 7) Очистка
        Directory.Delete(tempDir, recursive: true);
    }
}