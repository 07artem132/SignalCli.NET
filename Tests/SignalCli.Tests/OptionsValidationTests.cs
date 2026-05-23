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
    /// та одразу резолвити Config — це момент, коли валідація має спрацювати.
    /// </summary>
    private static Action ResolveConfigWith(Action<SignalCliOptions> mutate)
    {
        var services = new ServiceCollection();
        services.AddSignalCli(mutate);
        var sp = services.BuildServiceProvider();
        return () => sp.GetRequiredService<Config>();
    }

    [Fact]
    public void Resolving_Config_With_EmptyAppHome_Throws_OptionsValidationException()
    {
        // Arrange: AppHome порожній — DataAnnotations [Required] має зловити.
        var resolve = ResolveConfigWith(o =>
        {
            o.GetType().GetProperty(nameof(SignalCliOptions.LibDirectory))!.SetValue(o, "lib");
            o.GetType().GetProperty(nameof(SignalCliOptions.JavaExecutable))!.SetValue(o, "java");
            // AppHome лишаємо порожнім (default = "")
        });

        // Act & Assert
        var ex = Assert.Throws<OptionsValidationException>(resolve);
        Assert.Contains(nameof(SignalCliOptions.AppHome), ex.Message);
    }

    [Fact]
    public void Resolving_Config_With_NegativeRange_Throws_OptionsValidationException()
    {
        // Arrange: MaxRestartAttempts = -5 — [Range(0,100)] має зловити.
        var resolve = ResolveConfigWith(o =>
        {
            o.GetType().GetProperty(nameof(SignalCliOptions.AppHome))!.SetValue(o, "/tmp/signalcli-test");
            o.GetType().GetProperty(nameof(SignalCliOptions.LibDirectory))!.SetValue(o, "lib");
            o.GetType().GetProperty(nameof(SignalCliOptions.JavaExecutable))!.SetValue(o, "java");
            o.GetType().GetProperty(nameof(SignalCliOptions.MaxRestartAttempts))!.SetValue(o, -5);
        });

        var ex = Assert.Throws<OptionsValidationException>(resolve);
        Assert.Contains(nameof(SignalCliOptions.MaxRestartAttempts), ex.Message);
    }

    [Fact]
    public void Resolving_Config_With_NoExecutable_Throws_OptionsValidationException()
    {
        // Arrange: ні Java, ні Native — кастомне правило має зловити.
        var resolve = ResolveConfigWith(o =>
        {
            o.GetType().GetProperty(nameof(SignalCliOptions.AppHome))!.SetValue(o, "/tmp/signalcli-test");
            o.GetType().GetProperty(nameof(SignalCliOptions.LibDirectory))!.SetValue(o, "lib");
            // JavaExecutable і SignalCliExecutable обидва null/порожні.
        });

        var ex = Assert.Throws<OptionsValidationException>(resolve);
        Assert.Contains("JavaExecutable", ex.Message);
        Assert.Contains("SignalCliExecutable", ex.Message);
    }

    [Fact]
    public void Resolving_Config_With_ValidOptions_Succeeds()
    {
        // Arrange: коректні опції — резолв має пройти, Config має правильні значення.
        var services = new ServiceCollection();
        services.AddSignalCli((Action<SignalCliOptions>)(o =>
        {
            o.GetType().GetProperty(nameof(SignalCliOptions.AppHome))!.SetValue(o, "/tmp/signalcli-test");
            o.GetType().GetProperty(nameof(SignalCliOptions.LibDirectory))!.SetValue(o, "lib");
            o.GetType().GetProperty(nameof(SignalCliOptions.JavaExecutable))!.SetValue(o, "java");
        }));
        var sp = services.BuildServiceProvider();

        // Act
        var config = sp.GetRequiredService<Config>();

        // Assert
        Assert.Equal("/tmp/signalcli-test", config.AppHome);
        Assert.Equal("java", config.JavaExecutable);
        Assert.Equal(3, config.MaxRestartAttempts); // default із SignalCliOptions
    }
}
