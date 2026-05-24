using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SignalCli.Extensions;
using SignalCli.Models;

namespace SignalCli.Tests;

/// <summary>
/// D.6: перевірка типізованої валідації <see cref="SignalCliOptions"/>:
/// DataAnnotations (<c>[Required]</c>, <c>[Range]</c>) + кастомне правило
/// «JavaExecutable АБО SignalCliExecutable обов’язкові».
/// </summary>
public class OptionsValidationTests
{
    /// <summary>
    /// Шорткат: побудувати ServiceProvider із заданим конфігуратором SignalCliOptions
    /// та одразу резолвити <c>IOptions&lt;SignalCliOptions&gt;.Value</c> — це момент, коли
    /// валідація має спрацювати (тригериться при першому доступі до <c>.Value</c>).
    /// </summary>
    private static Action ResolveOptionsWith(Action<SignalCliOptions> mutate)
    {
        var services = new ServiceCollection();
        services.AddSignalCli(mutate);
        var sp = services.BuildServiceProvider();
        return () => _ = sp.GetRequiredService<IOptions<SignalCliOptions>>().Value;
    }

    [Fact]
    public void Resolving_Options_With_EmptyAppHome_Throws_OptionsValidationException()
    {
        // Arrange: AppHome порожній — DataAnnotations [Required] має зловити.
        var resolve = ResolveOptionsWith(o =>
        {
            o.LibDirectory = "lib";
            o.JavaExecutable = "java";
            // AppHome лишаємо порожнім (default = "")
        });

        // Act & Assert
        var ex = Assert.Throws<OptionsValidationException>(resolve);
        Assert.Contains(nameof(SignalCliOptions.AppHome), ex.Message);
    }

    [Fact]
    public void Resolving_Options_With_NegativeRange_Throws_OptionsValidationException()
    {
        // Arrange: MaxRestartAttempts = -5 — [Range(0,100)] має зловити.
        var resolve = ResolveOptionsWith(o =>
        {
            o.AppHome = "/tmp/signalcli-test";
            o.LibDirectory = "lib";
            o.JavaExecutable = "java";
            o.MaxRestartAttempts = -5;
        });

        var ex = Assert.Throws<OptionsValidationException>(resolve);
        Assert.Contains(nameof(SignalCliOptions.MaxRestartAttempts), ex.Message);
    }

    [Fact]
    public void Resolving_Options_With_NoExecutable_Throws_OptionsValidationException()
    {
        // Arrange: ні Java, ні Native — кастомне правило має зловити.
        var resolve = ResolveOptionsWith(o =>
        {
            o.AppHome = "/tmp/signalcli-test";
            o.LibDirectory = "lib";
            // JavaExecutable і SignalCliExecutable обидва null/порожні.
        });

        var ex = Assert.Throws<OptionsValidationException>(resolve);
        Assert.Contains("JavaExecutable", ex.Message);
        Assert.Contains("SignalCliExecutable", ex.Message);
    }

    [Fact]
    public void Resolving_Options_With_ValidValues_Succeeds()
    {
        // Arrange: коректні опції — резолв має пройти, значення правильні.
        var services = new ServiceCollection();
        services.AddSignalCli((Action<SignalCliOptions>)(o =>
        {
            o.AppHome = "/tmp/signalcli-test";
            o.LibDirectory = "lib";
            o.JavaExecutable = "java";
        }));
        var sp = services.BuildServiceProvider();

        // Act
        var options = sp.GetRequiredService<IOptions<SignalCliOptions>>().Value;

        // Assert
        Assert.Equal("/tmp/signalcli-test", options.AppHome);
        Assert.Equal("java", options.JavaExecutable);
        Assert.Equal(3, options.MaxRestartAttempts); // default із SignalCliOptions
    }

    /// <summary>
    /// post-modernize-tuning §8b.3 (audit B5): новий overload <c>AddSignalCli(IConfiguration)</c>
    /// має прив'язувати <see cref="SignalCliOptions"/> з in-memory-секції так само,
    /// як <c>Action&lt;SignalCliOptions&gt;</c>-overload.
    /// </summary>
    [Fact]
    public void AddSignalCli_FromConfiguration_BindsAppsettingsValues()
    {
        // Arrange: симулюємо `appsettings.json` через MemoryConfigurationProvider.
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["SignalCli:AppHome"] = "/tmp/signalcli-from-config",
            ["SignalCli:LibDirectory"] = "libdir",
            ["SignalCli:JavaExecutable"] = "/opt/java",
            ["SignalCli:MaxRestartAttempts"] = "7",
            ["SignalCli:RequestTimeoutSeconds"] = "42",
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddSignalCli(configuration.GetSection("SignalCli"));
        var sp = services.BuildServiceProvider();

        // Act
        var options = sp.GetRequiredService<IOptions<SignalCliOptions>>().Value;

        // Assert: значення зв'язалися; решта — default.
        Assert.Equal("/tmp/signalcli-from-config", options.AppHome);
        Assert.Equal("libdir", options.LibDirectory);
        Assert.Equal("/opt/java", options.JavaExecutable);
        Assert.Equal(7, options.MaxRestartAttempts);
        Assert.Equal(42, options.RequestTimeoutSeconds);
    }

