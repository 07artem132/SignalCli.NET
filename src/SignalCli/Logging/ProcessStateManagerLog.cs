using Microsoft.Extensions.Logging;
using SignalCli.Models.SignalCli;

namespace SignalCli.Logging;

/// <summary>
/// C.6: source-generated логи для <see cref="Services.SignalCli.ProcessStateManager"/>.
/// EventId-блок 960–999.
/// </summary>
internal static partial class ProcessStateManagerLog
{
    [LoggerMessage(EventId = 960, Level = LogLevel.Debug,
        Message = "UpdateState({NewState}) проігноровано — ProcessStateManager уже задиспоужений.")]
    public static partial void UpdateStateAfterDispose(ILogger logger, ProcessState newState);

    [LoggerMessage(EventId = 961, Level = LogLevel.Information, Message = "Стан процесу змінено на {NewState}")]
    public static partial void StateChanged(ILogger logger, ProcessState newState);
}
