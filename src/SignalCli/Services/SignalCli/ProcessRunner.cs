using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.SignalCli;
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

    public Task<(IProcess Process, StreamPair StreamPair)> StartProcessWithHandle(
        ProcessConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        cancellationToken.ThrowIfCancellationRequested();

        var psi = new ProcessStartInfo
        {
            FileName = config.Executable,
            Arguments = config.Arguments,
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

        // Додаємо змінні середовища
        foreach (var kv in config.EnvironmentVariables)
        {
            psi.Environment[kv.Key] = kv.Value;
        }

        _logger.LogDebug("Запуск процесу: {Exe} {Args}", config.Executable, config.Arguments);

        var proc = _processFactory.CreateProcess(psi);
        
        try
        {
            if (!proc.Start(cancellationToken))
            {
                throw new InvalidOperationException($"Не вдалося запустити процес: {config.Executable}");
            }

            _logger.LogInformation("Процес {Exe} запущено з PID {Pid}", config.Executable, proc.Id);

            var streams = new StreamPair(
                proc.StandardInput,
                proc.StandardOutput,
                proc.StandardError
            );

            return Task.FromResult((proc, streams));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка запуску процесу {Exe}", config.Executable);
            proc.Dispose();
            throw;
        }
    }
}