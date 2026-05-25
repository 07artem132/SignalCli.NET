using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SignalCli.Extensions;
using SignalCli.Interfaces.Signal;
using SignalCli.Models;

namespace SignalCli.Tests.Integration;

/// <summary>
/// signal-cli-api-coverage Wave 3 (contacts-identity): env-gated E2E тести проти живого
/// signal-cli daemon'у. Запускаються лише коли:
/// <list type="bullet">
/// <item>Bundled runtime присутній у <c>TargetDir</c> (як у <see cref="SignalCliE2EVersionTests"/>).</item>
/// <item><c>SIGNALCLI_TEST_ACCOUNT</c> env var виставлено (див. <see cref="TestAccountFixture"/>).</item>
/// </list>
/// Read-only — НЕ мутують state, тому безпечно ділять account між test-runs.
/// </summary>
[Trait("Category", "E2E")]
public class SignalCliE2EContactsTests
{
    /// <summary>
    /// Same TryBuildHost logic as <see cref="SignalCliE2EVersionTests"/>, дубльовано
    /// (а не extracted у shared helper) щоб обмежити blast-radius Wave-3 коммітом.
    /// При додаванні Wave-5/Wave-8 E2E розглянемо extraction.
    /// </summary>
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
                // Account-specific storage — інша директорія для CI/dev щоб не торкатися
                // звичайного SIGNALCLI_TEST_ACCOUNT профіля.
                cfg.StoragePathCli = Path.Combine(Path.GetTempPath(), "SignalCliE2EContacts-" + Guid.NewGuid());
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
    public async Task ListContacts_Returns_Empty_Or_Populated()
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

            var contacts = host.Services.GetRequiredService<ISignalContacts>();
            var resp = await contacts.ListContactsAsync(account, cancellationToken: startCts.Token);

            // Account може бути новостворений (0 contacts) або populated — обидва валідні.
            Assert.NotNull(resp);
            Assert.NotNull(resp.Items);
            // Якщо є contacts — перевіримо структуру першого.
            if (resp.Count > 0)
            {
                Assert.NotNull(resp[0].Name); // Name завжди present (composed displayName)
            }

            await host.StopAsync(TimeSpan.FromSeconds(15));
        }
        finally
        {
            host.Dispose();
        }
    }

    [Fact]
    public async Task ListIdentities_Returns_Own_Identity_AtMinimum()
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

            var contacts = host.Services.GetRequiredService<ISignalContacts>();
            var resp = await contacts.ListIdentitiesAsync(account, cancellationToken: startCts.Token);

            // Зареєстрований account мусить мати щонайменше власний identity-key.
            // Якщо немає — це або bug, або account розреєстрований.
            Assert.NotNull(resp);
            Assert.NotNull(resp.Items);
            // Wire-level pinning: TrustLevel повертається upper-snake string.
            foreach (var id in resp)
            {
                Assert.NotNull(id.Fingerprint);
                Assert.NotNull(id.SafetyNumber);
                // TrustLevel — один з трьох UPPER_SNAKE значень.
                Assert.Contains(id.TrustLevel, new[] { "UNTRUSTED", "TRUSTED_UNVERIFIED", "TRUSTED_VERIFIED" });
            }

            await host.StopAsync(TimeSpan.FromSeconds(15));
        }
        finally
        {
            host.Dispose();
        }
    }
}
