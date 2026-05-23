using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Logging;
using SignalCli.Models.Signal.Groups;

namespace SignalCli.Services.Signal;

// A.13: IDisposable прибрано — клас не тримає жодних ресурсів.
// post-modernize-tuning §8c.14 (audit N17): sealed — інхеріт не підтримується.
internal sealed class SignalGroups(
    ISignalCliClient signalCliClient,
    ILogger<SignalGroups> logger)
    : ISignalGroups
{
    private readonly ISignalCliClient _signalCliClient = signalCliClient ?? throw new ArgumentNullException(nameof(signalCliClient));
    private readonly ILogger<SignalGroups> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<ListGroupsResponse> ListGroupsAsync(string account, CancellationToken cancellationToken = default)
    {
        // post-modernize-tuning §8c.11 (audit N5): validate at the boundary.
        ArgumentException.ThrowIfNullOrEmpty(account);
        // §8c.7: bare-catch прибрано — ActivitySource у JsonRpcClient уже фіксує тип винятку.
        SignalGroupsLog.ListGroupsRequested(_logger);

        var response = await _signalCliClient
            .InvokeMethodAsync<ListGroupsParameters, ListGroupsResponse>(
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
        // §5.8: eager-evaluation of `string.Join` happens before the gen-call site;
        // the generated IsEnabled-guard runs too late to skip the allocation.
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            SignalGroupsLog.ListGroupsTrace(_logger, string.Join(", ", response));
        }

        return response;
    }

}
