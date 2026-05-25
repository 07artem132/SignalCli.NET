using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SignalCli.Extensions;
using SignalCli.Interfaces.Signal;
using SignalCli.Models;

namespace SignalCli.Tests.Integration;

/// <summary>
/// signal-cli-api-coverage Wave 8: env-gated E2E для read-only utility methods.
/// </summary>
[Trait("Category", "E2E")]
public class SignalCliE2EUtilityRpcTests
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
                cfg.StoragePathCli = Path.Combine(Path.GetTempPath(), "SignalCliE2EUtilityRpc-" + Guid.NewGuid());
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
    public async Task GetUserStatus_Self_ReturnsRegistered()
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

            var accounts = host.Services.GetRequiredService<ISignalAccounts>();

            // Перевіряємо власний номер — він мусить бути registered.
            var resp = await accounts.GetUserStatusAsync(account, recipients: [account], cancellationToken: startCts.Token);

            Assert.NotNull(resp);
            Assert.Single(resp);
            Assert.True(resp[0].IsRegistered);
            Assert.NotNull(resp[0].Uuid);

            await host.StopAsync(TimeSpan.FromSeconds(15));
        }
        finally
        {
            host.Dispose();
        }
    }
}
