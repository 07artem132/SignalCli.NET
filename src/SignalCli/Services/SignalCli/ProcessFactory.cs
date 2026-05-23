using System.Diagnostics;
using SignalCli.Interfaces.SignalCli;

namespace SignalCli.Services.SignalCli;

/// <summary>
/// Реалізація інтерфейсу IProcessFactory, що створює екземпляри IProcess.
/// </summary>
// post-modernize-tuning §8c.14 (audit N17): sealed — інхеріт не підтримується.
internal sealed class ProcessFactory: IProcessFactory
{
    public IProcess CreateProcess(ProcessStartInfo startInfo)
    {
        return new ProcessWrapper(startInfo);
    }
}