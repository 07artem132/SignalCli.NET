using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.Rpc;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Logging;
using SignalCli.Models;

namespace SignalCli.Services.Rpc;

/// <summary>
/// Фабрика для створення екземплярів IJsonRpcClient.
/// </summary>
internal class JsonRpcClientFactory : IJsonRpcClientFactory
{
    private readonly ILogger<JsonRpcClient> _logger;
    private readonly IStreamPairProvider _streamPairProvider;
    private readonly Config _config;

    /// <summary>
    /// Створює новий екземпляр фабрики JSON-RPC клієнтів.
    /// </summary>
    /// <param name="logger">Логер для запису діагностичної інформації.</param>
    /// <param name="streamPairProvider">Постачальник потоків для взаємодії з зовнішнім процесом.</param>
    /// <param name="config">Конфігурація — використовується для отримання таймауту запитів.</param>
    public JsonRpcClientFactory(
        ILogger<JsonRpcClient> logger,
        IStreamPairProvider streamPairProvider,
        Config config)
    {
        _logger = logger;
        _streamPairProvider = streamPairProvider;
        _config = config;
    }

    /// <inheritdoc />
    public IJsonRpcClient Create()
    {
        var client = new JsonRpcClient(_logger, _streamPairProvider, _config);
        JsonRpcClientHostedServiceLog.FactoryClientCreated(_logger);
        return client;
    }
}
