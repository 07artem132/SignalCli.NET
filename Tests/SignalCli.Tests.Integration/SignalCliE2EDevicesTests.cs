using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SignalCli.Extensions;
using SignalCli.Interfaces.Signal;
using SignalCli.Models;

namespace SignalCli.Tests.Integration;

/// <summary>
/// signal-cli-api-coverage Wave 5: env-gated E2E test проти живого signal-cli daemon'у.
/// Read-only (ListDevices) — безпечно ділить account між test-runs.
/// </summary>
[Trait("Category", "E2E")]
public class SignalCliE2EDevicesTests
{
    private static IHost? TryBuildHost(string account, out string skipReason)
    {
        var baseDir = AppContext.BaseDirectory;
        skipReason = string.Empty;

        var hostBuilder = Host.CreateDefaultBuilder().ConfigureServices(services =>
        {
            services.AddSignalCliWithBundledRuntimeDefaults(cfg =>
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    cfg.SignalCliExecutable = Path.Combine(baseDir, "signal-cli-native", "signal-cli");
                    cfg.JavaExecutable = null;
                }
                else
                {
                    cfg.LibDirectory = "signal-cli/lib";
                }
                cfg.RequestTimeoutSeconds = 30;
                cfg.StopTimeoutSeconds = 3;
                cfg.MaxRestartAttempts = 0;
                cfg.StoragePathCli = Path.Combine(Path.GetTempPath(), "SignalCliE2EDevices-" + Guid.NewGuid());
            });
        });

        var host = hostBuilder.Build();
        var cfg2 = host.Services.GetRequiredService<IOptions<SignalCliOptions>>().Value;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (!File.Exists(cfg2.SignalCliExecutable))
            {
                skipReason = $"Нативний signal-cli не знайдено за шляхом {cfg2.SignalCliExecutable}";
                host.Dispose();
                return null;
            }
        }
        else
        {
            if (string.IsNullOrEmpty(cfg2.JavaExecutable) || !File.Exists(cfg2.JavaExecutable))
            {
                skipReason = $"Бандл JRE не знайдено (JavaExecutable='{cfg2.JavaExecutable}').";
                host.Dispose();
                return null;
            }
            var libDir = Path.Combine(cfg2.AppHome, cfg2.LibDirectory);
            if (!Directory.Exists(libDir) || Directory.GetFiles(libDir, "*.jar").Length == 0)
            {
                skipReason = $"Бандл signal-cli jars не знайдено в {libDir}";
                host.Dispose();
                return null;
            }
        }
        return host;
    }

    [Fact]
    public async Task ListDevices_ReturnsAtLeastSelf()
    {
        if (!TestAccountFixture.TryGetOrSkip(out var account, out var accountSkip))
        {
            Console.Error.WriteLine($"[SKIP] {accountSkip}");
            return;
        }

        var host = TryBuildHost(account, out var runtimeSkip);
        if (host is null)
        {
            Console.Error.WriteLine($"[SKIP] {runtimeSkip}");
            return;
        }

        try
        {
            using var startCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await host.StartAsync(startCts.Token);
            var hostedService = host.Services.GetRequiredService<SignalCli.Services.SignalCli.SignalCliHostedService>();
            await hostedService.WaitForReadyAsync(startCts.Token);

            var devices = host.Services.GetRequiredService<ISignalDevices>();
            var resp = await devices.ListDevicesAsync(account, startCts.Token);

            // Registered primary account мусить мати щонайменше себе (id=1).
            Assert.NotNull(resp);
            Assert.NotEmpty(resp);
            Assert.Contains(resp, d => d.Id == 1L); // primary завжди id=1

            // §F6 wire pinning: жодних "isThisDevice" полів.
            foreach (var d in resp)
            {
                Assert.True(d.Id > 0);
                // CreatedTimestamp — non-zero (linked at some point).
                Assert.True(d.CreatedTimestamp > 0);
            }

            await host.StopAsync(TimeSpan.FromSeconds(15));
        }
        finally
        {
            host.Dispose();
        }
    }
}
