using SignalCli.Models;

namespace SignalCli.Tests;

/// <summary>
/// deprecated-shim-removal §5: міграція з ConfigTests.ToProcessConfig_* tests на пряму
/// SignalCliOptions.ToProcessConfig (внутрішня extension). Assertions verbatim — лише SUT-тип
/// змінено з Config на SignalCliOptions.
/// </summary>
/// <remarks>
/// Тест classpath-кешування ВИДАЛЕНО — кеш видалено разом з Config-типом (одне-два
/// `Directory.GetFiles` на life-cycle процесу не варті state на public-options-type'і).
/// </remarks>
public class SignalCliOptionsExtensionsTests
{
    [Fact]
    public void ToProcessConfig_ThrowsFileNotFoundException_IfNoJarFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var options = new SignalCliOptions
            {
                AppHome = tempDir,
                LibDirectory = "lib",
                JavaExecutable = "java"
            };
            Directory.CreateDirectory(Path.Combine(tempDir, "lib"));

            Assert.Throws<FileNotFoundException>(() => options.ToProcessConfig());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ToProcessConfig_BuildsClassPath_IfJarFilesFound()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var options = new SignalCliOptions
            {
                AppHome = tempDir,
                LibDirectory = "lib",
                JavaExecutable = "java"
            };
            var libDir = Path.Combine(tempDir, "lib");
            Directory.CreateDirectory(libDir);
            File.WriteAllText(Path.Combine(libDir, "test1.jar"), "fake jar");

            var processConfig = options.ToProcessConfig();
            Assert.NotNull(processConfig.ArgumentList);
            Assert.Contains("-classpath", processConfig.ArgumentList!);
            Assert.Contains(processConfig.ArgumentList!, a => a.Contains("test1.jar"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ToProcessConfig_NativeMode_LaunchesBinaryDirectlyWithoutJava()
    {
        var options = new SignalCliOptions
        {
            AppHome = Path.GetTempPath(),
            JavaExecutable = string.Empty,
            LibDirectory = string.Empty,
            SignalCliExecutable = "/opt/signal-cli/bin/signal-cli"
        };

        var pc = options.ToProcessConfig();

        Assert.Equal("/opt/signal-cli/bin/signal-cli", pc.Executable);
        Assert.NotNull(pc.ArgumentList);
        Assert.DoesNotContain("-classpath", pc.ArgumentList!);
        Assert.DoesNotContain("org.asamk.signal.Main", pc.ArgumentList!);
        Assert.Contains("jsonRpc", pc.ArgumentList!);
        Assert.Contains(pc.ArgumentList!, a => a.StartsWith("--receive-mode=", StringComparison.Ordinal));
    }

    [Fact]
    public void ToProcessConfig_NoJavaAndNoNative_Throws()
    {
        var options = new SignalCliOptions
        {
            AppHome = Path.GetTempPath(),
            JavaExecutable = string.Empty,
            LibDirectory = string.Empty
        };
        Assert.Throws<InvalidOperationException>(() => options.ToProcessConfig());
    }

    [Fact]
    public void ToProcessConfig_AppHomeChangeAffectsLogAndStoragePaths()
    {
        // F11: LogFileCli/StoragePathCli обчислюються від AppHome at-call-time, не from-baseline.
        var customHome = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(customHome);
        try
        {
            var libDir = Path.Combine(customHome, "lib");
            Directory.CreateDirectory(libDir);
            File.WriteAllText(Path.Combine(libDir, "test1.jar"), "fake");

            var options = new SignalCliOptions
            {
                AppHome = customHome,
                LibDirectory = "lib",
                JavaExecutable = "java"
            };

            var pc = options.ToProcessConfig();

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
            var options = new SignalCliOptions
            {
                AppHome = tempDir,
                LibDirectory = "lib",
                JavaExecutable = "java",
                StoragePathCli = storage
            };

            var processConfig = options.ToProcessConfig();

            Assert.NotNull(processConfig.ArgumentList);
            Assert.Contains($"--config={storage}", processConfig.ArgumentList!);
            Assert.Contains("org.asamk.signal.Main", processConfig.ArgumentList!);
            Assert.Contains("jsonRpc", processConfig.ArgumentList!);
            Assert.True(string.IsNullOrEmpty(processConfig.Arguments));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
