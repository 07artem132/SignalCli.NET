using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalCli.Interfaces.Rpc;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Logging;
using SignalCli.Models;

namespace SignalCli.Services.Rpc;

/// <summary>
/// Фабрика для створення екземплярів IJsonRpcClient.
/// </summary>
/// <remarks>
/// D.4: приймає <see cref="IOptions{SignalCliOptions}"/> замість legacy <c>Config</c>;
/// читає <c>options.Value</c> один раз у конструкторі (опції immutable).
/// </remarks>
internal class JsonRpcClientFactory : IJsonRpcClientFactory
{
    private readonly ILogger<JsonRpcClient> _logger;
    private readonly IStreamPairProvider _streamPairProvider;
    private readonly SignalCliOptions _options;

    public JsonRpcClientFactory(
        ILogger<JsonRpcClient> logger,
        IStreamPairProvider streamPairProvider,
        IOptions<SignalCliOptions> options)
    {
        _logger = logger;
        _streamPairProvider = streamPairProvider;
        _options = options.Value;
    }

    /// <inheritdoc />
    public IJsonRpcClient Create()
    {
        var client = new JsonRpcClient(_logger, _streamPairProvider, _options);
        JsonRpcClientHostedServiceLog.FactoryClientCreated(_logger);
        return client;
    }
}
