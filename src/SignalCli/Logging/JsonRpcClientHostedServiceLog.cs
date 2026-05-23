using Microsoft.Extensions.Logging;

namespace SignalCli.Logging;

/// <summary>
/// C.3: source-generated логи для <see cref="Services.Rpc.JsonRpcClientHostedService"/>
/// та <see cref="Services.Rpc.JsonRpcClientFactory"/>.
/// EventId-блок 400–499.
/// </summary>
internal static partial class JsonRpcClientHostedServiceLog
{
    [LoggerMessage(EventId = 400, Level = LogLevel.Information, Message = "JsonRpcClientHostedService починає роботу...")]
    public static partial void StartBegin(ILogger logger);

    [LoggerMessage(EventId = 401, Level = LogLevel.Information, Message = "Версія signal-cli JSON-RPC: {Version}")]
    public static partial void Version(ILogger logger, string? version);

    [LoggerMessage(EventId = 402, Level = LogLevel.Information, Message = "JsonRpcClientHostedService запущено - клієнт готовий")]
    public static partial void StartReady(ILogger logger);

    [LoggerMessage(EventId = 403, Level = LogLevel.Error, Message = "Помилка запуску JsonRpcClientHostedService")]
    public static partial void StartFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 404, Level = LogLevel.Information, Message = "JsonRpcClientHostedService зупиняється...")]
    public static partial void StopBegin(ILogger logger);

    [LoggerMessage(EventId = 405, Level = LogLevel.Information, Message = "JsonRpcClientHostedService зупинено.")]
    public static partial void Stopped(ILogger logger);

    [LoggerMessage(EventId = 406, Level = LogLevel.Error, Message = "Помилка зупинки JsonRpcClientHostedService")]
    public static partial void StopFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 407, Level = LogLevel.Error, Message = "Помилка при DisposeAsync у JsonRpcClientHostedService")]
    public static partial void DisposeFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 450, Level = LogLevel.Information, Message = "JsonRpcClient створено через JsonRpcClientFactory")]
    public static partial void FactoryClientCreated(ILogger logger);
}
