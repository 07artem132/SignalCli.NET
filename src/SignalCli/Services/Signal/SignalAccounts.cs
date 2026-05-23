using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Logging;
using SignalCli.Models.Signal.Accounts;

namespace SignalCli.Services.Signal;

// A.13: IDisposable прибрано — клас не тримає жодних ресурсів, порожній Dispose() лише плутав.
// post-modernize-tuning §8c.14 (audit N17): sealed — інхеріт не підтримується.
internal sealed class SignalAccounts(
    ISignalCliClient signalCliClient,
    ILogger<SignalAccounts> logger)
    : ISignalAccounts
{
    private readonly ISignalCliClient _signalCliClient = signalCliClient ?? throw new ArgumentNullException(nameof(signalCliClient));
    private readonly ILogger<SignalAccounts> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<ListAccountsResponse> ListAccountsAsync(CancellationToken cancellationToken = default)
    {
        // post-modernize-tuning §8c.7 (audit C8): bare catch-and-rethrow прибрано.
        // ActivitySource у JsonRpcClient.InvokeMethodAsync (§11.A.2) уже фіксує
        // exception-type-name на span'і, а consumer-callback може зробити власний log
        // у своєму exception-handler'і. Дублювати log без додавання context'у — шум.
        SignalAccountsLog.ListAccountsRequested(_logger);

        var response = await _signalCliClient
            .InvokeMethodAsync<ListAccountsParameters, ListAccountsResponse>(
                "listAccounts",
                new ListAccountsParameters(),
                cancellationToken).ConfigureAwait(false);

        if (response == null)
        {
            SignalAccountsLog.ListAccountsNullResponse(_logger);
            throw new InvalidOperationException("Отримано нульову відповідь від сервера");
        }

        // ПРИВАТНІСТЬ (F5): на Information логуємо лише кількість; Account-record містить
        // номер телефону/UUID, тож деталі — лише на Trace.
        SignalAccountsLog.ListAccountsOk(_logger, response.Count);
        // §5.8: `string.Join` оцінюється eagerly — `[LoggerMessage]` IsEnabled-guard всередині
        // методу не рятує від allocations на gen-call site. Обгортаємо вручну.
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            SignalAccountsLog.ListAccountsTrace(_logger, string.Join(", ", response));
        }

        return response;
    }

    public async Task<SyncAccountsResponse> SyncAccountAsync(CancellationToken cancellationToken = default)
    {
        // §8c.7: bare-catch прибрано (див. ListAccountsAsync).
        SignalAccountsLog.SyncAccountRequested(_logger);

        var response = await _signalCliClient
            .InvokeMethodAsync<SyncAccountsParameters, SyncAccountsResponse>(
                "sendSyncRequest",
                new SyncAccountsParameters(),
                cancellationToken).ConfigureAwait(false);

        if (response == null)
        {
            SignalAccountsLog.SyncAccountNullResponse(_logger);
            throw new InvalidOperationException("Отримано нульову відповідь від сервера");
        }

        // ПРИВАТНІСТЬ (F5): SyncAccountsResponse — порожній record (sendSyncRequest повертає лише факт),
        // тож на Information — лише факт виконання, без даних, які могли б містити PII.
        SignalAccountsLog.SyncAccountOk(_logger);

        return response;
    }

}
