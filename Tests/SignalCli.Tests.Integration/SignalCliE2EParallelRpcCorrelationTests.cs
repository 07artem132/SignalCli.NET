using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SignalCli.Extensions;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models;
using SignalCli.Models.SignalCli;

namespace SignalCli.Tests.Integration;

/// <summary>
/// e2e-coverage-expansion (audit v2.1 path-to-8): пінує CLAUDE.md
/// "signal-cli protocol behavior we depend on" §3 — *"Parallel request processing
/// → match by id, not by order"*. signal-cli's
/// <c>JsonRpcReader.java:58</c> використовує
/// <c>Executors.newVirtualThreadPerTaskExecutor()</c>, тож відповіді приходять
/// у execution-time-order, а не request-order. Наш
/// <see cref="SignalCli.Services.Rpc.JsonRpcClient"/> має
/// <c>_pendingRequests : ConcurrentDictionary&lt;string, TaskCompletionSource&gt;</c>
/// з id-based correlation — unit-тести покривають це через
/// <see cref="System.Reactive.Subjects.Subject{T}"/>-мок, але до сьогодні жоден
/// тест не запускав 10 паралельних RPC проти РЕАЛЬНОГО signal-cli virtual-thread
/// dispatcher'а.
///
/// Цей E2E закриває розрив. Strong claim: 10 паралельних
/// <c>VersionAsync</c>-викликів МУСЯТЬ завершитися РІВНО з тією самою version-strіngою
/// (signal-cli повертає одну version незалежно від concurrency). Якщо хтось у
/// майбутньому замінить <c>_pendingRequests</c> на <c>Queue&lt;TaskCompletionSource&gt;</c>
/// ("бо order preserves"), цей тест fail'не одразу — а unit-тести з in-order
/// <c>Subject</c>-моком цього не побачать.
///
/// Skip-gate: ідентичний до решти E2E-файлів (bundled-JRE на Win/macOS, native
/// signal-cli на Linux). Без runtime test зарахується passed без виконання E2E
/// частини — той самий патерн що в
/// <see cref="SignalCliE2EVersionTests.Version_RealSignalCli_ReturnsNonEmpty"/>.
/// </summary>
[Trait("Category", "E2E")]
public class SignalCliE2EParallelRpcCorrelationTests
{
    [Fact]
    public async Task Process_ParallelVersionCalls_AllResolveToCorrectResponseById()
    {
        var baseDir = AppContext.BaseDirectory;

        var hostBuilder = Host.CreateDefaultBuilder().ConfigureServices(services =>
        {
            // deprecated-shim-removal §1: AddSignalCliWithBundledRuntimeDefaults
            // замінив legacy Action<Config> overload — той самий auto-resolve
            // (bundled JRE / JAVA_HOME / PATH) без Config-shim'у.
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
                cfg.MaxRestartAttempts = 0;
                cfg.StoragePathCli = Path.Combine(
                    Path.GetTempPath(),
                    "SignalCliE2EParallelRpc-" + Guid.NewGuid());
            });
        });

        using var host = hostBuilder.Build();

        var cfg = host.Services
            .GetRequiredService<IOptions<SignalCliOptions>>().Value;
        if (!IsRuntimeAvailable(cfg, out var skipReason))
        {
            Console.Error.WriteLine($"[SKIP] {skipReason}");
            return;
        }

        try
        {
            using var startCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await host.StartAsync(startCts.Token);

            var hostedService = host.Services
                .GetRequiredService<SignalCli.Services.SignalCli.SignalCliHostedService>();
            await hostedService.WaitForReadyAsync(startCts.Token);
            Assert.Equal(
                ProcessState.Running,
                host.Services
                    .GetRequiredService<SignalCli.Services.SignalCli.ProcessStateManager>()
                    .CurrentState);

            var client = host.Services.GetRequiredService<ISignalCliClient>();

            // Кидаємо 10 паралельних version-викликів. CancellationToken bound'ить
            // загальний test runtime: якщо хоч один task завис через broken
            // correlation, лінкований CTS fail'не Task.WhenAll з OperationCanceledException
            // — без hung runner'а.
            using var rpcCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(cfg.RequestTimeoutSeconds * 2));
            const int parallelism = 10;
            var versionTasks = Enumerable.Range(0, parallelism)
                .Select(_ => client.InvokeMethodAsync(
                    "version",
                    new VersionParameters(),
                    SignalCli.Serialization.SignalJsonContext.Default.VersionParameters,
                    SignalCli.Serialization.SignalJsonContext.Default.VersionResponse,
                    rpcCts.Token))
                .ToArray();

            var results = await Task.WhenAll(versionTasks).WaitAsync(rpcCts.Token);

            // (1) Усі 10 task'ів завершилися без timeout'а.
            Assert.Equal(parallelism, results.Length);

            // (2) Усі 10 повернули ТУ САМУ version-string'у. Якщо у будь-якого
            //     task'а відповідь приземлилася у чужий TCS (broken correlation) —
            //     значення б розійшлися (або був би null/empty payload).
            var distinctVersions = results.Select(r => r.Version).Distinct().ToArray();
            Assert.Single(distinctVersions);

            // (3) Жодна відповідь не є null/empty — додаткова перевірка проти
            //     "тиха" cross-talk'у де task отримав відповідь зовсім не version-методу.
            foreach (var r in results)
            {
                Assert.NotNull(r);
                Assert.False(
                    string.IsNullOrEmpty(r.Version),
                    "Empty version → відповідь приземлилася у чужий TCS (id-correlation broken).");
            }

            // (4) Sanity: значення відповідає закріпленому у runtime-package'і signal-cli 0.14.x.
            Assert.Contains("0.14", distinctVersions[0]);

            await host.StopAsync(TimeSpan.FromSeconds(15));
        }
        finally
        {
            try
            {
                if (!string.IsNullOrEmpty(cfg.StoragePathCli))
                    Directory.Delete(cfg.StoragePathCli, recursive: true);
            }
            catch
            {
                /* best-effort cleanup */
            }
        }
    }

    /// <summary>
    /// Той самий runtime-availability skip-gate, що в
    /// <see cref="SignalCliE2EGracefulShutdownTests"/>. tasks.md §OF.1 нотує що
    /// extract в shared base — окремий follow-up (наразі тримаємо copy-paste,
    /// щоб тести лишалися незалежно-runnable).
    /// </summary>
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
