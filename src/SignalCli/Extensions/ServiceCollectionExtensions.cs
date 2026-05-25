using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SignalCli.Interfaces.Rpc;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models;
using SignalCli.Services.Rpc;
using SignalCli.Services.Signal;
using SignalCli.Services.SignalCli;
using SignalCli.Utilities;

namespace SignalCli.Extensions;

/// <summary>
/// Розширення для <see cref="IServiceCollection"/>, що дозволяють зареєструвати всі необхідні сервіси для роботи з Signal CLI.
/// </summary>
[PublicAPI]
public static class ServiceCollectionExtensions
{
    // audit-followup-2026 (addsignalcli-idempotency-fix): sentinel-type marker для guard'у.
    // Тип private nested щоб consumer'и не могли випадково зарегати/перевірити його.
    // Попередній guard `services.Any(d => d.ServiceType == typeof(IOptions<SignalCliOptions>))`
    // НЕ fire'ив бо IOptions<T> зареєстровано open-generic; повторні AddSignalCli виклики
    // (a) re-run'или configure delegate (second-wins), (b) додавали 3 duplicate IHostedService
    // descriptor'и → подвійний startup. Тепер: реєструємо marker на першому виклику, перевіряємо
    // на наступних.
    private sealed class SignalCliRegistrationMarker { }

    // C# 14 extension block: усі члени розширюють один отримувач (IServiceCollection services).
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Додає всі необхідні сервіси для роботи з Signal CLI до контейнера DI
        /// з використанням <see cref="IOptions{TOptions}"/> + типізованою валідацією.
        /// </summary>
        /// <param name="configureOptions">Делегат для налаштування <see cref="SignalCliOptions"/>.</param>
        /// <returns>Колекція сервісів з доданими сервісами Signal CLI.</returns>
        /// <remarks>
        /// <para>
        /// D.2/D.4: налаштовує <see cref="OptionsBuilder{TOptions}"/> для <see cref="SignalCliOptions"/>:
        /// <c>ValidateDataAnnotations()</c> для атрибутів <c>[Required]</c>/<c>[Range]</c>,
        /// плюс додаткова перевірка «<see cref="SignalCliOptions.JavaExecutable"/> АБО
        /// <see cref="SignalCliOptions.SignalCliExecutable"/> має бути задано», плюс
        /// compile-time-генерований <see cref="SignalCliOptionsValidator"/> (D.9).
        /// </para>
        /// <para>
        /// Валідація виконується «лазі» при першому доступі до <c>IOptions&lt;SignalCliOptions&gt;.Value</c>
        /// — а саме у конструкторі <see cref="SignalCliHostedService"/>. Якщо опції
        /// некоректні, <c>StartAsync</c> хоста кидає <see cref="OptionsValidationException"/>.
        /// </para>
        /// <para>Реєстрація ідемпотентна.</para>
        /// </remarks>
        public IServiceCollection AddSignalCli(Action<SignalCliOptions>? configureOptions)
        {
            // audit-followup-2026 (addsignalcli-idempotency-fix): sentinel-marker guard.
            if (services.Any(d => d.ServiceType == typeof(SignalCliRegistrationMarker)))
                return services;
            services.AddSingleton<SignalCliRegistrationMarker>();

            ConfigureOptions(services, configureOptions);
            RegisterCoreServices(services);
            return services;
        }

        /// <summary>
        /// Додає всі необхідні сервіси для роботи з Signal CLI до контейнера DI
        /// з прив'язкою <see cref="SignalCliOptions"/> до секції <see cref="IConfiguration"/>.
        /// </summary>
        /// <param name="configurationSection">
        /// Секція конфігурації для прив'язки (наприклад, <c>builder.Configuration.GetSection("SignalCli")</c>).
        /// </param>
        /// <returns>Колекція сервісів з доданими сервісами Signal CLI.</returns>
        /// <remarks>
        /// <para>
        /// post-modernize-tuning §8b.3: канонічний шлях для <c>appsettings.json</c>-конфігурації.
        /// Прив'язує <see cref="SignalCliOptions"/> через <c>OptionsBuilder.Bind(section)</c>,
        /// потім застосовує ті ж валідаційні правила, що й overload з <see cref="Action{SignalCliOptions}"/>
        /// (cross-field XOR Java/Native + source-gen <c>SignalCliOptionsValidator</c> + <c>ValidateOnStart</c>).
        /// </para>
        /// <example>
        /// <code>
        /// // appsettings.json:
        /// // { "SignalCli": { "AppHome": "/var/lib/signal", "JavaExecutable": "/usr/bin/java", ... } }
        /// builder.Services.AddSignalCli(builder.Configuration.GetSection("SignalCli"));
        /// </code>
        /// </example>
        /// <para>Реєстрація ідемпотентна — повторні виклики не дублюють хост-сервіси.</para>
        /// <para>
        /// <b>AOT-safe:</b> завдяки <c>EnableConfigurationBindingGenerator=true</c> у csproj
        /// configuration source-gen (.NET 8+) intercepts call-sites у
        /// <c>Microsoft.Extensions.DependencyInjection.OptionsBuilderConfigurationExtensions</c>
        /// — включаючи <c>OptionsBuilder.Bind(IConfiguration)</c> — і генерує reflection-free
        /// binder. Source: <see href="https://learn.microsoft.com/dotnet/core/extensions/configuration-generator"/>
        /// ("all APIs that eventually call into these various binding methods are intercepted").
        /// Тож overload більше НЕ потребує <c>[RequiresUnreferencedCode]/[RequiresDynamicCode]</c>.
        /// </para>
        /// </remarks>
        public IServiceCollection AddSignalCli(IConfiguration configurationSection)
        {
            ArgumentNullException.ThrowIfNull(configurationSection);

            // audit-followup-2026 (addsignalcli-idempotency-fix): sentinel-marker guard.
            if (services.Any(d => d.ServiceType == typeof(SignalCliRegistrationMarker)))
                return services;
            services.AddSingleton<SignalCliRegistrationMarker>();

            ConfigureOptionsFromConfiguration(services, configurationSection);
            RegisterCoreServices(services);
            return services;
        }

