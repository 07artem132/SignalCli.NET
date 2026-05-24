using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.Rpc;
using SignalCli.Logging;
using SignalCli.Models.SignalCli;
using SignalCli.Serialization;
using SignalCli.Services.SignalCli;

namespace SignalCli.Services.Rpc;

/// <summary>
/// HostedService, який чекає готовності потоків (IStreamPairProvider)
/// і створює IJsonRpcClient, щоб потім інші сервіси могли ним користуватися.
/// </summary>
internal sealed class JsonRpcClientHostedService : IHostedLifecycleService, IJsonRpcClientProvider, IAsyncDisposable
{
    private readonly ILogger<JsonRpcClientHostedService> _logger;
    private readonly IJsonRpcClientFactory _factory;
    private readonly SignalCliHostedService _signalCliHostedService;
    // signal-cli-protocol-alignment (field-barrier-hardening): volatile — поле читається з кількох
    // потоків (Client getter ← SignalCliHealthMonitor.PingCliAsync, SignalEventService.StartAsync)
    // без локу; на ARM64 (.NET 10 first-class) без acquire/release-семантики reader на іншому ядрі
    // міг би побачити stale-null після того як StartAsync опублікував non-null. На x64 reference-
    // read атомарний — фікс не змінює спостережувану поведінку, але закриває weak-memory-model gap.
    private volatile IJsonRpcClient? _client;
    // post-modernize-tuning §2.4: Interlocked.Exchange-based disposal flag.
    private int _disposedFlag;
    private bool _disposed => Volatile.Read(ref _disposedFlag) != 0;

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

    // post-modernize-tuning §8a.2 (audit B3): opt-in у IHostedLifecycleService для
    // explicit-ordering startup/shutdown phases. No-op реалізації — поточна поведінка
    // (StartAsync = WaitForReady + Create client + ping version) лишається без змін.
    /// <summary>Викликається ПЕРЕД <see cref="StartAsync"/> у всіх хостованих сервісах.</summary>
    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    /// <summary>Викликається ПІСЛЯ <see cref="StartAsync"/> у всіх хостованих сервісах.</summary>
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    /// <summary>Викликається ПЕРЕД <see cref="StopAsync"/> у всіх хостованих сервісах.</summary>
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    /// <summary>Викликається ПІСЛЯ <see cref="StopAsync"/> у всіх хостованих сервісах.</summary>
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Запускає хостований сервіс.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Завдання, що представляє асинхронну операцію.</returns>
    /// <exception cref="ObjectDisposedException">Виникає, якщо об'єкт був утилізований.</exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        JsonRpcClientHostedServiceLog.StartBegin(_logger);

        try
        {
            // Чекаємо, поки SignalCliHostedService реально підніме процес
            await _signalCliHostedService.WaitForReadyAsync(cancellationToken).ConfigureAwait(false);
            // A.7: фабрика тепер синхронна — створення клієнта не потребує await.
            _client = _factory.Create();
            var versionResp = await _client.InvokeMethodAsync(
                "version",
                new VersionParameters(),
                SignalJsonContext.Default.VersionParameters,
                SignalJsonContext.Default.VersionResponse,
                cancellationToken).ConfigureAwait(false);
            JsonRpcClientHostedServiceLog.Version(_logger, versionResp.Version);

            JsonRpcClientHostedServiceLog.StartReady(_logger);
        }
        catch (Exception ex)
        {
            JsonRpcClientHostedServiceLog.StartFailed(_logger, ex);
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

        JsonRpcClientHostedServiceLog.StopBegin(_logger);

        try
        {
            // A.6: IJsonRpcClient — IAsyncDisposable; диспозимо саме асинхронно.
            if (_client != null)
                await _client.DisposeAsync().ConfigureAwait(false);

            _client = null;
            JsonRpcClientHostedServiceLog.Stopped(_logger);
        }
        catch (Exception ex)
        {
            JsonRpcClientHostedServiceLog.StopFailed(_logger, ex);
            throw;
        }
    }

    /// <summary>
    /// Асинхронно звільняє ресурси, пов'язані з об'єктом.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposedFlag, 1) != 0) return;

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
            JsonRpcClientHostedServiceLog.DisposeFailed(_logger, ex);
        }
    }
}