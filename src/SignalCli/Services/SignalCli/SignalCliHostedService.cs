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
    private bool _disposed = false;
    private bool _stopping;
    private int _restartCount;

    // Ці поля потрібні, щоб повідомляти підписників (WaitForReadyAsync) про готовність
    private readonly BehaviorSubject<StreamPair?> _streamPairSubject;
    private readonly List<TaskCompletionSource<bool>> _readyTcsList = new();
    private readonly object _readyLock = new();

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

        _streamPairSubject = new BehaviorSubject<StreamPair?>(null);
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
        if (_disposed)
            throw new ObjectDisposedException(nameof(SignalCliHostedService));
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
            FailReady(ex);
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
        if (_disposed)
            throw new ObjectDisposedException(nameof(SignalCliHostedService));
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
    /// Поточна пара потоків для взаємодії з процесом.
    /// </summary>
    public StreamPair? CurrentStreamPair { get; private set; }

    /// <summary>
    /// Потік сповіщень про зміну пари потоків.
    /// </summary>
    public IObservable<StreamPair?> StreamPairChanged => _streamPairSubject;

    /// <summary>
    /// Асинхронно очікує, поки пара потоків стане доступною.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Завдання, що представляє очікування готовності потоків.</returns>
    /// <exception cref="ObjectDisposedException">Виникає, якщо об'єкт був утилізований.</exception>
    public Task WaitForReadyAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SignalCliHostedService));

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_readyLock)
        {
            if (CurrentStreamPair != null)
            {
                tcs.TrySetResult(true);
            }
            else
            {
                _readyTcsList.Add(tcs);
            }
        }
        return tcs.Task.WaitAsync(cancellationToken);
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
            CurrentStreamPair = streams;

            _currentProcess.Exited += OnProcessExited;

            _stateManager.UpdateState(ProcessState.Running, CurrentStreamPair);
            _logger.LogInformation("Процес Signal CLI запущено (PID={Pid})", _currentProcess.Id);

            // Сповіщаємо, що сервіс став готовим
            SucceedReady();
            UpdateStreamPair(CurrentStreamPair);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося запустити процес Signal CLI");
            _stateManager.UpdateState(ProcessState.Failed, error: ex);
            FailReady(ex);
            UpdateStreamPair(null);
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
                if (CurrentStreamPair != null)
                {
                    await CurrentStreamPair.StandardInput.WriteLineAsync("exit").ConfigureAwait(false);
                    await CurrentStreamPair.StandardInput.FlushAsync().ConfigureAwait(false);
                }

                // Чекаємо 2 секунди
                await Task.Delay(2000, cancellationToken).ConfigureAwait(false);

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
            FailReady();
            UpdateStreamPair(null);
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
        CurrentStreamPair?.Dispose();
        CurrentStreamPair = null;
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
        FailReady();
        UpdateStreamPair(null);

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

    #region Методи сповіщення Ready/NotReady

    /// <summary>
    /// Сповіщає підписників про готовність сервісу.
    /// </summary>
    private void SucceedReady()
    {
        List<TaskCompletionSource<bool>> copy;
        lock (_readyLock)
        {
            copy = new List<TaskCompletionSource<bool>>(_readyTcsList);
            _readyTcsList.Clear();
        }
        foreach (var tcs in copy)
        {
            tcs.TrySetResult(true);
        }
    }

    /// <summary>
    /// Сповіщає підписників про неготовність сервісу (помилку).
    /// </summary>
    /// <param name="ex">Помилка, яка спричинила неготовність (опціонально).</param>
    private void FailReady(Exception? ex = null)
    {
        List<TaskCompletionSource<bool>> copy;
        lock (_readyLock)
        {
            copy = new List<TaskCompletionSource<bool>>(_readyTcsList);
            _readyTcsList.Clear();
        }
        foreach (var tcs in copy)
        {
            tcs.TrySetException(ex ?? new InvalidOperationException("Сервіс не готовий"));
        }
    }

    #endregion

    #region StreamPair сповіщення

    /// <summary>
    /// Оновлює пару потоків та сповіщає підписників.
    /// </summary>
    /// <param name="pair">Нова пара потоків або null.</param>
    private void UpdateStreamPair(StreamPair? pair)
    {
        _streamPairSubject.OnNext(pair);
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
            _streamPairSubject.OnCompleted();
            _streamPairSubject.Dispose();
            FailReady();
        }
    }

    #endregion
}