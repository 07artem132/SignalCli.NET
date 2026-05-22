using System.Diagnostics;
using SignalCli.Interfaces.SignalCli;

namespace SignalCli.Services.SignalCli;

/// <summary>
/// Обгортка для системного процесу, що реалізує інтерфейс IProcess.
/// </summary>
internal class ProcessWrapper : IProcess
{
    private readonly Process _process;

    public ProcessWrapper(ProcessStartInfo startInfo)
    {
        _process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true // Включаємо підтримку подій
        };

        // Підписуємося на подію Exited внутрішнього процесу
        _process.Exited += (sender, args) =>
        {
            Exited?.Invoke(this, args);
        };
    }

    public event EventHandler? Exited;

    public int Id => _process.Id;
    
    public bool HasExited => _process.HasExited;
    
    public StreamWriter StandardInput => _process.StandardInput;
    
    public StreamReader StandardOutput => _process.StandardOutput;
    
    public StreamReader StandardError => _process.StandardError;
    
    public bool EnableRaisingEvents => _process.EnableRaisingEvents;

    public bool Start(CancellationToken cancellationToken = default)
    {
        return _process.Start();
    }

    public Task WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        return _process.WaitForExitAsync(cancellationToken);
    }

    public void Kill(bool entireProcessTree)
    {
        _process.Kill(entireProcessTree);
    }

    public void Dispose()
    {
        _process.Dispose();
    }
}