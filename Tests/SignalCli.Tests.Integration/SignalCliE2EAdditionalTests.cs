using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SignalCli.Extensions;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models;
using SignalCli.Models.SignalCli;
using SignalCli.Services.SignalCli;

namespace SignalCli.Tests.Integration;

/// <summary>
/// audit-followup-2026 §5 (integration-tests-expansion) — лишковий cluster з 4 E2E tests,
/// що в [4.0.0] був deferred до 4.0.1 з причини "require real signal-cli runtime + CI
/// infrastructure". Тести skip-gated за тим самим механізмом що
/// <see cref="SignalCliE2EVersionTests"/> / <see cref="SignalCliE2EGracefulShutdownTests"/>:
/// якщо bundled JRE або native бінарник недоступний — рано return + Console.Error скип-маркер.
/// </summary>
[Trait("Category", "E2E")]
public class SignalCliE2EAdditionalTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Shared helpers — той самий skip-gate як у `SignalCliE2EGracefulShutdownTests`.
    // ─────────────────────────────────────────────────────────────────────────────

    private static IHost BuildHostWithBundledDefaults(
        Action<SignalCliOptions>? extraConfigure = null,
        string? storageSuffix = null)
    {
        var baseDir = AppContext.BaseDirectory;
        return Host.CreateDefaultBuilder().ConfigureServices(services =>
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
                cfg.StopTimeoutSeconds = 5;
                cfg.MaxRestartAttempts = 1;
                cfg.HealthCheckIntervalSeconds = 2;
                cfg.HealthCheckTimeoutSeconds = 5;
                cfg.StoragePathCli = Path.Combine(
                    Path.GetTempPath(),
                    $"SignalCliE2E-{storageSuffix ?? "default"}-" + Guid.NewGuid());
                extraConfigure?.Invoke(cfg);
            });
        }).Build();
    }

    private static bool IsRuntimeAvailable(SignalCliOptions cfg, out string skipReason)
    {
        skipReason = string.Empty;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (string.IsNullOrEmpty(cfg.SignalCliExecutable) || !File.Exists(cfg.SignalCliExecutable))
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

    private static SignalCliOptions GetOptions(IHost host) =>
        host.Services.GetRequiredService<IOptions<SignalCliOptions>>().Value;

    private static async Task CleanupStorage(SignalCliOptions cfg)
    {
        await Task.Yield();
        try { Directory.Delete(cfg.StoragePathCli!, recursive: true); } catch { /* best-effort */ }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // E2E #1 — Process_StartStopRestart_TransitionsObservedCorrectly
    // Asserts: start → Running → Stop → Stopped → Start → Running cycle.
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Process_StartStopRestart_TransitionsObservedCorrectly()
    {
        using var host = BuildHostWithBundledDefaults(storageSuffix: "StartStop");
        var cfg = GetOptions(host);
        if (!IsRuntimeAvailable(cfg, out var skipReason))
        {
            Console.Error.WriteLine($"[SKIP] {skipReason}");
            return;
        }

        try
        {
            using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(120));

            var sm = host.Services.GetRequiredService<ProcessStateManager>();
            var hosted = host.Services.GetRequiredService<SignalCli.Services.SignalCli.SignalCliHostedService>();

            // Перший Start.
            await host.StartAsync(ct.Token);
            await hosted.WaitForReadyAsync(ct.Token);
            Assert.Equal(ProcessState.Running, sm.CurrentState);

            // Stop. Не використовуємо host.StopAsync (зупинить ВСІ hosted-сервіси
            // включно з JsonRpcClientHostedService — рестарт буде складніший); зупиняємо
            // саме SignalCliHostedService через його ForceRestartAsync який робить
            // stop + start цикл.
            var pid1 = hosted.CurrentProcessForTests?.Id;
            Assert.NotNull(pid1);

            await hosted.ForceRestartAsync(ct.Token);
            await hosted.WaitForReadyAsync(ct.Token);
            Assert.Equal(ProcessState.Running, sm.CurrentState);

            var pid2 = hosted.CurrentProcessForTests?.Id;
            Assert.NotNull(pid2);
            Assert.NotEqual(pid1, pid2);

            await host.StopAsync(TimeSpan.FromSeconds(15));
        }
        finally { await CleanupStorage(cfg); }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // E2E #2 — Process_KilledExternally_AutoRestartReclaimsProcess
    // Externally Kill() the real signal-cli child, assert OnProcessExited fires
    // auto-restart and the new process answers `version`.
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Process_KilledExternally_AutoRestartReclaimsProcess()
    {
        using var host = BuildHostWithBundledDefaults(
            cfg => { cfg.MaxRestartAttempts = 2; cfg.RestartDelaySeconds = 0; },
            storageSuffix: "KilledExt");
        var cfg = GetOptions(host);
        if (!IsRuntimeAvailable(cfg, out var skipReason))
        {
            Console.Error.WriteLine($"[SKIP] {skipReason}");
            return;
        }

        try
        {
            using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(180));

            await host.StartAsync(ct.Token);
            var hosted = host.Services.GetRequiredService<SignalCli.Services.SignalCli.SignalCliHostedService>();
            await hosted.WaitForReadyAsync(ct.Token);

            var pid1 = hosted.CurrentProcessForTests?.Id;
            Assert.NotNull(pid1);

            // External kill — на Win/Linux/macOS Process.Kill(entireProcessTree:false)
            // достатньо щоб OnProcessExited fired.
            using (var realProc = Process.GetProcessById(pid1!.Value))
                realProc.Kill(entireProcessTree: false);

            // Чекаємо до 60с щоб watchdog tick'нув і підняв новий процес.
            var sw = Stopwatch.StartNew();
            int? pid2 = null;
            while (sw.Elapsed < TimeSpan.FromSeconds(60))
            {
                await Task.Delay(500, ct.Token);
                pid2 = hosted.CurrentProcessForTests?.Id;
                if (pid2 is not null && pid2 != pid1) break;
            }

            Assert.NotNull(pid2);
            Assert.NotEqual(pid1, pid2);

            // Новий процес має відповідати на version.
            var client = host.Services.GetRequiredService<ISignalCliClient>();
            var version = await client.InvokeMethodAsync(
                "version",
                new VersionParameters(),
                SignalCli.Serialization.SignalJsonContext.Default.VersionParameters,
                SignalCli.Serialization.SignalJsonContext.Default.VersionResponse,
                ct.Token);
            Assert.False(string.IsNullOrEmpty(version.Version));

            await host.StopAsync(TimeSpan.FromSeconds(15));
        }
        finally { await CleanupStorage(cfg); }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // E2E #3 — HealthMonitor_OverInterval_LastPingResultUpdates
    // HealthCheckIntervalSeconds=2; wait 6s, assert LastPingResult.Ok==true and
    // timestamp is fresh.
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HealthMonitor_OverInterval_LastPingResultUpdates()
    {
        using var host = BuildHostWithBundledDefaults(
            cfg => { cfg.HealthCheckIntervalSeconds = 2; cfg.HealthCheckTimeoutSeconds = 5; },
            storageSuffix: "HealthMon");
        var cfg = GetOptions(host);
        if (!IsRuntimeAvailable(cfg, out var skipReason))
        {
            Console.Error.WriteLine($"[SKIP] {skipReason}");
            return;
        }

        try
        {
            using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            await host.StartAsync(ct.Token);
            var hosted = host.Services.GetRequiredService<SignalCli.Services.SignalCli.SignalCliHostedService>();
            await hosted.WaitForReadyAsync(ct.Token);

            var monitor = host.Services.GetRequiredService<SignalCliHealthMonitor>();

            // Чекаємо ~6 wall-clock секунд — мінімум 2 ping-ticks за HealthCheckIntervalSeconds=2.
            await Task.Delay(TimeSpan.FromSeconds(6), ct.Token);

            var snapshot = monitor.LastPingResult;
            Assert.NotNull(snapshot);
            var (ok, at) = snapshot.Value;
            Assert.True(ok, $"Health-ping не успішний: Ok={ok}, At={at:o}");

            var age = DateTimeOffset.UtcNow - at;
            Assert.True(age < TimeSpan.FromSeconds(10),
                $"LastPingResult.At ({at:o}) старіший за 10s — health-monitor не tick'ає.");

            await host.StopAsync(TimeSpan.FromSeconds(15));
        }
        finally { await CleanupStorage(cfg); }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // E2E #4 — DisposeAsync_MidFlight_LeavesNoOrphanProcess
    // Start → fire VersionAsync → immediately await host.DisposeAsync() →
    // assert child signal-cli process is no longer alive.
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_MidFlight_LeavesNoOrphanProcess()
    {
        var host = BuildHostWithBundledDefaults(storageSuffix: "MidFlight");
        var cfg = GetOptions(host);
        if (!IsRuntimeAvailable(cfg, out var skipReason))
        {
            Console.Error.WriteLine($"[SKIP] {skipReason}");
            host.Dispose();
            return;
        }

        int? pid = null;
        try
        {
            using var startCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await host.StartAsync(startCts.Token);
            var hosted = host.Services.GetRequiredService<SignalCli.Services.SignalCli.SignalCliHostedService>();
            await hosted.WaitForReadyAsync(startCts.Token);
            pid = hosted.CurrentProcessForTests?.Id;
            Assert.NotNull(pid);

            // Fire-and-don't-await: викликаємо version паралельно з DisposeAsync.
            var client = host.Services.GetRequiredService<ISignalCliClient>();
            var versionTask = client.InvokeMethodAsync(
                "version",
                new VersionParameters(),
                SignalCli.Serialization.SignalJsonContext.Default.VersionParameters,
                SignalCli.Serialization.SignalJsonContext.Default.VersionResponse,
                CancellationToken.None);

            // Йдемо у DisposeAsync, не чекаючи на versionTask.
            if (host is IAsyncDisposable iad) await iad.DisposeAsync(); else host.Dispose();

            // versionTask може успішно вернутися ДО Dispose чи бути cancel'нутий — обидва ок.
            try { await versionTask; } catch (OperationCanceledException) { /* expected */ }
            catch (ObjectDisposedException) { /* expected */ }
        }
        finally
        {
            // Перевіряємо що OS-процес зник; race-window дозволяємо 5с.
            if (pid is { } realPid)
            {
                var sw = Stopwatch.StartNew();
                bool alive = true;
                while (sw.Elapsed < TimeSpan.FromSeconds(5))
                {
                    try
                    {
                        using var p = Process.GetProcessById(realPid);
                        alive = !p.HasExited;
                        if (!alive) break;
                    }
                    catch (ArgumentException)
                    {
                        alive = false;
                        break;
                    }
                    await Task.Delay(200);
                }
                Assert.False(alive, $"PID {realPid} orphaned after DisposeAsync — process tree не cleaned up.");
            }
            await CleanupStorage(cfg);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // E2E #5 — Configuration_FromIConfiguration_StartsRealCli
    // Uses AddSignalCli(IConfiguration) overload — validates that the new
    // `configuration-binder-aot-completion` path works end-to-end against real
    // signal-cli (source-gen-intercepted binding, no reflection).
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Configuration_FromIConfiguration_StartsRealCli()
    {
        var baseDir = AppContext.BaseDirectory;
        var storageDir = Path.Combine(Path.GetTempPath(), "SignalCliE2E-IConf-" + Guid.NewGuid());

        // Builds the same options as bundled-defaults but через IConfiguration overload —
        // в реальному світі це read-from-appsettings.json. Тут — InMemoryCollection.
        // Resolve JavaExecutable manually бо bundled JavaPathResolver працює лише через
        // AddSignalCliWithBundledRuntimeDefaults. Без runtime — early skip.
        SignalCliOptions probe;
        {
            using var probeHost = BuildHostWithBundledDefaults(storageSuffix: "IConfProbe");
            probe = GetOptions(probeHost);
        }
        if (!IsRuntimeAvailable(probe, out var skipReason))
        {
            Console.Error.WriteLine($"[SKIP] {skipReason}");
            try { Directory.Delete(storageDir, true); } catch { /* best-effort */ }
            return;
        }

        var settings = new Dictionary<string, string?>
        {
            ["SignalCli:AppHome"] = baseDir,
            ["SignalCli:LibDirectory"] = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? string.Empty
                : "signal-cli/lib",
            ["SignalCli:RequestTimeoutSeconds"] = "30",
            ["SignalCli:StopTimeoutSeconds"] = "5",
            ["SignalCli:MaxRestartAttempts"] = "0",
            ["SignalCli:HealthCheckIntervalSeconds"] = "30",
            ["SignalCli:HealthCheckTimeoutSeconds"] = "10",
            ["SignalCli:StoragePathCli"] = storageDir,
        };
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            settings["SignalCli:SignalCliExecutable"] = probe.SignalCliExecutable;
        else
            settings["SignalCli:JavaExecutable"] = probe.JavaExecutable;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSignalCli(configuration.GetSection("SignalCli"));
            })
            .Build();

        try
        {
            using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await host.StartAsync(ct.Token);

            var hosted = host.Services.GetRequiredService<SignalCli.Services.SignalCli.SignalCliHostedService>();
            await hosted.WaitForReadyAsync(ct.Token);

            var client = host.Services.GetRequiredService<ISignalCliClient>();
            var version = await client.InvokeMethodAsync(
                "version",
                new VersionParameters(),
                SignalCli.Serialization.SignalJsonContext.Default.VersionParameters,
                SignalCli.Serialization.SignalJsonContext.Default.VersionResponse,
                ct.Token);

            Assert.False(string.IsNullOrEmpty(version.Version));
            Assert.Contains("0.14", version.Version);

            await host.StopAsync(TimeSpan.FromSeconds(15));
        }
        finally
        {
            try { Directory.Delete(storageDir, recursive: true); } catch { /* best-effort */ }
        }
    }
}