        // deprecated-shim-removal §4 (remove-legacy-addsignalcli-overload): легасі-overload
        // `AddSignalCli(Action<Config>?)` видалено. Replacement:
        //   • bundled-runtime defaults → AddSignalCliWithBundledRuntimeDefaults(opts => ...)
        //   • без defaults → AddSignalCli(Action<SignalCliOptions>?)
        //   • з appsettings.json → AddSignalCli(IConfiguration)

        /// <summary>
        /// deprecated-shim-removal §1 (config-auto-resolve-migration): додає SignalCli з
        /// preset'ом bundled-runtime defaults — <c>AppHome = AppContext.BaseDirectory</c>,
        /// <c>LibDirectory = "SignalCli/lib"</c>, <c>JavaExecutable</c> auto-resolved через
        /// <see cref="JavaPathResolver"/> (bundled JRE → JAVA_HOME → Windows Oracle → PATH).
        /// Споживач у <paramref name="configure"/> може override'нути будь-яке поле — наприклад
        /// на Linux native-режимі задає <c>SignalCliExecutable</c> і нулить <c>JavaExecutable</c>.
        /// </summary>
        /// <param name="configure">Опціональний делегат для override'у defaults.</param>
        /// <remarks>
        /// Цей extension — replacement для legacy <c>AddSignalCli(Action&lt;Config&gt;?)</c>,
        /// що ра ніше робив той самий auto-resolve через <c>Config.CreateDefault()</c>. Призначений
        /// для consumer'ів пакетів <c>SignalCli.Runtime.Jre.{win-x64,osx-arm64}</c>. Consumer'и
        /// без bundled-runtime використовують <c>AddSignalCli(Action&lt;SignalCliOptions&gt;)</c> напряму.
        /// </remarks>
        public IServiceCollection AddSignalCliWithBundledRuntimeDefaults(Action<SignalCliOptions>? configure = null)
        {
            return services.AddSignalCli(opts =>
            {
                opts.AppHome = AppContext.BaseDirectory;
                opts.LibDirectory = "SignalCli/lib";
                opts.JavaExecutable = JavaPathResolver.TryResolveJavaPath(opts.AppHome);
                configure?.Invoke(opts);
            });
        }

