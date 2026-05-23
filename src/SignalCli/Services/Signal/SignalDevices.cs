using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Logging;
using SignalCli.Models.Signal.Devices;

namespace SignalCli.Services.Signal;

// A.13: IDisposable прибрано — клас не тримає жодних ресурсів.
internal class SignalDevices(
    ISignalCliClient signalCliClient,
    ILogger<SignalDevices> logger)
    : ISignalDevices
{
    private readonly ISignalCliClient _signalCliClient = signalCliClient ?? throw new ArgumentNullException(nameof(signalCliClient));
    private readonly ILogger<SignalDevices> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<StartLinkResponse> StartLink(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _signalCliClient
                .InvokeMethodAsync<StartLinkResponse, StartLinkParameters>(
                    "startLink",
                    new StartLinkParameters(),
                    cancellationToken).ConfigureAwait(false);

            if (response == null)
            {
                throw new InvalidOperationException("Отримано нульову відповідь від сервера");
            }

            return response;
        }
        catch (Exception ex)
        {
            SignalDevicesLog.StartLinkFailed(_logger, ex);
            throw;
        }
    }

    public async Task<FinishLinkResponse> FinishLink(string deviceLinkUri, string deviceName, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _signalCliClient
                .InvokeMethodAsync<FinishLinkResponse, FinishLinkParameters>(
                    "finishLink",
                    new FinishLinkParameters(deviceLinkUri, deviceName),
                    cancellationToken).ConfigureAwait(false);

            if (response == null)
            {
                throw new InvalidOperationException("Отримано нульову відповідь від сервера");
            }

            return response;
        }
        catch (Exception ex)
        {
            SignalDevicesLog.FinishLinkFailed(_logger, ex);
            throw;
        }
    }

}