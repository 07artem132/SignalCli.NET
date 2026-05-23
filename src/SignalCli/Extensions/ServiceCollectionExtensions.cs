using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        /// </remarks>
        public IServiceCollection AddSignalCli(Action<Config>? configure)
        {
            // H.23: ідемпотентність — якщо Config уже зареєстровано, нічого не робимо.
            // Це покриває випадок, коли AddSignalCli викликається двічі (напр., у тестах
            // або при модульній реєстрації) — у нас усі сервіси — singletons, дубль
            // hosted-service-ів призвів би до двох процесів signal-cli.
            if (services.Any(d => d.ServiceType == typeof(Config)))
                return services;

            // 1) Конфігурація
            var config = Config.CreateDefault();
            configure?.Invoke(config);
            services.AddSingleton(config);

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
}
