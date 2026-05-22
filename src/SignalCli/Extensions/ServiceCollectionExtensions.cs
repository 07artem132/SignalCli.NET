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
/// Розширення для IServiceCollection, що дозволяють зареєструвати всі необхідні сервіси для роботи з Signal CLI.
/// </summary>
[PublicAPI]
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Додає всі необхідні сервіси для роботи з Signal CLI до контейнера DI.
    /// </summary>
    /// <param name="services">Колекція сервісів DI.</param>
    /// <param name="configure">Делегат для налаштування конфігурації Signal CLI.</param>
    /// <returns>Колекція сервісів з доданими сервісами Signal CLI.</returns>
    public static IServiceCollection AddSignalCli(
        this IServiceCollection services,
        Action<Config> configure)
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

        // 5) Реєстрація HealthMonitor
        services.AddSingleton<SignalCliHealthMonitor>();
        // Реєстрація HealthMonitor як HostedService
        services.AddHostedService(sp => sp.GetRequiredService<SignalCliHealthMonitor>());

        return services;
    }

    /// <summary>
    /// Додає сервіс обробки подій Signal до контейнера DI.
    /// </summary>
    /// <param name="services">Колекція сервісів DI.</param>
    /// <returns>Колекція сервісів з доданим сервісом обробки подій.</returns>
    public static IServiceCollection AddSignalEvents(this IServiceCollection services)
    {
        // Реєстрація сервісу, який розбирає нотифікації
        services.AddSingleton<ISignalEventService, SignalEventService>();
        services.AddHostedService(sp => sp.GetRequiredService<ISignalEventService>());
        return services;
    }
}