using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Logging;
using SignalCli.Models.Signal.Groups;

namespace SignalCli.Services.Signal;

// A.13: IDisposable прибрано — клас не тримає жодних ресурсів.
internal class SignalGroups(
    ISignalCliClient signalCliClient,
    ILogger<SignalGroups> logger)
    : ISignalGroups
{
    private readonly ISignalCliClient _signalCliClient = signalCliClient ?? throw new ArgumentNullException(nameof(signalCliClient));
    private readonly ILogger<SignalGroups> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<ListGroupsResponse> ListGroups(string account, CancellationToken cancellationToken = default)
    {
        SignalGroupsLog.ListGroupsRequested(_logger);

        try
        {
            var response = await _signalCliClient
                .InvokeMethodAsync<ListGroupsResponse, ListGroupsParameters>(
                    "listGroups",
                    new ListGroupsParameters(account),
                    cancellationToken).ConfigureAwait(false);

            if (response == null)
            {
                SignalGroupsLog.ListGroupsNullResponse(_logger);
                throw new InvalidOperationException("Отримано нульову відповідь від сервера");
            }

            // ПРИВАТНІСТЬ (F5): Group/Member записи в response містять PII (members, назви, IDs);
            // на Information — лише кількість, повні деталі — Trace.
            SignalGroupsLog.ListGroupsOk(_logger, response.Count);
            SignalGroupsLog.ListGroupsTrace(_logger, string.Join(", ", response));

            return response;
        }
        catch (Exception ex)
        {
            SignalGroupsLog.ListGroupsFailed(_logger, ex);
            throw;
        }
    }

}
