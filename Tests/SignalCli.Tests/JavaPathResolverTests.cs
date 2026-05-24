using System.Runtime.InteropServices;
using SignalCli.Utilities;

namespace SignalCli.Tests;

/// <summary>
/// deprecated-shim-removal §5: міграція з ConfigTests/ConfigResolveOnPathTests на новий
/// JavaPathResolver. Assertions verbatim — лише SUT-тип змінено з Config.Resolve* на
/// JavaPathResolver.TryResolve*.
/// </summary>
public class JavaPathResolverTests
{
    private static string BundledJavaExecutableName =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : "java";

    [Fact]
    public void TryResolveBundledJre_ReturnsPath_WhenBundledJrePresent()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var binDir = Path.Combine(baseDir, "jre", "bin");
        Directory.CreateDirectory(binDir);
        var javaPath = Path.Combine(binDir, BundledJavaExecutableName);
        File.WriteAllText(javaPath, "fake java");
        try
        {
            Assert.Equal(javaPath, JavaPathResolver.TryResolveBundledJre(baseDir));
        }
        finally
        {
            Directory.Delete(baseDir, recursive: true);
        }
    }

    [Fact]
    public void TryResolveBundledJre_ReturnsNull_WhenNoBundledJre()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(baseDir);
        try
        {
            Assert.Null(JavaPathResolver.TryResolveBundledJre(baseDir));
        }
        finally
        {
            Directory.Delete(baseDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void TryResolveBundledJre_ReturnsNull_ForEmptyBaseDirectory(string? baseDir)
    {
        Assert.Null(JavaPathResolver.TryResolveBundledJre(baseDir!));
    }

    [Fact]
    public void TryResolveOnPath_ReturnsPath_WhenExecutableInPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var executableName = BundledJavaExecutableName;
        var executablePath = Path.Combine(tempDir, executableName);
        File.WriteAllText(executablePath, "fake");

        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
            Environment.SetEnvironmentVariable("PATH", tempDir + separator + originalPath);
            Assert.Equal(executablePath, JavaPathResolver.TryResolveOnPath(executableName));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryResolveOnPath_ReturnsNull_WhenNotFound()
    {
        Assert.Null(JavaPathResolver.TryResolveOnPath($"definitely-not-an-executable-{Guid.NewGuid()}.bin"));
    }
}
