using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models;
using SignalCli.Models.SignalCli;

namespace SignalCli.Services.SignalCli;

/// <summary>
/// HostedService, який:
/// 1) Запускає та зупиняє Signal-CLI при старті/зупинці застосунку.
/// 2) Автоматично перезапускає його при аварійному завершенні (до MaxRestartAttempts).
/// 3) Надає доступ до StreamPair через IStreamPairProvider.
/// </summary>
public class SignalCliHostedService : IHostedService, IStreamPairProvider, IDisposable
{
    private readonly ILogger<SignalCliHostedService> _logger;
    private readonly IProcessRunner _processRunner;
    private readonly ProcessStateManager _stateManager;
    private readonly Config _config;

    private readonly AsyncLock _operationLock = new AsyncLock();

    // Поля для управління процесом
    private IProcess? _currentProcess;
    // Тримаємо посилання на пару потоків ВИКЛЮЧНО для звільнення ресурсу в CleanupProcess.
    // Єдине джерело істини про стан/потоки — ProcessStateManager.
    private StreamPair? _currentStreamPair;
    private bool _disposed;
    private bool _stopping;
    private int _restartCount;

    // Сигнал завершення для StreamPairChanged (щоб потік завершувався при Dispose сервісу).
    private readonly Subject<bool> _disposeSignal = new();

