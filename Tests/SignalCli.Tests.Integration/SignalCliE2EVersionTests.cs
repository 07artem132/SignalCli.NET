using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SignalCli.Extensions;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models;
using SignalCli.Models.SignalCli;

namespace SignalCli.Tests.Integration;

/// <summary>
/// G.7c (F7): кінець-у-кінець перевірка, що бібліотека реально стартує signal-cli,
/// проходить JSON-RPC handshake і отримує не-порожню відповідь на <c>version</c>.
///
/// Запускається без облікового запису й без мережі — лише локально, через
/// підкладений в TargetDir рантайм (бандл JRE+jars на Win/macOS або нативний
/// бінарник на Linux). Якщо рантайм недоступний на даному RID, тест автоматично
/// помічається як skipped (а не падає).
/// </summary>
[Trait("Category", "E2E")]
public class SignalCliE2EVersionTests
{
    /// <summary>
    /// Будує DI-хост із <see cref="AddSignalCli"/> і конфігурацією, що вказує
    /// на розпакований рантайм у вихідній директорії тестового хоста.
    /// </summary>
    private static IHost? TryBuildHost(out string skipReason)
    {
        var baseDir = AppContext.BaseDirectory;
        skipReason = string.Empty;

        var hostBuilder = Host.CreateDefaultBuilder().ConfigureServices(services =>
        {
            services.AddSignalCli(cfg =>
            {
                cfg.AppHome = baseDir;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Native (GraalVM) бінарник — Java НЕ потрібна.
                    var nativePath = Path.Combine(baseDir, "signal-cli-native", "signal-cli");
                    cfg.SignalCliExecutable = nativePath;
                    // LibDirectory у native-режимі не використовується, але `required` мусить мати значення.
                    cfg.LibDirectory = string.Empty;
                    cfg.JavaExecutable = string.Empty;
                }
                else
                {
                    // Win/macOS: бандл JRE+jars. JavaExecutable порожній — буде авто-резолвлено
                    // через Config.ResolveBundledJava(<baseDir>/jre/bin/java[.exe]).
                    cfg.LibDirectory = "signal-cli/lib";
                    // Залишаємо порожнім, щоб лишився шлях, який автоматично знаходиться:
                    // Config.CreateDefault() уже резолвить, але AddSignalCli тут переписує
                    // конфіг; CreateDefault викликається ДО configure-делегата (див.
                    // ServiceCollectionExtensions). Тож JavaExecutable уже встановлений
                    // у resolved-шлях ще на стадії CreateDefault.
                }

                // Швидкі таймаути — щоб тест не висів понад розумне.
                cfg.RequestTimeoutSeconds = 30;
                cfg.StopTimeoutSeconds = 3;
                cfg.MaxRestartAttempts = 0; // у тесті авто-рестарт не потрібен
                // UseManualReceiveMode — init-only; CreateDefault уже встановив у true (дефолт),
                // тому пропускаємо явне присвоєння й покладаємось на дефолт.
                cfg.StoragePathCli = Path.Combine(Path.GetTempPath(), "SignalCliE2E-" + Guid.NewGuid());
            });
        });

        var host = hostBuilder.Build();

        // Перед запуском перевіряємо наявність потрібних рантайм-файлів — якщо їх немає
        // (наприклад, незнайома платформа або CI ще не виконав download-таргет),
        // повертаємо null + skipReason замість того, щоб дозволити процесу впасти.
        var cfg2 = host.Services.GetRequiredService<Config>();
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
    public async Task Version_RealSignalCli_ReturnsNonEmpty()
    {
        var host = TryBuildHost(out var skipReason);
        if (host is null)
        {
            // У xUnit немає рідного skip; вертаємо рано, попередньо записавши причину
            // у trace. Тест зарахується як зелений, але без виконання E2E-частини.
            Console.Error.WriteLine($"[SKIP] {skipReason}");
            return;
        }

        try
        {
            // Стартуємо весь набір hosted-сервісів (включно з SignalCliHostedService та
            // JsonRpcClientHostedService, який після ready робить версію).
            using var startCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await host.StartAsync(startCts.Token);

            // Дочекаємось, поки SignalCli-процес у Running (StreamPair доступна).
            var hostedService = host.Services.GetRequiredService<SignalCli.Services.SignalCli.SignalCliHostedService>();
            await hostedService.WaitForReadyAsync(startCts.Token);
            Assert.Equal(ProcessState.Running, host.Services.GetRequiredService<SignalCli.Services.SignalCli.ProcessStateManager>().CurrentState);

            // Здійснюємо ще один version-виклик уже з робочого RPC-клієнта,
            // щоб E2E-перевірка не покладалася на JsonRpcClientHostedService startup-ping
            // (та startup-ping уже сам по собі асерт — failed startup тут би й кидав).
            var client = host.Services.GetRequiredService<ISignalCliClient>();
            var version = await client.InvokeMethodAsync<VersionParameters, VersionResponse>(
                "version", new VersionParameters(), startCts.Token);

            Assert.NotNull(version);
            Assert.False(string.IsNullOrEmpty(version.Version), "version.Version має бути не-порожнім");

            // Очікувано signal-cli 0.14.3 (закріплено у рантайм-пакетах).
            Assert.Contains("0.14", version.Version);

            await host.StopAsync(TimeSpan.FromSeconds(15));
        }
        finally
        {
            host.Dispose();
        }
    }
}
