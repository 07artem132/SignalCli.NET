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
}
