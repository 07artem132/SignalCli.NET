using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.Rpc;
using SignalCli.Models.SignalCli;
using SignalCli.Services.SignalCli;

namespace SignalCli.Services.Rpc;

/// <summary>
/// HostedService, який чекає готовності потоків (IStreamPairProvider)
/// і створює IJsonRpcClient, щоб потім інші сервіси могли ним користуватися.
/// </summary>
internal sealed class JsonRpcClientHostedService : IHostedService, IJsonRpcClientProvider, IAsyncDisposable
{
    private readonly ILogger<JsonRpcClientHostedService> _logger;
    private readonly IJsonRpcClientFactory _factory;
    private readonly SignalCliHostedService _signalCliHostedService;
    private IJsonRpcClient? _client;
    private bool _disposed;

    /// <summary>
    /// Створює новий екземпляр хостованого сервісу JSON-RPC клієнта.
    /// </summary>
    /// <param name="logger">Логер для запису діагностичної інформації.</param>
    /// <param name="factory">Фабрика для створення JSON-RPC клієнтів.</param>
    /// <param name="signalCliHostedService">Хостований сервіс Signal CLI.</param>
    public JsonRpcClientHostedService(
        ILogger<JsonRpcClientHostedService> logger,
        IJsonRpcClientFactory factory,
        SignalCliHostedService signalCliHostedService)
    {
        _logger = logger;
        _factory = factory;
        _signalCliHostedService = signalCliHostedService;
    }

    /// <summary>
    /// Поточний активний екземпляр JSON-RPC клієнта.
    /// </summary>
    /// <exception cref="InvalidOperationException">Виникає, якщо клієнт ще не ініціалізовано.</exception>
    public IJsonRpcClient Client => _client
                                    ?? throw new InvalidOperationException("JsonRpcClient ще не ініціалізовано.");

    /// <summary>
    /// Запускає хостований сервіс.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Завдання, що представляє асинхронну операцію.</returns>
    /// <exception cref="ObjectDisposedException">Виникає, якщо об'єкт був утилізований.</exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation("JsonRpcClientHostedService починає роботу...");

        try
        {
            // Чекаємо, поки SignalCliHostedService реально підніме процес
            await _signalCliHostedService.WaitForReadyAsync(cancellationToken).ConfigureAwait(false);
            // A.7: фабрика тепер синхронна — створення клієнта не потребує await.
            _client = _factory.Create();
            var versionResp = await _client.InvokeMethodAsync<VersionResponse, VersionParameters>("version", new(), cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Версія signal-cli JSON-RPC: {Version}", versionResp.Version);

            _logger.LogInformation("JsonRpcClientHostedService запущено - клієнт готовий");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка запуску JsonRpcClientHostedService");
            throw;
        }
    }

    /// <summary>
    /// Зупиняє хостований сервіс.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Завдання, що представляє асинхронну операцію.</returns>
    /// <exception cref="ObjectDisposedException">Виникає, якщо об'єкт був утилізований.</exception>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation("JsonRpcClientHostedService зупиняється...");

        try
        {
            // A.6: IJsonRpcClient — IAsyncDisposable; диспозимо саме асинхронно.
            if (_client != null)
                await _client.DisposeAsync().ConfigureAwait(false);

            _client = null;
            _logger.LogInformation("JsonRpcClientHostedService зупинено.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка зупинки JsonRpcClientHostedService");
            throw;
        }
    }

    /// <summary>
    /// Асинхронно звільняє ресурси, пов'язані з об'єктом.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (_client != null)
            {
                await _client.DisposeAsync().ConfigureAwait(false);
                _client = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при DisposeAsync у JsonRpcClientHostedService");
        }
    }
}