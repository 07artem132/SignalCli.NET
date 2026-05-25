using System.Text.Json;
using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Logging;
using SignalCli.Models.Signal.Devices;
using SignalCli.Serialization;

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
            .InvokeMethodAsync(
                "startLink",
                new StartLinkParameters(),
                SignalJsonContext.Default.StartLinkParameters,
                SignalJsonContext.Default.StartLinkResponse,
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
            .InvokeMethodAsync(
                "finishLink",
                new FinishLinkParameters(deviceLinkUri, deviceName),
                SignalJsonContext.Default.FinishLinkParameters,
                SignalJsonContext.Default.FinishLinkResponse,
                cancellationToken).ConfigureAwait(false);

        if (response == null)
        {
            throw new InvalidOperationException("Отримано нульову відповідь від сервера");
        }

        return response;
    }

    // ===== signal-cli-api-coverage Wave 5 (device-management) =====

    /// <inheritdoc/>
    public async Task AddDeviceAsync(string account, string uri, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);
        ArgumentException.ThrowIfNullOrEmpty(uri);
        // §F11: pub_key у URI може бути unpadded base64 (DeviceLinkUrl.java:48). signal-cli
        // (Java Base64) приймає обидва форми, тож .NET сервіс pass-through URI без декодування.

        await _signalCliClient
            .InvokeMethodAsync(
                "addDevice",
                new AddDeviceParameters(account, uri),
                SignalJsonContext.Default.AddDeviceParameters,
                SignalJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);

        SignalDevicesLog.AddDeviceOk(_logger);
    }

    /// <inheritdoc/>
    public async Task<ListDevicesResponse> ListDevicesAsync(string account, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);

        var response = await _signalCliClient
            .InvokeMethodAsync(
                "listDevices",
                new ListDevicesParameters(account),
                SignalJsonContext.Default.ListDevicesParameters,
                SignalJsonContext.Default.ListDevicesResponse,
                cancellationToken).ConfigureAwait(false);

        if (response == null)
        {
            SignalDevicesLog.DeviceOpNullResponse(_logger, "listDevices");
            throw new InvalidOperationException("Отримано нульову відповідь від сервера");
        }

        SignalDevicesLog.ListDevicesOk(_logger, response.Count);
        return response;
    }

    /// <inheritdoc/>
    public async Task RemoveDeviceAsync(string account, int deviceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);
        if (deviceId <= 0)
            throw new ArgumentOutOfRangeException(nameof(deviceId), "DeviceId має бути > 0.");

        await _signalCliClient
            .InvokeMethodAsync(
                "removeDevice",
                new RemoveDeviceParameters(account, deviceId),
                SignalJsonContext.Default.RemoveDeviceParameters,
                SignalJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);

        SignalDevicesLog.RemoveDeviceOk(_logger, deviceId);
    }

    /// <inheritdoc/>
    public async Task UpdateDeviceAsync(
        string account,
        int deviceId,
        string deviceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);
        if (deviceId <= 0)
            throw new ArgumentOutOfRangeException(nameof(deviceId), "DeviceId має бути > 0.");
        // §F12: deviceName encrypted server-side. Empty-string не блокуємо client-side
        // (upstream не has empty-guard), але null-guard є для defensive programming.
        ArgumentNullException.ThrowIfNull(deviceName);

        await _signalCliClient
            .InvokeMethodAsync(
                "updateDevice",
                new UpdateDeviceParameters(account, deviceId, deviceName),
                SignalJsonContext.Default.UpdateDeviceParameters,
                SignalJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);

        // §F12: НЕ логуємо deviceName на Information level — це encrypted bytes.
        SignalDevicesLog.UpdateDeviceOk(_logger, deviceId);
    }
}