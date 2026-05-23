using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalCli.Diagnostics;
using SignalCli.Interfaces.Rpc;
using SignalCli.Logging;
using SignalCli.Models;
using SignalCli.Models.SignalCli;

namespace SignalCli.Services.SignalCli;

/// <summary>
/// Сервіс, який відстежує "живість" процесу Signal-CLI,
/// і при "зависаннях" ініціює перезапуск через SignalCliHostedService.
/// </summary>
/// <remarks>
/// B.1/B.2: реалізовано через <see cref="BackgroundService"/>; цикл побудовано на
/// <see cref="PeriodicTimer"/>(interval, <see cref="TimeProvider"/>) — стандартний
/// .NET-патерн для періодичних воркерів. <c>FakeTimeProvider</c> у тестах крутить
/// тики без реального wall-clock.
/// </remarks>
public sealed class SignalCliHealthMonitor : BackgroundService
{
    private readonly ILogger<SignalCliHealthMonitor> _logger;
    private readonly IJsonRpcClientProvider _clientProvider;
    private readonly SignalCliHostedService _signalCliHostedService;
    // D.4: типована immutable-конфігурація.
    private readonly SignalCliOptions _options;
    // G.14 (F14): абстракція часу — у проді System (wall-clock), у тестах
    // FakeTimeProvider, тож інтервал між пінгами стає віртуальним і flake-вільним.
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Створює новий екземпляр монітора здоров'я Signal CLI.
    /// </summary>
    /// <param name="logger">Логер для запису діагностичної інформації.</param>
    /// <param name="clientProvider">Постачальник JSON-RPC клієнта.</param>
    /// <param name="signalCliHostedService">Хостований сервіс Signal CLI.</param>
    /// <param name="options">Типована конфігурація через <see cref="IOptions{SignalCliOptions}"/>.</param>
    /// <param name="timeProvider">
    /// Опціональний постачальник часу для <see cref="PeriodicTimer"/> та <c>CancelAfter</c>.
    /// За замовчуванням <see cref="TimeProvider.System"/>; у тестах підставляється
    /// <c>FakeTimeProvider</c>, щоб монітор-цикл працював у віртуальному часі.
    /// </param>
    public SignalCliHealthMonitor(
        ILogger<SignalCliHealthMonitor> logger,
        IJsonRpcClientProvider clientProvider,
        SignalCliHostedService signalCliHostedService,
        IOptions<SignalCliOptions> options,
        TimeProvider? timeProvider = null
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clientProvider = clientProvider ?? throw new ArgumentNullException(nameof(clientProvider));
        _signalCliHostedService = signalCliHostedService ?? throw new ArgumentNullException(nameof(signalCliHostedService));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Lifecycle-обгортка над <see cref="BackgroundService.StartAsync"/>:
    /// зберігає звичну для попередніх версій діагностику й pre-cancellation-перевірку,
    /// потім делегує запуск ExecuteAsync базовому класу.
    /// </summary>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // Якщо ExecuteTask вже існує — повторний StartAsync не очікуваний.
        if (ExecuteTask is { IsCompleted: false })
        {
            SignalCliHealthMonitorLog.AlreadyStarted(_logger);
            throw new InvalidOperationException("StartAsync викликано коли цикл вже працює, зупиніть монітор та викличте StartAsync.");
        }
        cancellationToken.ThrowIfCancellationRequested();

        SignalCliHealthMonitorLog.StartBegin(_logger);
        var t = base.StartAsync(cancellationToken);
        SignalCliHealthMonitorLog.Started(_logger);
        return t;
    }

    /// <summary>
    /// Lifecycle-обгортка над <see cref="BackgroundService.StopAsync"/>: збереження
    /// інформаційних логів для діагностики.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        SignalCliHealthMonitorLog.StopBegin(_logger);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        SignalCliHealthMonitorLog.Stopped(_logger);
    }

    /// <summary>
    /// Основний цикл моніторингу здоров’я Signal CLI: періодично пінгує процес і
    /// викликає примусовий перезапуск, якщо пінг не вдається.
    /// </summary>
    /// <param name="stoppingToken">Токен зупинки <see cref="BackgroundService"/>.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        SignalCliHealthMonitorLog.LoopStarted(_logger);

        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.HealthCheckIntervalSeconds));
        using var timer = new PeriodicTimer(interval, _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    var isHealthy = await PingCliAsync(
                        timeout: TimeSpan.FromSeconds(_options.HealthCheckTimeoutSeconds),
                        stoppingToken
                    ).ConfigureAwait(false);

                    if (isHealthy) continue;
                    SignalCliHealthMonitorLog.RestartTriggered(_logger);
                    // post-modernize-tuning §11.B.5: process-restart counter (health-triggered).
                    SignalCliDiagnostics.ProcessRestarts.Add(1, new KeyValuePair<string, object?>("trigger", "health"));
                    await _signalCliHostedService.ForceRestartAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Нормальний сценарій при зупинці сервісу.
                    break;
                }
                // Навмисний широкий catch: межа циклу моніторингу — помилка однієї
                // ітерації не повинна зупиняти весь монітор (логуємо й продовжуємо).
                catch (Exception ex)
                {
                    SignalCliHealthMonitorLog.LoopIterationFailed(_logger, ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // PeriodicTimer.WaitForNextTickAsync кидає OCE при stoppingToken — очікувано.
        }

        SignalCliHealthMonitorLog.LoopFinished(_logger);
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
                SignalCliHealthMonitorLog.PingNoStreamPair(_logger);
                return false;
            }

            // G.14: таймаут пінга — через _timeProvider, щоб FakeTimeProvider у тестах
            // міг віртуально провернути час. CancellationTokenSource(TimeSpan, TimeProvider)
            // створює auto-cancel CTS на віртуальному годиннику; лінкуємо з caller-токеном.
            using var timeoutCts = new CancellationTokenSource(timeout, _timeProvider);
            using var localCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

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
            SignalCliHealthMonitorLog.PingOk(_logger, response.Version);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SignalCliHealthMonitorLog.PingFailed(_logger, ex);
            return false;
        }
        catch (OperationCanceledException ex)
        {
            SignalCliHealthMonitorLog.PingTimedOut(_logger, ex);
            return false;
        }
    }
}
