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
}
