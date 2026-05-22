using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.Rpc;
using SignalCli.Interfaces.SignalCli;

namespace SignalCli.Services.Rpc;

/// <summary>
/// Фабрика для створення екземплярів IJsonRpcClient.
/// </summary>
internal class JsonRpcClientFactory : IJsonRpcClientFactory
{
    private readonly ILogger<JsonRpcClient> _logger;
    private readonly IStreamPairProvider _streamPairProvider;

    /// <summary>
    /// Створює новий екземпляр фабрики JSON-RPC клієнтів.
    /// </summary>
    /// <param name="logger">Логер для запису діагностичної інформації.</param>
    /// <param name="streamPairProvider">Постачальник потоків для взаємодії з зовнішнім процесом.</param>
    public JsonRpcClientFactory(
        ILogger<JsonRpcClient> logger,
        IStreamPairProvider streamPairProvider)
    {
        _logger = logger;
        _streamPairProvider = streamPairProvider;
    }

    /// <summary>
    /// Асинхронно створює новий екземпляр JSON-RPC клієнта.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Новий екземпляр JSON-RPC клієнта.</returns>
    public Task<IJsonRpcClient> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IJsonRpcClient client = new JsonRpcClient(
            _logger,
            _streamPairProvider
        );

        _logger.LogInformation("JsonRpcClient створено через JsonRpcClientFactory");
        return Task.FromResult(client);
    }
}