    /// <summary>
    /// §8b.3: валідаційне правило «Java XOR Native» має спрацювати і для IConfiguration-шляху,
    /// якщо обидва executable-поля порожні.
    /// </summary>
    [Fact]
    public void AddSignalCli_FromConfiguration_NoExecutable_Throws_OptionsValidationException()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["SignalCli:AppHome"] = "/tmp/x",
            ["SignalCli:LibDirectory"] = "lib",
            // JavaExecutable + SignalCliExecutable обидва пропущено
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddSignalCli(configuration.GetSection("SignalCli"));
        var sp = services.BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(
            () => _ = sp.GetRequiredService<IOptions<SignalCliOptions>>().Value);
        Assert.Contains("JavaExecutable", ex.Message);
    }

    /// <summary>
    /// §8b.3: ArgumentNullException при null-section'і — sanity.
    /// </summary>
    [Fact]
    public void AddSignalCli_NullConfiguration_Throws_ArgumentNullException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() =>
            services.AddSignalCli((IConfiguration)null!));
    }

    // audit-followup-2026 (addsignalcli-idempotency-fix): після fix'у з sentinel-marker
    // guard'ом — re-introduce три idempotency-тести (видалені під час initial landing коли
    // знайдено bug).

    /// <summary>
    /// Sentinel-marker guard має fire'ити: другий виклик AddSignalCli — no-op.
    /// Перший виклик виграє щодо options-значень + жодних нових IHostedService descriptor'ів.
    /// </summary>
    [Fact]
    public void AddSignalCli_CalledTwice_SecondCallIsNoOp()
    {
        var services = new ServiceCollection();
        services.AddSignalCli((SignalCliOptions o) =>
        {
            o.AppHome = "/first";
            o.LibDirectory = "lib";
            o.JavaExecutable = "java-first";
        });
        var firstCount = services.Count;

        services.AddSignalCli((SignalCliOptions o) =>
        {
            o.AppHome = "/SECOND";
            o.LibDirectory = "DIFFERENT";
            o.JavaExecutable = "java-SECOND";
        });

        Assert.Equal(firstCount, services.Count); // sentinel-marker guard fired

        var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<SignalCliOptions>>().Value;
        Assert.Equal("/first", opts.AppHome); // first-call's configure delegate wins
        Assert.Equal("java-first", opts.JavaExecutable);
    }

    /// <summary>
    /// Mixed-overload idempotency: перший — Action&lt;SignalCliOptions&gt;, другий — IConfiguration.
    /// Sentinel-marker guard має fire'ити для ОБОХ overload'ів, бо marker один для всіх трьох.
    /// </summary>
    [Fact]
    public void AddSignalCli_MixedOverloads_SecondIsNoOp()
    {
        var services = new ServiceCollection();
        services.AddSignalCli((SignalCliOptions o) =>
        {
            o.AppHome = "/from-action";
            o.LibDirectory = "lib";
            o.JavaExecutable = "java";
        });
        var firstCount = services.Count;

        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SignalCli:AppHome"] = "/from-config",
                ["SignalCli:LibDirectory"] = "different-lib",
                ["SignalCli:JavaExecutable"] = "java-config",
            })
            .Build();
        services.AddSignalCli(cfg.GetSection("SignalCli"));

        Assert.Equal(firstCount, services.Count);

        var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<SignalCliOptions>>().Value;
        Assert.Equal("/from-action", opts.AppHome); // Action overload wins
    }

    /// <summary>
    /// Hosted-services SHALL bе exactly 3 (SignalCliHostedService, JsonRpcClientHostedService,
    /// SignalCliHealthMonitor) незалежно від N викликів AddSignalCli — pre-fix кожен повторний
    /// виклик додавав 3 duplicate descriptor'и → подвійний startup.
    /// </summary>
    [Fact]
    public void AddSignalCli_RepeatedCalls_HostedServicesCountStaysAt3()
    {
        var services = new ServiceCollection();
        for (int i = 0; i < 5; i++)
        {
            services.AddSignalCli((SignalCliOptions o) =>
            {
                o.AppHome = "/tmp";
                o.LibDirectory = "lib";
                o.JavaExecutable = "java";
            });
        }

        var hostedServiceCount = services.Count(d =>
            d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService));
        Assert.Equal(3, hostedServiceCount);
    }

    /// <summary>
    /// audit-followup-2026 (edge-case-coverage §6.h): EnvironmentVariables — read-only-snapshot
    /// семантика. Тип експонується як <see cref="IReadOnlyDictionary{TKey,TValue}"/>; consumer
    /// не може мутувати через cast до <see cref="IDictionary{TKey,TValue}"/> на runtime-вʼю.
    /// </summary>
    [Fact]
    public void EnvironmentVariables_IsTypedAsReadOnlyDictionary()
    {
        var services = new ServiceCollection();
        services.AddSignalCli((SignalCliOptions o) =>
        {
            o.AppHome = "/tmp";
            o.LibDirectory = "lib";
            o.JavaExecutable = "java";
            o.EnvironmentVariables = new Dictionary<string, string> { ["k"] = "v" };
        });
        var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<SignalCliOptions>>().Value;

        // Property-тип на read — IReadOnlyDictionary. Це compile-time-guarantee, runtime-check
        // лише sanity (зміна типу зламає compile, не runtime).
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(opts.EnvironmentVariables);
        Assert.Equal("v", opts.EnvironmentVariables["k"]);
    }
}
