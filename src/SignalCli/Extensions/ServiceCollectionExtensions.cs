using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
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
        /// <param name="configure">Делегат для налаштування конфігурації Signal CLI.</param>
        /// <returns>Колекція сервісів з доданими сервісами Signal CLI.</returns>
        public IServiceCollection AddSignalCli(Action<Config> configure)
        {
            // 1) Конфігурація
            var config = Config.CreateDefault();
            configure?.Invoke(config);
            services.AddSingleton(config);

            // 2) Менеджер стану процесу
            services.AddSingleton<ProcessStateManager>();

            // 3) Запуск процесу
            services.AddSingleton<IProcessFactory, ProcessFactory>();
            services.AddSingleton<IProcessRunner, ProcessRunner>();

            // 4) Об'єднаний HostedService
            services.AddSingleton<SignalCliHostedService>();
            services.AddHostedService(sp => sp.GetRequiredService<SignalCliHostedService>());

            // 4.1) Реєстрація як IStreamPairProvider
            services.AddSingleton<IStreamPairProvider>(sp => sp.GetRequiredService<SignalCliHostedService>());

            // Інші сервіси (JsonRpcClientFactory, JsonRpcClientHostedService, SignalService, SignalMessage)
            services.AddSingleton<IJsonRpcClientFactory, JsonRpcClientFactory>();
            services.AddSingleton<JsonRpcClientHostedService>();
            services.AddHostedService(sp => sp.GetRequiredService<JsonRpcClientHostedService>());
            services.AddSingleton<IJsonRpcClientProvider>(sp => sp.GetRequiredService<JsonRpcClientHostedService>());

            services.AddSingleton<ISignalCliClient, SignalService>();
            services.AddSingleton<ISignalMessage, SignalMessage>();
            services.AddSingleton<ISignalDevices, SignalDevices>();
            services.AddSingleton<ISignalAccounts, SignalAccounts>();
            services.AddSingleton<ISignalGroups, SignalGroups>();

            // 5) Реєстрація HealthMonitor (також як HostedService)
            services.AddSingleton<SignalCliHealthMonitor>();
            services.AddHostedService(sp => sp.GetRequiredService<SignalCliHealthMonitor>());

            return services;
        }

        /// <summary>
        /// Додає сервіс обробки подій Signal до контейнера DI.
        /// </summary>
        /// <returns>Колекція сервісів з доданим сервісом обробки подій.</returns>
        public IServiceCollection AddSignalEvents()
        {
            // Реєстрація сервісу, який розбирає нотифікації
            services.AddSingleton<ISignalEventService, SignalEventService>();
            services.AddHostedService(sp => sp.GetRequiredService<ISignalEventService>());
            return services;
        }
    }
}
