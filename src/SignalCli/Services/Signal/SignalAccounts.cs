using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Logging;
using SignalCli.Models;
using SignalCli.Models.Signal.Accounts;
using SignalCli.Serialization;

namespace SignalCli.Services.Signal;

// A.13: IDisposable прибрано — клас не тримає жодних ресурсів, порожній Dispose() лише плутав.
// post-modernize-tuning §8c.14 (audit N17): sealed — інхеріт не підтримується.
//
// signal-cli-api-coverage Wave 6: + IOptions<SignalCliOptions> для destructive-ops gate.
// _destructiveOpsEnabled читається ОДИН раз у ctor per CLAUDE.md rule #10.
internal sealed class SignalAccounts(
    ISignalCliClient signalCliClient,
    ILogger<SignalAccounts> logger,
    IOptions<SignalCliOptions> options)
    : ISignalAccounts
{
    private readonly ISignalCliClient _signalCliClient = signalCliClient ?? throw new ArgumentNullException(nameof(signalCliClient));
    private readonly ILogger<SignalAccounts> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly bool _destructiveOpsEnabled =
        (options ?? throw new ArgumentNullException(nameof(options))).Value.EnableDestructiveOperations;

    public async Task<ListAccountsResponse> ListAccountsAsync(CancellationToken cancellationToken = default)
    {
        // post-modernize-tuning §8c.7 (audit C8): bare catch-and-rethrow прибрано.
        // ActivitySource у JsonRpcClient.InvokeMethodAsync (§11.A.2) уже фіксує
        // exception-type-name на span'і, а consumer-callback може зробити власний log
        // у своєму exception-handler'і. Дублювати log без додавання context'у — шум.
        SignalAccountsLog.ListAccountsRequested(_logger);

        var response = await _signalCliClient
            .InvokeMethodAsync(
                "listAccounts",
                new ListAccountsParameters(),
                SignalJsonContext.Default.ListAccountsParameters,
                SignalJsonContext.Default.ListAccountsResponse,
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
            .InvokeMethodAsync(
                "sendSyncRequest",
                new SyncAccountsParameters(),
                SignalJsonContext.Default.SyncAccountsParameters,
                SignalJsonContext.Default.SyncAccountsResponse,
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

    // ===== signal-cli-api-coverage Wave 6 (account-lifecycle, DESTRUCTIVE, opt-in gated) =====

    /// <summary>
    /// Гейтер для всіх destructive-методів. Кидає <see cref="InvalidOperationException"/> ПЕРЕД
    /// RPC-dispatch якщо <see cref="SignalCliOptions.EnableDestructiveOperations"/> = false.
    /// </summary>
    private void EnsureDestructiveAllowed([CallerMemberName] string? method = null)
    {
        if (!_destructiveOpsEnabled)
        {
            SignalAccountsLog.DestructiveOperationBlocked(_logger, method ?? "<unknown>");
            throw new InvalidOperationException(
                $"Destructive operation '{method}' заблоковано — set SignalCliOptions.EnableDestructiveOperations = true для opt-in. " +
                "⚠ ці operations не можна скасувати: irreversibly unregister/wipe local data/change phone number.");
        }
    }

    /// <inheritdoc/>
    public async Task<UpdateAccountResponse> UpdateAccountAsync(UpdateAccountOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        EnsureDestructiveAllowed();

        // §F3: NumberSharing pass-through bool? (не enum). §XOR: Builder уже enforce'нув;
        // defense-in-depth для direct record-construction.
        if (options.Username is not null && options.DeleteUsername)
            throw new ArgumentException("Username та DeleteUsername — взаємовиключні.", nameof(options));

        var parameters = new UpdateAccountParameters(
            Account: options.Account,
            DeviceName: options.DeviceName,
            UnrestrictedUnidentifiedSender: options.UnrestrictedUnidentifiedSender,
            DiscoverableByNumber: options.DiscoverableByNumber,
            NumberSharing: options.NumberSharing,
            Username: options.Username,
            DeleteUsername: options.DeleteUsername);

        var response = await _signalCliClient
            .InvokeMethodAsync(
                "updateAccount",
                parameters,
                SignalJsonContext.Default.UpdateAccountParameters,
                SignalJsonContext.Default.UpdateAccountResponse,
                cancellationToken).ConfigureAwait(false);

        // updateAccount response — non-null навіть для attribute-only updates ({}), але обидва
        // поля можуть бути null. Не throw — це валідна form.
        var result = response ?? new UpdateAccountResponse(null, null);
        SignalAccountsLog.UpdateAccountOk(_logger, usernameChanged: result.Username is not null);
        return result;
    }

    /// <inheritdoc/>
    public async Task UnregisterAsync(string account, bool deleteAccount = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);
        EnsureDestructiveAllowed();

        await _signalCliClient
            .InvokeMethodAsync(
                "unregister",
                new UnregisterParameters(account, deleteAccount),
                SignalJsonContext.Default.UnregisterParameters,
                SignalJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);

        SignalAccountsLog.UnregisterOk(_logger, deleteAccount);
    }

    /// <inheritdoc/>
    public async Task DeleteLocalAccountDataAsync(string account, bool ignoreRegistered = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);
        EnsureDestructiveAllowed();

        await _signalCliClient
            .InvokeMethodAsync(
                "deleteLocalAccountData",
                new DeleteLocalAccountDataParameters(account, ignoreRegistered),
                SignalJsonContext.Default.DeleteLocalAccountDataParameters,
                SignalJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);

        SignalAccountsLog.DeleteLocalAccountDataOk(_logger, ignoreRegistered);
    }

    /// <inheritdoc/>
    public async Task StartChangeNumberAsync(StartChangeNumberOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        EnsureDestructiveAllowed();

        await _signalCliClient
            .InvokeMethodAsync(
                "startChangeNumber",
                new StartChangeNumberParameters(
                    Account: options.Account,
                    Number: options.NewNumber,
                    Voice: options.Voice,
                    Captcha: options.Captcha),
                SignalJsonContext.Default.StartChangeNumberParameters,
                SignalJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);

        SignalAccountsLog.StartChangeNumberOk(_logger, options.Voice);
    }

    /// <inheritdoc/>
    public async Task FinishChangeNumberAsync(FinishChangeNumberOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        EnsureDestructiveAllowed();

        await _signalCliClient
            .InvokeMethodAsync(
                "finishChangeNumber",
                new FinishChangeNumberParameters(
                    Account: options.Account,
                    Number: options.NewNumber,
                    VerificationCode: options.VerificationCode,
                    Pin: options.Pin),
                SignalJsonContext.Default.FinishChangeNumberParameters,
                SignalJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);

        SignalAccountsLog.FinishChangeNumberOk(_logger);
    }

    /// <inheritdoc/>
    public async Task UpdateConfigurationAsync(UpdateConfigurationOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        EnsureDestructiveAllowed();

        await _signalCliClient
            .InvokeMethodAsync(
                "updateConfiguration",
                new UpdateConfigurationParameters(
                    Account: options.Account,
                    ReadReceipts: options.ReadReceipts,
                    UnidentifiedDeliveryIndicators: options.UnidentifiedDeliveryIndicators,
                    TypingIndicators: options.TypingIndicators,
                    LinkPreviews: options.LinkPreviews),
                SignalJsonContext.Default.UpdateConfigurationParameters,
                SignalJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);

        SignalAccountsLog.UpdateConfigurationOk(_logger);
    }

    /// <inheritdoc/>
    public async Task SetPinAsync(string account, string pin, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);
        ArgumentException.ThrowIfNullOrEmpty(pin);
        // Signal SVR rejects PIN'и шорше 4 chars (server-side). Pre-validate для clearer error.
        if (pin.Length < 4)
            throw new ArgumentException("PIN має бути ≥ 4 chars (Signal SVR requirement).", nameof(pin));
        EnsureDestructiveAllowed();

        await _signalCliClient
            .InvokeMethodAsync(
                "setPin",
                new SetPinParameters(account, pin),
                SignalJsonContext.Default.SetPinParameters,
                SignalJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);

        // Privacy: НЕ логуємо pin — це secret value.
        SignalAccountsLog.SetPinOk(_logger);
    }

    /// <inheritdoc/>
    public async Task RemovePinAsync(string account, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);
        EnsureDestructiveAllowed();

        await _signalCliClient
            .InvokeMethodAsync(
                "removePin",
                new RemovePinParameters(account),
                SignalJsonContext.Default.RemovePinParameters,
                SignalJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);

        SignalAccountsLog.RemovePinOk(_logger);
    }
}
