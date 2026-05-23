using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Logging;
using SignalCli.Models.Signal.Devices;

namespace SignalCli.Services.Signal;

// A.13: IDisposable прибрано — клас не тримає жодних ресурсів.
// post-modernize-tuning §8c.14 (audit N17): sealed — інхеріт не підтримується.
internal sealed class SignalDevices(
    ISignalCliClient signalCliClient,
    ILogger<SignalDevices> logger)
    : ISignalDevices
{
    private readonly ISignalCliClient _signalCliClient = signalCliClient ?? throw new ArgumentNullException(nameof(signalCliClient));
    private readonly ILogger<SignalDevices> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<StartLinkResponse> StartLinkAsync(CancellationToken cancellationToken = default)
    {
        // post-modernize-tuning §8c.7/§8c.13 (audit C8/N16): bare-catch прибрано — ActivitySource
        // у JsonRpcClient (§11.A.2) уже фіксує тип винятку. Entry-level log залишено.
        SignalDevicesLog.StartLinkRequested(_logger);
        var response = await _signalCliClient
            .InvokeMethodAsync<StartLinkParameters, StartLinkResponse>(
                "startLink",
                new StartLinkParameters(),
                cancellationToken).ConfigureAwait(false);

        if (response == null)
        {
            throw new InvalidOperationException("Отримано нульову відповідь від сервера");
        }

        return response;
    }

    public async Task<FinishLinkResponse> FinishLinkAsync(string deviceLinkUri, string deviceName, CancellationToken cancellationToken = default)
    {
        // post-modernize-tuning §8c.11 (audit N5): validate inputs at the boundary —
        // ArgumentException for null/empty замість 400-class signal-cli-помилки після RPC.
        ArgumentException.ThrowIfNullOrEmpty(deviceLinkUri);
        ArgumentException.ThrowIfNullOrEmpty(deviceName);

        // §8c.7: bare-catch прибрано.
        SignalDevicesLog.FinishLinkRequested(_logger, deviceName);
        var response = await _signalCliClient
            .InvokeMethodAsync<FinishLinkParameters, FinishLinkResponse>(
                "finishLink",
                new FinishLinkParameters(deviceLinkUri, deviceName),
                cancellationToken).ConfigureAwait(false);

        if (response == null)
        {
            throw new InvalidOperationException("Отримано нульову відповідь від сервера");
        }

        return response;
    }

}