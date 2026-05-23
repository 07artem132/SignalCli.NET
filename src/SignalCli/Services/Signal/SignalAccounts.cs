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

    public async Task<ListAccountsResponse> ListAccounts(CancellationToken cancellationToken = default)
    {
        SignalAccountsLog.ListAccountsRequested(_logger);

        try
        {
            var response = await _signalCliClient
                .InvokeMethodAsync<ListAccountsResponse, ListAccountsParameters>(
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
            SignalAccountsLog.ListAccountsTrace(_logger, string.Join(", ", response));

            return response;
        }
        catch (Exception ex)
        {
            SignalAccountsLog.ListAccountsFailed(_logger, ex);
            throw;
        }
    }

    public async Task<SyncAccountsResponse> SyncAccount(CancellationToken cancellationToken = default)
    {
        //todo work
        SignalAccountsLog.SyncAccountRequested(_logger);

        try
        {
            var response = await _signalCliClient
                .InvokeMethodAsync<SyncAccountsResponse, SyncAccountsParameters>(
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
        catch (Exception ex)
        {
            SignalAccountsLog.SyncAccountFailed(_logger, ex);
            throw;
        }
    }

}
