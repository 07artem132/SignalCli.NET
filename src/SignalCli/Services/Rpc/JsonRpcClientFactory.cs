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
// post-modernize-tuning §8c.14 (audit N17): sealed — інхеріт не підтримується.
internal sealed class JsonRpcClientFactory : IJsonRpcClientFactory
{
    private readonly ILogger<JsonRpcClient> _logger;
    private readonly IStreamPairProvider _streamPairProvider;
    private readonly SignalCliOptions _options;
    // audit N4: TimeProvider прокидається у JsonRpcClient для CancellationTokenSource(timeout, TimeProvider).
    private readonly TimeProvider _timeProvider;

    public JsonRpcClientFactory(
        ILogger<JsonRpcClient> logger,
        IStreamPairProvider streamPairProvider,
        IOptions<SignalCliOptions> options,
        TimeProvider? timeProvider = null)
    {
        _logger = logger;
        _streamPairProvider = streamPairProvider;
        _options = options.Value;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public IJsonRpcClient Create()
    {
        var client = new JsonRpcClient(_logger, _streamPairProvider, _options, _timeProvider);
        JsonRpcClientHostedServiceLog.FactoryClientCreated(_logger);
        return client;
    }
}
