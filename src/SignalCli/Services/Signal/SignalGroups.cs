using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.Signal.Groups;

namespace SignalCli.Services.Signal;

internal class SignalGroups(
    ISignalCliClient signalCliClient,
    ILogger<SignalGroups> logger)
    : ISignalGroups, IDisposable
{
    private readonly ISignalCliClient _signalCliClient = signalCliClient ?? throw new ArgumentNullException(nameof(signalCliClient));
    private readonly ILogger<SignalGroups> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private bool _disposed;

    public async Task<ListGroupsResponse> ListGroups(string account, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Отримання списку груп");

        try
        {
            var response = await _signalCliClient
                .InvokeMethodAsync<ListGroupsResponse, ListGroupsParameters>(
                    "listGroups",
                    new ListGroupsParameters(account),
                    cancellationToken).ConfigureAwait(false);

            if (response == null)
            {
                _logger.LogError("Отримано нульову відповідь на listGroups");
                throw new InvalidOperationException("Отримано нульову відповідь від сервера");
            }

            _logger.LogInformation(
                "Список груп отримано успішно. Групи={listGroups}",
                string.Join(", ", response)
            );

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка отримання списку груп");
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}