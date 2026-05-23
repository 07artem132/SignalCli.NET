using JetBrains.Annotations;
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
        /// Додає всі необхідні сервіси для роботи з Signal CLI до контейнера DI.
        /// </summary>
        /// <param name="configure">
        /// Делегат для налаштування конфігурації Signal CLI. Може бути <c>null</c> —
        /// тоді використовується <see cref="Config.CreateDefault"/> без подальших змін.
        /// </param>
        /// <returns>Колекція сервісів з доданими сервісами Signal CLI.</returns>
        /// <remarks>
        /// Реєстрація ідемпотентна (F23/H.23): повторні виклики не дублюють hosted-сервіси.
        /// Конфігурація з ПЕРШОГО виклику залишається активною; пізніші <paramref name="configure"/>
        /// при повторній реєстрації ігноруються (це навмисно — щоб не змінювати поведінку «гаряче»).
        /// <para>
        /// D.3: legacy-overload (<see cref="Config"/>). Рекомендується новий overload із
        /// <see cref="SignalCliOptions"/>, що дає типізовану валідацію
        /// (DataAnnotations + ValidateOnStart). Цей метод буде видалений у 3.0.
        /// </para>
        /// </remarks>
        [Obsolete("Use AddSignalCli(Action<SignalCliOptions>?) — has DataAnnotations validation + ValidateOnStart. Will be removed in 3.0.")]
        public IServiceCollection AddSignalCli(Action<Config>? configure)
        {
            // H.23: ідемпотентність — якщо Config уже зареєстровано, нічого не робимо.
            if (services.Any(d => d.ServiceType == typeof(Config)))
                return services;

            // 1) Конфігурація — legacy шлях: будуємо Config напряму, без IOptions/валідації.
            var config = Config.CreateDefault();
            configure?.Invoke(config);
            services.AddSingleton(config);

            RegisterCoreServices(services);
            return services;
        }

        /// <summary>
        /// Додає всі необхідні сервіси для роботи з Signal CLI до контейнера DI
        /// з використанням <see cref="IOptions{TOptions}"/> + типізованою валідацією.
        /// </summary>
        /// <param name="configureOptions">Делегат для налаштування <see cref="SignalCliOptions"/>.</param>
        /// <returns>Колекція сервісів з доданими сервісами Signal CLI.</returns>
        /// <remarks>
        /// <para>
        /// D.2: налаштовує <see cref="OptionsBuilder{TOptions}"/> для <see cref="SignalCliOptions"/>:
        /// <c>ValidateDataAnnotations()</c> для атрибутів <see cref="System.ComponentModel.DataAnnotations.RequiredAttribute"/>/
        /// <see cref="System.ComponentModel.DataAnnotations.RangeAttribute"/>, плюс додаткова перевірка
        /// «<see cref="SignalCliOptions.JavaExecutable"/> АБО <see cref="SignalCliOptions.SignalCliExecutable"/>
        /// має бути задано».
        /// </para>
        /// <para>
        /// Валідація виконується «лазі» при першому доступі до <c>IOptions&lt;SignalCliOptions&gt;.Value</c>
        /// — а саме при резолві <see cref="Config"/>-singleton-у на старті <see cref="SignalCliHostedService"/>;
        /// якщо опції некоректні, <c>StartAsync</c> хоста кидає <see cref="OptionsValidationException"/>.
        /// </para>
        /// <para>Реєстрація ідемпотентна.</para>
        /// </remarks>
        public IServiceCollection AddSignalCli(Action<SignalCliOptions>? configureOptions)
        {
            if (services.Any(d => d.ServiceType == typeof(Config)))
                return services;

            // 1) IOptions<SignalCliOptions> з валідацією.
            var optionsBuilder = services.AddOptions<SignalCliOptions>();
            if (configureOptions != null)
                optionsBuilder.Configure(configureOptions);
            optionsBuilder
                .ValidateDataAnnotations()
                .Validate(
                    o => !string.IsNullOrEmpty(o.JavaExecutable) || !string.IsNullOrEmpty(o.SignalCliExecutable),
                    "Потрібно задати JavaExecutable (для JVM-режиму) АБО SignalCliExecutable (для native-режиму).");

            // 2) Config — singleton-фабрика, що бере значення з IOptions і саме тут тригерить
            //    DataAnnotations + Validate(...) → OptionsValidationException на старті.
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<SignalCliOptions>>().Value.ToConfig());

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
    /// Спільна реєстрація сервісів для обох overload-ів <c>AddSignalCli</c>.
    /// </summary>
    private static void RegisterCoreServices(IServiceCollection services)
    {
        // 2) Менеджер стану процесу
        services.TryAddSingleton<ProcessStateManager>();

        // 3) Запуск процесу
        services.TryAddSingleton<IProcessFactory, ProcessFactory>();
        services.TryAddSingleton<IProcessRunner, ProcessRunner>();

        // 4) Об'єднаний HostedService
        services.TryAddSingleton<SignalCliHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<SignalCliHostedService>());

        // 4.1) Реєстрація як IStreamPairProvider
        services.TryAddSingleton<IStreamPairProvider>(sp => sp.GetRequiredService<SignalCliHostedService>());

        // Інші сервіси (JsonRpcClientFactory, JsonRpcClientHostedService, SignalService, SignalMessage)
        services.TryAddSingleton<IJsonRpcClientFactory, JsonRpcClientFactory>();
        services.TryAddSingleton<JsonRpcClientHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<JsonRpcClientHostedService>());
        services.TryAddSingleton<IJsonRpcClientProvider>(sp => sp.GetRequiredService<JsonRpcClientHostedService>());

        services.TryAddSingleton<ISignalCliClient, SignalService>();
        services.TryAddSingleton<ISignalMessage, SignalMessage>();
        services.TryAddSingleton<ISignalDevices, SignalDevices>();
        services.TryAddSingleton<ISignalAccounts, SignalAccounts>();
        services.TryAddSingleton<ISignalGroups, SignalGroups>();

        // 5) Реєстрація HealthMonitor (також як HostedService)
        services.TryAddSingleton<SignalCliHealthMonitor>();
        services.AddHostedService(sp => sp.GetRequiredService<SignalCliHealthMonitor>());
    }
}
