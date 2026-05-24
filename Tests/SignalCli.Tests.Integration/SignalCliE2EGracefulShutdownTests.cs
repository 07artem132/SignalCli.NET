using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SignalCli.Extensions;
using SignalCli.Models;

namespace SignalCli.Tests.Integration;

/// <summary>
/// signal-cli-protocol-alignment §1 (graceful-shutdown-fix) + audit-followup-2026
/// integration-tests-expansion §5.4: end-to-end перевірка що StopAsync дійсно завершує
/// процес чисто (через stdin EOF), а НЕ через timeout-fallback з Kill(entireProcessTree).
///
/// Пре-fix: ми писали літеральне "exit" на stdin — signal-cli віддавав -32700 Parse error
/// і лишався живим → StopAsync завжди вистрілював ProcessKillTimeout → Kill. Post-fix:
/// stdin.Close() → EOF → signal-cli's reader-loop природньо завершується.
///
/// Skip-gate: той самий що в SignalCliE2EVersionTests — без bundled runtime тест mark'ається
/// як passed без виконання E2E-частини.
/// </summary>
[Trait("Category", "E2E")]
public class SignalCliE2EGracefulShutdownTests
{
    [Fact]
    public async Task Process_GracefulShutdown_ExitsWithoutKillTimeout()
    {
        var baseDir = AppContext.BaseDirectory;

        var hostBuilder = Host.CreateDefaultBuilder().ConfigureServices(services =>
        {
            // deprecated-shim-removal §1: AddSignalCliWithBundledRuntimeDefaults замінив
            // legacy Action<Config> overload.
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
                cfg.StopTimeoutSeconds = 5; // generous щоб graceful встиг завершитися
                cfg.MaxRestartAttempts = 0;
                cfg.StoragePathCli = Path.Combine(Path.GetTempPath(), "SignalCliE2EGraceful-" + Guid.NewGuid());
            });
        });

        using var host = hostBuilder.Build();

        var cfg2 = host.Services
            .GetRequiredService<IOptions<SignalCliOptions>>().Value;
        if (!IsRuntimeAvailable(cfg2, out var skipReason))
        {
            Console.Error.WriteLine($"[SKIP] {skipReason}");
            return;
        }

        try
        {
            using var startCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await host.StartAsync(startCts.Token);

            var hostedService = host.Services.GetRequiredService<SignalCli.Services.SignalCli.SignalCliHostedService>();
            await hostedService.WaitForReadyAsync(startCts.Token);

            // Capture child PID до Stop'у.
            var pid = hostedService.CurrentProcessForTests?.Id;
            Assert.NotNull(pid);

            // Stop — має пройти граційно через stdin.Close() → EOF.
            var stopStart = Stopwatch.GetTimestamp();
            await host.StopAsync(TimeSpan.FromSeconds(15));
            var stopDuration = Stopwatch.GetElapsedTime(stopStart);

            // Інваріант: stop тривав значно менше за StopTimeoutSeconds (5с). Якщо ми б
            // спадали на Kill-timeout, то ~5с+ overhead. Graceful EOF — sub-1s типово.
            Assert.True(stopDuration < TimeSpan.FromSeconds(4),
                $"Stop took {stopDuration.TotalSeconds:F2}s — суттєво довше за expected sub-1s graceful EOF. " +
                "Можливо regression до старого WriteLineAsync(\"exit\") flow з Kill-fallback.");

            // Перевірка що OS-процес дійсно завершився (within race-window).
            try
            {
                var proc = Process.GetProcessById(pid!.Value);
                Assert.True(proc.HasExited, $"PID {pid} still alive {stopDuration.TotalSeconds:F2}s after StopAsync.");
            }
            catch (ArgumentException)
            {
                // Process.GetProcessById кидає коли PID уже зник — це ОК (clean exit).
            }
        }
        finally
        {
            try { Directory.Delete(cfg2.StoragePathCli!, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    private static bool IsRuntimeAvailable(SignalCliOptions cfg, out string skipReason)
    {
        skipReason = string.Empty;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (!File.Exists(cfg.SignalCliExecutable))
            {
                skipReason = $"Native signal-cli не знайдено: {cfg.SignalCliExecutable}";
                return false;
            }
        }
        else
        {
            if (string.IsNullOrEmpty(cfg.JavaExecutable) || !File.Exists(cfg.JavaExecutable))
            {
                skipReason = $"Bundled JRE не знайдено (JavaExecutable='{cfg.JavaExecutable}')";
                return false;
            }
            var libDir = Path.Combine(cfg.AppHome, cfg.LibDirectory);
            if (!Directory.Exists(libDir) || Directory.GetFiles(libDir, "*.jar").Length == 0)
            {
                skipReason = $"signal-cli jars не знайдено в {libDir}";
                return false;
            }
        }
        return true;
    }
}
