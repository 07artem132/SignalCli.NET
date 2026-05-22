using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.Rpc;
using SignalCli.Models;
using SignalCli.Models.SignalCli;

namespace SignalCli.Services.SignalCli;

/// <summary>
/// Сервіс, який відстежує "живість" процесу Signal-CLI, 
/// і при "зависаннях" ініціює перезапуск через SignalCliHostedService.
/// </summary>
public sealed class SignalCliHealthMonitor : IHostedService, IDisposable
{
    private readonly ILogger<SignalCliHealthMonitor> _logger;
    private readonly IJsonRpcClientProvider _clientProvider;
    private readonly SignalCliHostedService _signalCliHostedService;
    private readonly Config _config;

    private CancellationTokenSource? _monitorCts;
    private Task? _monitorTask;
    private bool _disposed;

    /// <summary>
    /// Створює новий екземпляр монітора здоров'я Signal CLI.
    /// </summary>
    /// <param name="logger">Логер для запису діагностичної інформації.</param>
    /// <param name="clientProvider">Постачальник JSON-RPC клієнта.</param>
    /// <param name="signalCliHostedService">Хостований сервіс Signal CLI.</param>
    /// <param name="config">Конфігурація Signal CLI.</param>
    public SignalCliHealthMonitor(
        ILogger<SignalCliHealthMonitor> logger,
        IJsonRpcClientProvider clientProvider,
        SignalCliHostedService signalCliHostedService,
        Config config
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clientProvider = clientProvider ?? throw new ArgumentNullException(nameof(clientProvider));
        _signalCliHostedService = signalCliHostedService ?? throw new ArgumentNullException(nameof(signalCliHostedService));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Запускається при ініціалізації застосунку: 
    /// створює фонове завдання моніторингу і входить у MonitorLoop.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Завдання, що представляє асинхронну операцію.</returns>
    /// <exception cref="InvalidOperationException">Виникає, якщо моніторинг вже запущено.</exception>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_monitorCts is { IsCancellationRequested: false })
        {
            _logger.LogError("StartAsync викликано коли цикл вже працює, зупиніть монітор та викличте StartAsync.");
            throw new InvalidOperationException("StartAsync викликано коли цикл вже працює, зупиніть монітор та викличте StartAsync.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        
        _logger.LogInformation("SignalCliHealthMonitor запускається...");

        // Створюємо новий CTS, щоб при зупинці можна було скасувати MonitorLoop
        _monitorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Запускаємо моніторинг в окремому завданні:
        _monitorTask = Task.Run(() => MonitorLoop(_monitorCts.Token), _monitorCts.Token);

        _logger.LogInformation("SignalCliHealthMonitor запущено.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Основний цикл, який періодично "пінгує" CLI:
    /// 1) чекає MonitoringIntervalSeconds
    /// 2) викликає PingCliAsync(...)
    /// 3) якщо пінг невдалий — викликає ForceRestartAsync
    /// </summary>
    /// <param name="ct">Токен скасування операції.</param>
    /// <returns>Завдання, що представляє асинхронну операцію моніторингу.</returns>
    private async Task MonitorLoop(CancellationToken ct)
    {
        _logger.LogInformation("Цикл моніторингу запущено в SignalCliHealthMonitor");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Чекаємо інтервал моніторингу (з Config)
                await Task.Delay(TimeSpan.FromSeconds(_config.HealthCheckIntervalSeconds), ct).ConfigureAwait(false);

                // "Пінгуємо"
                var isHealthy = await PingCliAsync(
                    timeout: TimeSpan.FromSeconds(_config.HealthCheckTimeoutSeconds),
                    ct
                ).ConfigureAwait(false);

                if (isHealthy) continue;
                _logger.LogWarning("Signal CLI не відповідає. Запускаємо перезапуск...");
                await _signalCliHostedService.ForceRestartAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Це нормальний сценарій при зупинці сервісу
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неочікувана помилка в циклі моніторингу");
            }
        }

        _logger.LogInformation("Цикл моніторингу завершено в SignalCliHealthMonitor");
    }

    /// <summary>
    /// Відправляє короткий JSON-RPC "version" запит, з урахуванням локального таймауту.
    /// Повертає true, якщо CLI відповів вчасно.
    /// </summary>
    /// <param name="timeout">Максимальний час очікування відповіді.</param>
    /// <param name="ct">Токен скасування операції.</param>
    /// <returns>true, якщо CLI відповів вчасно; інакше - false.</returns>
    private async Task<bool> PingCliAsync(TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            // Якщо у нас немає доступного StreamPair (сервіс ще не "готовий"), 
            // вважаємо CLI "не здоровим"
            if (_signalCliHostedService.CurrentStreamPair == null)
            {
                _logger.LogDebug("PingCliAsync: немає поточного StreamPair => CLI не готовий");
                return false;
            }

            // Створюємо локальний CancellationToken на випадок таймауту
            using var localCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            localCts.CancelAfter(timeout);

            // Отримуємо готовий клієнт
            var client = _clientProvider.Client;
            // Викликаємо "version"
            var response = await client.InvokeMethodAsync<VersionResponse, VersionParameters>(
                "version",
                new VersionParameters(),
                localCts.Token
            ).ConfigureAwait(false);
            // Якщо відповіли без помилки і є поле Version, значить все ок
            if (string.IsNullOrEmpty(response.Version)) return false;
            _logger.LogDebug("PingCliAsync: CLI відповів з версією={Version}", response.Version);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "PingCliAsync: пінг CLI невдалий");
            return false;
        } 
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Signal CLI не відповідає");
            return false;
        }
    }

    /// <summary>
    /// Зупиняється при вимкненні застосунку: 
    /// скасовує моніторинговий цикл і чекає, поки він завершиться.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Завдання, що представляє асинхронну операцію.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SignalCliHealthMonitor зупиняється...");

        if (_disposed)
            return;
        
        if (_monitorCts != null)
            await _monitorCts.CancelAsync().ConfigureAwait(false);

        if (_monitorTask != null)
        {
            try
            {
                await _monitorTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Ігноруємо — це очікувано
            }
        }

        _logger.LogInformation("SignalCliHealthMonitor зупинено.");
    }

    /// <summary>
    /// Звільнення ресурсів, скасування всіх фонових операцій.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
    }
}