        /// <summary>
        /// Додає сервіс обробки подій Signal до контейнера DI.
        /// </summary>
        /// <returns>Колекція сервісів з доданим сервісом обробки подій.</returns>
        /// <remarks>
        /// Ідемпотентно (F23/H.23): повторні виклики не призведуть до подвійного hosted-сервісу.
        /// </remarks>
        public IServiceCollection AddSignalEvents()
        {
            if (services.Any(d => d.ServiceType == typeof(ISignalEventService)))
                return services;

            // Реєстрація сервісу, який розбирає нотифікації
            services.AddSingleton<ISignalEventService, SignalEventService>();
            services.AddHostedService(sp => sp.GetRequiredService<ISignalEventService>());
            return services;
        }
    }

    /// <summary>
    /// D.4: реєструє <see cref="IOptions{SignalCliOptions}"/> з валідаторами.
    /// </summary>
    /// <remarks>
    /// audit E1: <c>.ValidateDataAnnotations()</c> навмисно ПРИБРАНО — source-gen
    /// <see cref="SignalCliOptionsValidator"/> ([OptionsValidator]) перевіряє ті ж самі
    /// <c>[Required]</c>/<c>[Range]</c> атрибути <b>без reflection</b>. Подвійна валідація
    /// тримала reflection-залежність <c>Microsoft.Extensions.Options.DataAnnotations</c>,
    /// що блокувала <c>&lt;IsAotCompatible&gt;true&lt;/IsAotCompatible&gt;</c> (IL2026).
    /// Cross-field правила (Java XOR Native) лишаються в <c>.Validate(...)</c> — source-gen
    /// валідатор без проблем виконує custom-lambda поряд із згенерованими перевірками.
    /// </remarks>
    private static void ConfigureOptions(IServiceCollection services, Action<SignalCliOptions>? configureOptions)
    {
        var builder = services.AddOptions<SignalCliOptions>();
        if (configureOptions != null)
            builder.Configure(configureOptions);
        ApplyCommonValidation(builder);
        RegisterCompiledValidator(services);
    }

    /// <summary>
    /// post-modernize-tuning §8b.3: інший шлях конфігурації — Bind із <see cref="IConfiguration"/>-секції.
    /// Усі решта валідаційних правил такі самі (cross-field + source-gen).
    /// </summary>
    // configuration-binder-aot-completion (CHANGELOG [4.0] pending follow-up):
    // EnableConfigurationBindingGenerator=true у csproj activates configuration source-gen
    // (.NET 8+), який intercepts OptionsBuilderConfigurationExtensions.Bind(...) call-site
    // і substitutes reflection-free generated binder. Per Microsoft Docs
    // (configuration-generator article): "all APIs that eventually call into these various
    // binding methods are intercepted and replaced with generated code." Тому overload
    // більше НЕ потребує [RequiresUnreferencedCode]/[RequiresDynamicCode] атрибутів —
    // call-site AOT-safe.
    private static void ConfigureOptionsFromConfiguration(IServiceCollection services, IConfiguration section)
    {
        var builder = services.AddOptions<SignalCliOptions>().Bind(section);
        ApplyCommonValidation(builder);
        RegisterCompiledValidator(services);
    }

    /// <summary>
    /// Спільне валідаційне правило для обох overload-ів — Java XOR Native + <c>ValidateOnStart</c>.
    /// </summary>
    private static void ApplyCommonValidation(OptionsBuilder<SignalCliOptions> builder)
    {
        builder
            .Validate(
                o => !string.IsNullOrEmpty(o.JavaExecutable) || !string.IsNullOrEmpty(o.SignalCliExecutable),
                "Потрібно задати JavaExecutable (для JVM-режиму) АБО SignalCliExecutable (для native-режиму).")
            .ValidateOnStart();
    }

    /// <summary>
    /// D.9: компайл-тайм-валідатор (без reflection, AOT-safe) — джерело істини для
    /// <c>[Required]</c>/<c>[Range]</c>-перевірок після видалення <c>.ValidateDataAnnotations()</c> (E1).
    /// </summary>
    private static void RegisterCompiledValidator(IServiceCollection services)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<SignalCliOptions>, SignalCliOptionsValidator>());
    }

    // deprecated-shim-removal §4: CopyFrom helper видалено — використовувалося лише
    // легасі `AddSignalCli(Action<Config>?)` overload'ом, що теж зник.

    /// <summary>
    /// Спільна реєстрація сервісів для обох overload-ів <c>AddSignalCli</c>.
    /// </summary>
    private static void RegisterCoreServices(IServiceCollection services)
    {
        // audit N4: реєструємо TimeProvider у DI, щоб сервіси, які приймають його як
        // опціональну ctor-залежність (JsonRpcClient, JsonRpcClientFactory,
        // SignalCliHostedService, SignalCliHealthMonitor), отримували System у проді
        // й могли мати FakeTimeProvider у тестах через services.Replace(...).
        services.TryAddSingleton(TimeProvider.System);

        // Менеджер стану процесу
        services.TryAddSingleton<ProcessStateManager>();

        // Запуск процесу
        services.TryAddSingleton<IProcessFactory, ProcessFactory>();
        services.TryAddSingleton<IProcessRunner, ProcessRunner>();

        // Об'єднаний HostedService
        services.TryAddSingleton<SignalCliHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<SignalCliHostedService>());

        // Реєстрація як IStreamPairProvider
        services.TryAddSingleton<IStreamPairProvider>(sp => sp.GetRequiredService<SignalCliHostedService>());

        // Інші сервіси
        services.TryAddSingleton<IJsonRpcClientFactory, JsonRpcClientFactory>();
        services.TryAddSingleton<JsonRpcClientHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<JsonRpcClientHostedService>());
        services.TryAddSingleton<IJsonRpcClientProvider>(sp => sp.GetRequiredService<JsonRpcClientHostedService>());

        services.TryAddSingleton<ISignalCliClient, SignalService>();
        services.TryAddSingleton<ISignalMessage, SignalMessage>();
        services.TryAddSingleton<ISignalDevices, SignalDevices>();
        services.TryAddSingleton<ISignalAccounts, SignalAccounts>();
        services.TryAddSingleton<ISignalGroups, SignalGroups>();
        // signal-cli-api-coverage Wave 3 (contacts-identity): новий facade.
        services.TryAddSingleton<ISignalContacts, SignalContacts>();

        // HealthMonitor (також як HostedService)
        services.TryAddSingleton<SignalCliHealthMonitor>();
        services.AddHostedService(sp => sp.GetRequiredService<SignalCliHealthMonitor>());
    }
}
