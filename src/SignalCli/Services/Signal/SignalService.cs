using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.Rpc;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Logging;
using SignalCli.Models.SignalCli;

namespace SignalCli.Services.Signal;

// A.13: IDisposable прибрано — фасад над IJsonRpcClientProvider не тримає ресурсів.
internal class SignalService : ISignalCliClient
{
    private readonly IJsonRpcClientProvider _rpcClient;
    private readonly ILogger<SignalService> _logger;

    public SignalService(IJsonRpcClientProvider jsonRpcClientProvider, ILogger<SignalService> logger)
    {
        _rpcClient = jsonRpcClientProvider ?? throw new ArgumentNullException(nameof(jsonRpcClientProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> InvokeMethodAsync<TResponse, TRequest>(
        string method,
        TRequest parameters,
        CancellationToken cancellationToken = default) where TResponse : notnull
    {
            try
            {
                // ПРИВАТНІСТЬ: не логуємо параметри/результат — вони містять тіла повідомлень,
                // номери телефонів та вкладення. Логуємо лише назву методу.
                SignalServiceLog.InvokeMethod(_logger, method);

                var response = await _rpcClient.Client
                    .InvokeMethodAsync<TResponse, TRequest>(method, parameters, cancellationToken)
                    .ConfigureAwait(false);

                if (response is null)
                {
                    SignalServiceLog.InvokeMethodNullResponse(_logger, method);
                    throw new InvalidOperationException("Отримано нульову відповідь від сервера");
                }

                SignalServiceLog.InvokeMethodOk(_logger, method);
                return response;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                SignalServiceLog.InvokeMethodFailed(_logger, ex, method);
                throw;
            }
    }

    public async Task<VersionResponse> VersionAsync(CancellationToken cancellationToken = default)
    {
        SignalServiceLog.VersionRequested(_logger);

        try
        {
            var response = await InvokeMethodAsync<VersionResponse, VersionParameters>("version", new(), cancellationToken).ConfigureAwait(false);

            if (response == null)
            {
                SignalServiceLog.VersionNullResponse(_logger);
                throw new InvalidOperationException("Отримано нульову відповідь від сервера");
            }

            SignalServiceLog.VersionOk(_logger, response.Version);

            return response;
        }
        catch (Exception ex)
        {
            SignalServiceLog.VersionFailed(_logger, ex);
            throw;
        }
    }

}
