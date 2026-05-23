using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Logging;
using SignalCli.Models.SignalCli;

namespace SignalCli.Services.SignalCli;

internal class ProcessRunner : IProcessRunner
{
    private readonly ILogger<ProcessRunner> _logger;
    private readonly IProcessFactory _processFactory;

    public ProcessRunner(ILogger<ProcessRunner> logger, IProcessFactory processFactory)
    {
        _logger = logger;
        _processFactory = processFactory;
    }

    // post-modernize-tuning §8a.4 (audit B4): ValueTask<T> — реалізація синхронна,
    // зайва Task.FromResult-обгортка прибрана.
    public ValueTask<(IProcess Process, StreamPair StreamPair)> StartProcessWithHandle(
        ProcessConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        cancellationToken.ThrowIfCancellationRequested();

        var psi = new ProcessStartInfo
        {
            FileName = config.Executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = string.IsNullOrWhiteSpace(config.WorkingDirectory)
                ? AppDomain.CurrentDomain.BaseDirectory
                : config.WorkingDirectory,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            RedirectStandardInput = config.RedirectStandardInput,
            RedirectStandardOutput = config.RedirectStandardOutput,
            RedirectStandardError = config.RedirectStandardError
        };

        // .NET 10: ізоляція дочірнього процесу в окремій групі (лише Windows).
        if (config.CreateNewProcessGroup && OperatingSystem.IsWindows())
        {
            psi.CreateNewProcessGroup = true;
        }

        // Віддаємо перевагу ArgumentList (безпечне екранування кожного аргументу).
        // Рядок Arguments — лише запасний варіант для зворотної сумісності.
        if (config.ArgumentList is { Count: > 0 })
        {
            foreach (var arg in config.ArgumentList)
                psi.ArgumentList.Add(arg);
        }
        else if (!string.IsNullOrEmpty(config.Arguments))
        {
            psi.Arguments = config.Arguments;
        }

        // Додаємо змінні середовища
        foreach (var kv in config.EnvironmentVariables)
        {
            psi.Environment[kv.Key] = kv.Value;
        }

        ProcessRunnerLog.StartingProcess(_logger, config.Executable, config.Arguments);

        var proc = _processFactory.CreateProcess(psi);
        
        try
        {
            if (!proc.Start(cancellationToken))
            {
                throw new InvalidOperationException($"Не вдалося запустити процес: {config.Executable}");
            }

            ProcessRunnerLog.ProcessStarted(_logger, config.Executable, proc.Id);

            var streams = new StreamPair(
                proc.StandardInput,
                proc.StandardOutput,
                proc.StandardError
            );

            return ValueTask.FromResult<(IProcess, StreamPair)>((proc, streams));
        }
        catch (Exception ex)
        {
            ProcessRunnerLog.StartFailed(_logger, ex, config.Executable);
            proc.Dispose();
            throw;
        }
    }
}