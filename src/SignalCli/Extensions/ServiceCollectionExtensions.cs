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

namespace SignalCli.Extensions;

/// <summary>
/// Розширення для <see cref="IServiceCollection"/>, що дозволяють зареєструвати всі необхідні сервіси для роботи з Signal CLI.
/// </summary>
[PublicAPI]
public static class ServiceCollectionExtensions
{
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
            if (services.Any(d => d.ServiceType == typeof(IOptions<SignalCliOptions>)
                                  || d.ServiceType == typeof(SignalCliOptions)))
                return services;

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
        /// <b>AOT-warning:</b> `OptionsBuilder.Bind&lt;TOptions&gt;(IConfiguration)` тягне
        /// <c>Microsoft.Extensions.Configuration.Binder</c>, що використовує reflection. Для
        /// AOT-deploy'у користуйтеся `AddSignalCli(Action&lt;SignalCliOptions&gt;)`-overload'ом
        /// (повністю reflection-free).
        /// </para>
        /// </remarks>
        [RequiresUnreferencedCode("Calls Microsoft.Extensions.Configuration.Binder which uses reflection on SignalCliOptions members. Use AddSignalCli(Action<SignalCliOptions>) for AOT scenarios.")]
        [RequiresDynamicCode("Calls Microsoft.Extensions.Configuration.Binder which may generate dynamic code at runtime. Use AddSignalCli(Action<SignalCliOptions>) for AOT scenarios.")]
        public IServiceCollection AddSignalCli(IConfiguration configurationSection)
        {
            ArgumentNullException.ThrowIfNull(configurationSection);

            if (services.Any(d => d.ServiceType == typeof(IOptions<SignalCliOptions>)
                                  || d.ServiceType == typeof(SignalCliOptions)))
                return services;

            ConfigureOptionsFromConfiguration(services, configurationSection);
            RegisterCoreServices(services);
            return services;
        }

        /// <summary>
        /// Додає всі необхідні сервіси для роботи з Signal CLI до контейнера DI (legacy-overload).
        /// </summary>
        /// <param name="configure">
        /// Делегат для налаштування <see cref="Config"/>. Може бути <c>null</c> — тоді
        /// використовується <see cref="Config.CreateDefault"/> без подальших змін.
        /// </param>
        /// <returns>Колекція сервісів з доданими сервісами Signal CLI.</returns>
        /// <remarks>
        /// <para>
        /// D.3: legacy-overload. Внутрішньо адаптує <see cref="Config"/> у
        /// <see cref="SignalCliOptions"/> — усі решта сервісів працюють з <c>IOptions</c>-моделлю.
        /// </para>
        /// <para>
        /// Рекомендується новий overload із <see cref="SignalCliOptions"/>, що дає
        /// явну типізовану валідацію (DataAnnotations + ValidateOnStart). Цей метод
        /// буде видалений у 3.0.
        /// </para>
        /// </remarks>
        [Obsolete("Use AddSignalCli(Action<SignalCliOptions>?) — has DataAnnotations validation + ValidateOnStart. Will be removed in 3.0.")]
        public IServiceCollection AddSignalCli(Action<Config>? configure)
        {
            if (services.Any(d => d.ServiceType == typeof(IOptions<SignalCliOptions>)
                                  || d.ServiceType == typeof(SignalCliOptions)))
                return services;

            // Legacy шлях: будуємо Config через CreateDefault, конвертуємо у SignalCliOptions.
            ConfigureOptions(services, o =>
            {
                var legacy = Config.CreateDefault();
                configure?.Invoke(legacy);
                var snapshot = legacy.ToOptions();
                CopyFrom(snapshot, o);
            });
            RegisterCoreServices(services);
            return services;
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
    [RequiresUnreferencedCode("Bind uses reflection.")]
    [RequiresDynamicCode("Bind may generate dynamic code.")]
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

    /// <summary>Поле-в-поле копіювання SignalCliOptions snapshot → інстанс із Options-фреймворку.</summary>
    private static void CopyFrom(SignalCliOptions src, SignalCliOptions dst)
    {
        dst.AppHome = src.AppHome;
        dst.LibDirectory = src.LibDirectory;
        dst.JavaExecutable = src.JavaExecutable;
        dst.SignalCliExecutable = src.SignalCliExecutable;
        dst.CliLogLevelCli = src.CliLogLevelCli;
        dst.LogFileCli = src.LogFileCli;
        dst.StoragePathCli = src.StoragePathCli;
        dst.UseManualReceiveMode = src.UseManualReceiveMode;
        dst.MaxRestartAttempts = src.MaxRestartAttempts;
        dst.HealthCheckIntervalSeconds = src.HealthCheckIntervalSeconds;
        dst.HealthCheckTimeoutSeconds = src.HealthCheckTimeoutSeconds;
        dst.RestartDelaySeconds = src.RestartDelaySeconds;
        dst.StopTimeoutSeconds = src.StopTimeoutSeconds;
        dst.RequestTimeoutSeconds = src.RequestTimeoutSeconds;
        dst.RestartWindowSeconds = src.RestartWindowSeconds;
        dst.EnvironmentVariables = src.EnvironmentVariables;
    }

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

        // HealthMonitor (також як HostedService)
        services.TryAddSingleton<SignalCliHealthMonitor>();
        services.AddHostedService(sp => sp.GetRequiredService<SignalCliHealthMonitor>());
    }
}
