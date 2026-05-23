using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.Rpc;
using SignalCli.Interfaces.SignalCli;
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
                _logger.LogDebug("Виклик JSON-RPC методу: {Method}", method);

                var response = await _rpcClient.Client
                    .InvokeMethodAsync<TResponse, TRequest>(method, parameters, cancellationToken)
                    .ConfigureAwait(false);

                if (response is null)
                {
                    _logger.LogError("Отримано нульову відповідь від JSON-RPC методу {Method}", method);
                    throw new InvalidOperationException("Отримано нульову відповідь від сервера");
                }

                _logger.LogDebug("Метод {Method} повернув результат успішно", method);
                return response;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Помилка виклику JSON-RPC методу {Method}", method);
                throw;
            }
    }

    public async Task<VersionResponse> VersionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Отримання версії");

        try
        {
            var response = await InvokeMethodAsync<VersionResponse, VersionParameters>("version", new(), cancellationToken).ConfigureAwait(false);

            if (response == null)
            {
                _logger.LogError("Отримано нульову відповідь на запит версії");
                throw new InvalidOperationException("Отримано нульову відповідь від сервера");
            }

            _logger.LogInformation(
                "Версію отримано успішно. Версія={Version}", response.Version);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка отримання версії");
            throw;
        }
    }

}