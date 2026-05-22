using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.Signal.Accounts;

namespace SignalCli.Services.Signal;

internal class SignalAccounts(
    ISignalCliClient signalCliClient,
    ILogger<SignalAccounts> logger)
    : ISignalAccounts, IDisposable
{
    private readonly ISignalCliClient _signalCliClient = signalCliClient ?? throw new ArgumentNullException(nameof(signalCliClient));
    private readonly ILogger<SignalAccounts> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private bool _disposed;

    public async Task<ListAccountsResponse> ListAccounts(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Отримання списку облікових записів");

        try
        {
            var response = await _signalCliClient
                .InvokeMethodAsync<ListAccountsResponse, ListAccountsParameters>(
                    "listAccounts",
                    new ListAccountsParameters(),
                    cancellationToken).ConfigureAwait(false);

            if (response == null)
            {
                _logger.LogError("Отримано нульову відповідь на listAccounts");
                throw new InvalidOperationException("Отримано нульову відповідь від сервера");
            }

            _logger.LogInformation(
                "Список облікових записів отримано успішно. Облікові записи={AccountList}",
                string.Join(", ", response)
            );

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка отримання списку облікових записів");
            throw;
        }
    }

    public async Task<SyncAccountsResponse> SyncAccount(CancellationToken cancellationToken = default)
    {
        //todo work
        _logger.LogDebug("Синхронізація облікових записів");

        try
        {
            var response = await _signalCliClient
                .InvokeMethodAsync<SyncAccountsResponse, SyncAccountsParameters>(
                    "sendSyncRequest",
                    new SyncAccountsParameters(),
                    cancellationToken).ConfigureAwait(false);

            if (response == null)
            {
                _logger.LogError("Отримано нульову відповідь на sendSyncRequest");
                throw new InvalidOperationException("Отримано нульову відповідь від сервера");
            }

            _logger.LogInformation(
                "Синхронізація облікових записів виконана успішно. Облікові записи={AccountList}",
                string.Join(", ", response)
            );

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка синхронізації облікових записів");
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}