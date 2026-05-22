using SignalCli.Models;

namespace SignalCli.Tests;

public class ConfigResolveOnPathTests
{
    [Fact]
    public void ResolveOnPath_WhenExecutableInPath_ReturnsFullPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        var exeName = "fake-java-" + Guid.NewGuid().ToString("N") + (OperatingSystem.IsWindows() ? ".exe" : "");
        var exePath = Path.Combine(dir, exeName);
        File.WriteAllText(exePath, "");

        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var sep = OperatingSystem.IsWindows() ? ';' : ':';
            Environment.SetEnvironmentVariable("PATH", dir + sep + originalPath);

            var resolved = Config.ResolveOnPath(exeName);

            Assert.Equal(exePath, resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ResolveOnPath_WhenNotPresent_ReturnsNull()
    {
        var resolved = Config.ResolveOnPath("definitely-not-an-executable-" + Guid.NewGuid().ToString("N"));
        Assert.Null(resolved);
    }
}
