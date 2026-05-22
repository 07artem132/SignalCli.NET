using System.Diagnostics;
using SignalCli.Interfaces.SignalCli;

namespace SignalCli.Services.SignalCli;

/// <summary>
/// Реалізація інтерфейсу IProcessFactory, що створює екземпляри IProcess.
/// </summary>
internal class ProcessFactory: IProcessFactory
{
    public IProcess CreateProcess(ProcessStartInfo startInfo)
    {
        return new ProcessWrapper(startInfo);
    }
}