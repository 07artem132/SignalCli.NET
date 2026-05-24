using SignalCli.HealthChecks;
using SignalCli.Models;

namespace SignalCli.Tests.RegressionGuards;

/// <summary>
/// RG07 — NF-003 (healthchecks-version-sync, audit v2.1): main lib (SignalCli.NET) і
/// adapter (SignalCli.NET.HealthChecks) МУСЯТЬ ділити рівно одну assembly version.
/// </summary>
/// <remarks>
/// Adapter ходить до internal-членів main lib через
/// <c>[InternalsVisibleTo("SignalCli.HealthChecks")]</c>, отже бінарна сумісність
/// строго прив'язана до версії. Якщо consumer оновить лише один із пакетів —
/// перший health-check-probe кине <see cref="System.MissingMethodException"/>.
///
/// Версія тепер живе тільки в <c>Directory.Build.props</c> як
/// <c>&lt;SignalCliPackageVersion&gt;</c>; обидва csproj читають її через
/// <c>$(SignalCliPackageVersion)</c>. Цей тест ловить кейс коли контрибутор
/// випадково хардкодить <c>&lt;Version&gt;</c> у будь-якому з двох csproj.
/// </remarks>
public class VersionLockstepTests
{
    [Fact]
    public void MainLibAndHealthChecksAdapter_ShareExactSameAssemblyVersion()
    {
        // Torkaєmoсь типу з main lib, щоб гарантовано форс-завантажити асемблі.
        var main = typeof(SignalCliOptions).Assembly.GetName().Version;
        // Так само з adapter — публічний тип `SignalCliHealthCheck` (sealed class у
        // SignalCli.HealthChecks namespace) тягне за собою свою assembly.
        var adapter = typeof(SignalCliHealthCheck).Assembly.GetName().Version;

        Assert.NotNull(main);
        Assert.NotNull(adapter);
        Assert.Equal(main, adapter);
    }
}
