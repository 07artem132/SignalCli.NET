using Microsoft.Extensions.Logging;

namespace SignalCli.Logging;

/// <summary>
/// C.5: source-generated логи для <see cref="Services.Signal.SignalGroups"/>.
/// EventId-блок 810–819.
/// </summary>
internal static partial class SignalGroupsLog
{
    [LoggerMessage(EventId = 810, Level = LogLevel.Debug, Message = "Отримання списку груп")]
    public static partial void ListGroupsRequested(ILogger logger);

    [LoggerMessage(EventId = 811, Level = LogLevel.Error, Message = "Отримано нульову відповідь на listGroups")]
    public static partial void ListGroupsNullResponse(ILogger logger);

    [LoggerMessage(EventId = 812, Level = LogLevel.Information,
        Message = "Список груп отримано успішно. Кількість={Count}")]
    public static partial void ListGroupsOk(ILogger logger, int count);

    [LoggerMessage(EventId = 813, Level = LogLevel.Trace, Message = "Групи={Groups}")]
    public static partial void ListGroupsTrace(ILogger logger, string groups);

    [LoggerMessage(EventId = 814, Level = LogLevel.Error, Message = "Помилка отримання списку груп")]
    public static partial void ListGroupsFailed(ILogger logger, Exception ex);
}
