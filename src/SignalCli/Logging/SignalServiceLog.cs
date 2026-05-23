using Microsoft.Extensions.Logging;

namespace SignalCli.Logging;

/// <summary>
/// C.5: source-generated логи для <see cref="Services.Signal.SignalService"/>.
/// EventId-блок 600–699.
/// </summary>
internal static partial class SignalServiceLog
{
    [LoggerMessage(EventId = 600, Level = LogLevel.Debug, Message = "Виклик JSON-RPC методу: {Method}")]
    public static partial void InvokeMethod(ILogger logger, string method);

    [LoggerMessage(EventId = 601, Level = LogLevel.Error, Message = "Отримано нульову відповідь від JSON-RPC методу {Method}")]
    public static partial void InvokeMethodNullResponse(ILogger logger, string method);

    [LoggerMessage(EventId = 602, Level = LogLevel.Debug, Message = "Метод {Method} повернув результат успішно")]
    public static partial void InvokeMethodOk(ILogger logger, string method);

    [LoggerMessage(EventId = 603, Level = LogLevel.Error, Message = "Помилка виклику JSON-RPC методу {Method}")]
    public static partial void InvokeMethodFailed(ILogger logger, Exception ex, string method);

    [LoggerMessage(EventId = 604, Level = LogLevel.Debug, Message = "Отримання версії")]
    public static partial void VersionRequested(ILogger logger);

    [LoggerMessage(EventId = 605, Level = LogLevel.Error, Message = "Отримано нульову відповідь на запит версії")]
    public static partial void VersionNullResponse(ILogger logger);

    [LoggerMessage(EventId = 606, Level = LogLevel.Information, Message = "Версію отримано успішно. Версія={Version}")]
    public static partial void VersionOk(ILogger logger, string? version);

    [LoggerMessage(EventId = 607, Level = LogLevel.Error, Message = "Помилка отримання версії")]
    public static partial void VersionFailed(ILogger logger, Exception ex);
}