    /// <summary>
    /// Створює новий екземпляр хостованого сервісу Signal CLI.
    /// </summary>
    /// <param name="logger">Логер для запису діагностичної інформації.</param>
    /// <param name="processRunner">Запускач зовнішніх процесів.</param>
    /// <param name="stateManager">Менеджер стану процесу.</param>
    /// <param name="config">Конфігурація Signal CLI.</param>
    public SignalCliHostedService(
        ILogger<SignalCliHostedService> logger,
        IProcessRunner processRunner,
        ProcessStateManager stateManager,
        Config config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    #region IHostedService

    /// <summary>
    /// Запускає хостований сервіс.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Завдання, що представляє асинхронну операцію.</returns>
    /// <exception cref="ObjectDisposedException">Виникає, якщо об'єкт був утилізований.</exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("SignalCliHostedService запускається...");

        try
        {
            // Запускаємо процес (перший старт)
            await StartProcessInternalAsyncNoLock(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("SignalCliHostedService успішно запущено.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка запуску SignalCliHostedService");
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
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("SignalCliHostedService зупиняється...");

        try
        {
            var currentState = _stateManager.CurrentState;
            if (currentState != ProcessState.Running && currentState != ProcessState.Starting)
            {
                _logger.LogInformation("StopProcessInternalAsync: поточний стан = {State}, пропускаємо зупинку.", currentState);
                return;
            }
            // Зупиняємо процес
            await StopProcessInternalAsyncNoLock(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("SignalCliHostedService зупинено.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка зупинки SignalCliHostedService");
            throw;
        }
    }

    #endregion

    #region IStreamPairProvider

    /// <summary>
    /// Поточна пара потоків (похідна від ProcessStateManager — єдиного джерела істини).
    /// </summary>
    public StreamPair? CurrentStreamPair => _stateManager.CurrentStreamPair;

    /// <summary>
    /// Потік сповіщень про зміну пари потоків (похідний від стану; завершується при Dispose).
    /// </summary>
    public IObservable<StreamPair?> StreamPairChanged =>
        _stateManager.StreamPairChanged.TakeUntil(_disposeSignal);

    /// <summary>
    /// Асинхронно очікує, поки пара потоків стане доступною (делегує менеджеру стану).
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Завдання, що представляє очікування готовності потоків.</returns>
    /// <exception cref="ObjectDisposedException">Виникає, якщо об'єкт був утилізований.</exception>
    public Task WaitForReadyAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _stateManager.WaitForReadyAsync(cancellationToken);
    }

    #endregion

    #region ForceRestartAsync

    /// <summary>
    /// Примусовий перезапуск Signal-CLI (наприклад, при "CLI завис").
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Завдання, що представляє асинхронну операцію перезапуску.</returns>
    public async Task ForceRestartAsync(CancellationToken cancellationToken)
    {
        if (_disposed || _stopping)
        {
            _logger.LogWarning("ForceRestartAsync викликано, але сервіс утилізовано або зупиняється.");
            return;
        }

        using (await _operationLock.LockAsync(cancellationToken))
        {
            if (_disposed || _stopping)
            {
                _logger.LogWarning("ForceRestartAsync: сервіс було утилізовано/зупинено під час очікування блокування.");
                return;
            }

            // Перевіримо, що поточний стан — Running або Failed, інакше рестарт марний
            var currentState = _stateManager.CurrentState;
            if (currentState != ProcessState.Running &&
                currentState != ProcessState.Failed)
            {
                _logger.LogInformation("ForceRestartAsync: поточний стан = {State}, пропускаємо примусовий перезапуск.", currentState);
                return;
            }

            // Враховуємо лічильник перезапусків
            _restartCount++;
            if (_config.MaxRestartAttempts > 0 && _restartCount > _config.MaxRestartAttempts)
            {
                _logger.LogError("ForceRestartAsync: перевищено MaxRestartAttempts ({Max}). Скасування перезапуску.", _config.MaxRestartAttempts);
                return;
            }

            _logger.LogWarning("Примусовий перезапуск SignalCLI (спроба #{Count}/{Max})", _restartCount, _config.MaxRestartAttempts);

            // 1) Зупинка
            await StopProcessInternalAsyncNoLock(cancellationToken).ConfigureAwait(false);

            // 2) Невелика затримка
            await Task.Delay(TimeSpan.FromSeconds(_config.RestartDelaySeconds), cancellationToken).ConfigureAwait(false);

            // 3) Перезапуск
            await StartProcessInternalAsyncNoLock(cancellationToken).ConfigureAwait(false);
        }
    }

    #endregion

    #region Внутрішня логіка запуску/зупинки процесу

    /// <summary>
    /// Внутрішній метод для запуску процесу без блокування.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Завдання, що представляє асинхронну операцію.</returns>
    private async Task StartProcessInternalAsyncNoLock(CancellationToken cancellationToken)
    {
        if (_disposed) return;
        cancellationToken.ThrowIfCancellationRequested();
            
        var currentState = _stateManager.CurrentState;
        if (currentState != ProcessState.NotStarted &&
            currentState != ProcessState.Stopped &&
            currentState != ProcessState.Failed)
        {
            _logger.LogWarning("Неможливо запустити у стані {State}", currentState);
            return;
        }

        _logger.LogInformation("Запуск процесу Signal CLI...");
        _stopping = false;
        // Якщо це реальний старт (не авто/force), можна скинути _restartCount в 0
        // Але для автоперезапуску робимо інакше — в OnProcessExited / ForceRestartAsync
        if (currentState == ProcessState.NotStarted)
            _restartCount = 0;

        _stateManager.UpdateState(ProcessState.Starting);

        try
        {
            var procConfig = _config.ToProcessConfig();
            var (proc, streams) = await _processRunner.StartProcessWithHandle(procConfig, cancellationToken).ConfigureAwait(false);

            _currentProcess = proc;
            _currentStreamPair = streams;

            _currentProcess.Exited += OnProcessExited;

            // ProcessStateManager — єдине джерело істини; з нього похідні
            // CurrentStreamPair / StreamPairChanged / WaitForReadyAsync.
            _stateManager.UpdateState(ProcessState.Running, streams);
            _logger.LogInformation("Процес Signal CLI запущено (PID={Pid})", _currentProcess.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося запустити процес Signal CLI");
            _stateManager.UpdateState(ProcessState.Failed, error: ex);
            throw;
        }
    }

    /// <summary>
    /// Внутрішній метод для зупинки процесу без блокування.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Завдання, що представляє асинхронну операцію.</returns>
    private async Task StopProcessInternalAsyncNoLock(CancellationToken cancellationToken)
    {
        if (_disposed) return;
        cancellationToken.ThrowIfCancellationRequested();

        var currentState = _stateManager.CurrentState;
        if (currentState != ProcessState.Running && currentState != ProcessState.Starting)
        {
            _logger.LogInformation("StopProcessInternalAsync: поточний стан = {State}, пропускаємо зупинку.", currentState);
            return;
        }

        _logger.LogInformation("Зупинка процесу Signal CLI...");
        _stopping = true;
        _stateManager.UpdateState(ProcessState.Stopping);

        try
        {
            if (_currentProcess != null && !_currentProcess.HasExited)
            {
                // Спробуємо "exit"
                if (_currentStreamPair != null)
                {
                    await _currentStreamPair.StandardInput.WriteLineAsync("exit").ConfigureAwait(false);
                    await _currentStreamPair.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                // Чекаємо граційного завершення (інтервал з Config)
                await Task.Delay(TimeSpan.FromSeconds(_config.StopTimeoutSeconds), cancellationToken)
                    .ConfigureAwait(false);

                if (!_currentProcess.HasExited)
                {
                    _logger.LogWarning("Процес не завершився, примусово завершуємо його...");
                    _currentProcess.Kill(entireProcessTree: true);
                }
            }

            _stateManager.UpdateState(ProcessState.Stopped);
            _logger.LogInformation("Процес Signal CLI зупинено.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка зупинки Signal CLI");
            _stateManager.UpdateState(ProcessState.Failed, error: ex);
            throw;
        }
        finally
        {
            CleanupProcess();
            _stopping = false;
        }
    }

    /// <summary>
    /// Очищує ресурси, пов'язані з процесом.
    /// </summary>
    private void CleanupProcess()
    {
        if (_currentProcess != null)
        {
            _currentProcess.Exited -= OnProcessExited;
            try
            {
                _currentProcess.Dispose();
            }
            catch { /* ігноруємо */ }
            _currentProcess = null;
        }
        _currentStreamPair?.Dispose();
        _currentStreamPair = null;
    }

    #endregion

    #region Обробка Exited та автоперезапуск

    /// <summary>
    /// Обробник події Exited процесу. Запускає автоматичний перезапуск за потреби.
    /// </summary>
    /// <param name="sender">Джерело події.</param>
    /// <param name="e">Аргументи події.</param>
    private async void OnProcessExited(object? sender, EventArgs e)
    {
        // Якщо сервіс вже Dispose / Stop, то ігноруємо
        if (_disposed) return;
        if (_stopping) return; // умисна зупинка

        _logger.LogWarning("Процес Signal CLI завершився неочікувано.");
        _stateManager.UpdateState(ProcessState.Failed);

        CleanupProcess();

        if (_config.MaxRestartAttempts <= 0)
        {
            _logger.LogWarning("Автоперезапуск вимкнено (MaxRestartAttempts=0).");
            return;
        }

        _restartCount++;
        if (_restartCount <= _config.MaxRestartAttempts)
        {
            _logger.LogInformation("Спроба автоперезапуску (#{Count}/{Max})...", _restartCount, _config.MaxRestartAttempts);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_config.RestartDelaySeconds)).ConfigureAwait(false);
                // Можна було викликати ForceRestartAsync, але ми вже знаємо що процес мертвий,
                // тож просто StartProcessInternalAsync:
                await StartProcessInternalAsyncNoLock(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не вдалося перезапустити процес Signal CLI.");
            }
        }
        else
        {
            _logger.LogError("Досягнуто максимальну кількість перезапусків ({Max}). Більше не перезапускатимемо.", _config.MaxRestartAttempts);
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Звільняє ресурси, пов'язані з об'єктом.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);

        _logger.LogInformation("Disposing SignalCliHostedService...");

        try
        {
            // Якщо процес все ще живий, краще його зупинити
            if (_currentProcess != null && !_currentProcess.HasExited)
            {
                _currentProcess.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при disposing SignalCliHostedService");
        }
        finally
        {
            CleanupProcess();

            // Завершуємо потік StreamPairChanged для підписників цього сервісу.
            _disposeSignal.OnNext(true);
            _disposeSignal.OnCompleted();
            _disposeSignal.Dispose();

            // Переводимо стан у Failed з помилкою — це провалює всі очікуючі WaitForReadyAsync
            // (єдине джерело істини — менеджер стану).
            if (_stateManager.CurrentState is ProcessState.Running
                or ProcessState.Starting or ProcessState.Stopping or ProcessState.NotStarted)
            {
                _stateManager.UpdateState(ProcessState.Failed,
                    error: new InvalidOperationException("SignalCliHostedService утилізовано."));
            }
        }
    }

    #endregion
}