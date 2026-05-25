using System.Text.Json;
using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Logging;
using SignalCli.Models.Signal.Contacts;
using SignalCli.Serialization;

namespace SignalCli.Services.Signal;

/// <summary>
/// signal-cli-api-coverage Wave 3 (contacts-identity). 8 RPC методів signal-cli (Trust
/// розщеплено на 2 type-safe методи). Void-методи (Trust*/UpdateContact/RemoveContact/
/// UpdateProfile/Block/Unblock) використовують <see cref="JsonElement"/> як response-тип
/// бо upstream emit'ить <c>"result": null</c> — JsonElement deserialize'ить null-значення
/// без exception (на відміну від reference-typed responses де <c>typedResult is null</c>
/// throw'ить).
/// </summary>
internal sealed class SignalContacts(
    ISignalCliClient signalCliClient,
    ILogger<SignalContacts> logger)
    : ISignalContacts
{
    private readonly ISignalCliClient _signalCliClient =
        signalCliClient ?? throw new ArgumentNullException(nameof(signalCliClient));

    private readonly ILogger<SignalContacts> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public async Task<ListContactsResponse> ListContactsAsync(
        string account,
        IEnumerable<string>? recipients = null,
        bool allRecipients = false,
        bool? blocked = null,
        string? name = null,
        bool includeInternal = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);

        var parameters = new ListContactsParameters(
            Account: account,
            Recipients: recipients,
            AllRecipients: allRecipients,
            Blocked: blocked,
            Name: name,
            Internal: includeInternal);

        var response = await _signalCliClient
            .InvokeMethodAsync(
                "listContacts",
                parameters,
                SignalJsonContext.Default.ListContactsParameters,
                SignalJsonContext.Default.ListContactsResponse,
                cancellationToken).ConfigureAwait(false);

        if (response == null)
            throw new InvalidOperationException("Отримано нульову відповідь від сервера");

        SignalContactsLog.ListContactsOk(_logger, response.Count);
        return response;
    }

    /// <inheritdoc/>
    public async Task<ListIdentitiesResponse> ListIdentitiesAsync(
        string account,
        string? recipient = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);

        var response = await _signalCliClient
            .InvokeMethodAsync(
                "listIdentities",
                new ListIdentitiesParameters(account, recipient),
                SignalJsonContext.Default.ListIdentitiesParameters,
                SignalJsonContext.Default.ListIdentitiesResponse,
                cancellationToken).ConfigureAwait(false);

        if (response == null)
            throw new InvalidOperationException("Отримано нульову відповідь від сервера");

        SignalContactsLog.ListIdentitiesOk(_logger, response.Count);
        return response;
    }

    /// <inheritdoc/>
    public async Task TrustAllKnownKeysAsync(
        string account,
        string recipient,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);
        ArgumentException.ThrowIfNullOrEmpty(recipient);

        await _signalCliClient
            .InvokeMethodAsync(
                "trust",
                new TrustParameters(account, recipient, TrustAllKnownKeys: true, VerifiedSafetyNumber: null),
                SignalJsonContext.Default.TrustParameters,
                SignalJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);

        SignalContactsLog.TrustOk(_logger, "AllKnownKeys");
    }

    /// <inheritdoc/>
    public async Task TrustVerifiedAsync(
        string account,
        string recipient,
        string verifiedSafetyNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);
        ArgumentException.ThrowIfNullOrEmpty(recipient);
        ArgumentException.ThrowIfNullOrEmpty(verifiedSafetyNumber);

        await _signalCliClient
            .InvokeMethodAsync(
                "trust",
                new TrustParameters(account, recipient, TrustAllKnownKeys: false, VerifiedSafetyNumber: verifiedSafetyNumber),
                SignalJsonContext.Default.TrustParameters,
                SignalJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);

        SignalContactsLog.TrustOk(_logger, "Verified");
    }

    /// <inheritdoc/>
    public async Task UpdateContactAsync(UpdateContactOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var parameters = new UpdateContactParameters(
            Account: options.Account,
            Recipient: options.Recipient,
            Name: options.Name,
            GivenName: options.GivenName,
            FamilyName: options.FamilyName,
            NickGivenName: options.NickGivenName,
            NickFamilyName: options.NickFamilyName,
            Note: options.Note,
            Expiration: options.Expiration);

        await _signalCliClient
            .InvokeMethodAsync(
                "updateContact",
                parameters,
                SignalJsonContext.Default.UpdateContactParameters,
                SignalJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);

        SignalContactsLog.UpdateContactOk(_logger);
    }

    /// <inheritdoc/>
    public async Task RemoveContactAsync(
        string account,
        string recipient,
        RemoveContactMode mode = RemoveContactMode.DeleteContact,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);
        ArgumentException.ThrowIfNullOrEmpty(recipient);

        // §F9: enum→XOR mapping. На wire — рівно одне з {hide=true, forget=true, обидва=false}.
        var (hide, forget) = mode switch
        {
            RemoveContactMode.DeleteContact => (false, false),
            RemoveContactMode.Hide => (true, false),
            RemoveContactMode.Forget => (false, true),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Невідоме значення RemoveContactMode"),
        };

        await _signalCliClient
            .InvokeMethodAsync(
                "removeContact",
                new RemoveContactParameters(account, recipient, hide, forget),
                SignalJsonContext.Default.RemoveContactParameters,
                SignalJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);

        SignalContactsLog.RemoveContactOk(_logger, mode.ToString());
    }

    /// <inheritdoc/>
    public async Task UpdateProfileAsync(UpdateProfileOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        // §F18 XOR вже enforce'нуто у Builder; повторно guard'имо service-side на випадок
        // прямої record-construction (не через Builder) — defense-in-depth.
        if (options.AvatarPath is not null && options.RemoveAvatar)
            throw new ArgumentException("§F18: AvatarPath та RemoveAvatar — взаємовиключні.", nameof(options));

        var parameters = new UpdateProfileParameters(
            Account: options.Account,
            GivenName: options.GivenName,
            FamilyName: options.FamilyName,
            About: options.About,
            AboutEmoji: options.AboutEmoji,
            MobileCoinAddress: options.MobileCoinAddress,
            Avatar: options.AvatarPath,
            RemoveAvatar: options.RemoveAvatar);

        await _signalCliClient
            .InvokeMethodAsync(
                "updateProfile",
                parameters,
                SignalJsonContext.Default.UpdateProfileParameters,
                SignalJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);

        SignalContactsLog.UpdateProfileOk(_logger);
    }

    /// <inheritdoc/>
    public async Task BlockAsync(
        string account,
        IEnumerable<string>? recipients = null,
        IEnumerable<string>? groupIds = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);

        var recipientList = recipients?.ToList();
        var groupList = groupIds?.ToList();

        await _signalCliClient
            .InvokeMethodAsync(
                "block",
                new BlockParameters(account, recipientList, groupList),
                SignalJsonContext.Default.BlockParameters,
                SignalJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);

        SignalContactsLog.BlockOk(_logger, recipientList?.Count ?? 0, groupList?.Count ?? 0);
    }

    /// <inheritdoc/>
    public async Task UnblockAsync(
        string account,
        IEnumerable<string>? recipients = null,
        IEnumerable<string>? groupIds = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);

        var recipientList = recipients?.ToList();
        var groupList = groupIds?.ToList();

        await _signalCliClient
            .InvokeMethodAsync(
                "unblock",
                new UnblockParameters(account, recipientList, groupList),
                SignalJsonContext.Default.UnblockParameters,
                SignalJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);

        SignalContactsLog.UnblockOk(_logger, recipientList?.Count ?? 0, groupList?.Count ?? 0);
    }
}
