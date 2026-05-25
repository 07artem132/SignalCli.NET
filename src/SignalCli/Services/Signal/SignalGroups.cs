using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Logging;
using SignalCli.Models.Signal.Groups;
using SignalCli.Serialization;

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
            .InvokeMethodAsync(
                "listGroups",
                new ListGroupsParameters(account),
                SignalJsonContext.Default.ListGroupsParameters,
                SignalJsonContext.Default.ListGroupsResponse,
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

    // ===== signal-cli-api-coverage Wave 2 (groups-crud) =====

    /// <inheritdoc/>
    public async Task<JoinGroupResponse> JoinGroupAsync(
        string account,
        string uri,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);
        ArgumentException.ThrowIfNullOrEmpty(uri);

        var response = await _signalCliClient
            .InvokeMethodAsync(
                "joinGroup",
                new JoinGroupParameters(account, uri),
                SignalJsonContext.Default.JoinGroupParameters,
                SignalJsonContext.Default.JoinGroupResponse,
                cancellationToken).ConfigureAwait(false);

        if (response == null)
        {
            SignalGroupsLog.GroupOpNullResponse(_logger, "joinGroup");
            throw new InvalidOperationException("Отримано нульову відповідь від сервера");
        }

        SignalGroupsLog.JoinGroupOk(_logger, response.TimeStamp, response.OnlyRequested == true);
        return response;
    }

    /// <inheritdoc/>
    public async Task<UpdateGroupResponse> UpdateGroupAsync(
        UpdateGroupOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        // §F14 dual-mode: groupId=null → upstream створить нову групу.
        // Enum→kebab mapping робимо у service-layer (Wave-1 pattern із ReceiptType).
        var parameters = new UpdateGroupParameters(
            Account: options.Account,
            GroupId: options.GroupId,
            Name: options.Name,
            Description: options.Description,
            Avatar: options.AvatarPath,
            Members: options.Members,
            RemoveMembers: options.RemoveMembers,
            Admins: options.Admins,
            RemoveAdmins: options.RemoveAdmins,
            Bans: options.Bans,
            Unbans: options.Unbans,
            ResetLink: options.ResetLink,
            Link: ToWireLinkState(options.LinkState),
            SetPermissionAddMember: ToWirePermission(options.PermissionAddMember),
            SetPermissionEditDetails: ToWirePermission(options.PermissionEditDetails),
            SetPermissionSendMessages: ToWirePermission(options.PermissionSendMessages),
            Expiration: options.Expiration);

        var response = await _signalCliClient
            .InvokeMethodAsync(
                "updateGroup",
                parameters,
                SignalJsonContext.Default.UpdateGroupParameters,
                SignalJsonContext.Default.UpdateGroupResponse,
                cancellationToken).ConfigureAwait(false);

        if (response == null)
        {
            SignalGroupsLog.GroupOpNullResponse(_logger, "updateGroup");
            throw new InvalidOperationException("Отримано нульову відповідь від сервера");
        }

        SignalGroupsLog.UpdateGroupOk(_logger, response.TimeStamp, created: response.GroupId is not null);
        return response;
    }

    /// <inheritdoc/>
    public async Task<QuitGroupResponse> QuitGroupAsync(
        string account,
        string groupId,
        bool deleteLocally = false,
        IEnumerable<string>? admins = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(account);
        ArgumentException.ThrowIfNullOrEmpty(groupId);

        var parameters = new QuitGroupParameters(
            Account: account,
            GroupId: groupId,
            Delete: deleteLocally,
            Admins: admins);

        var response = await _signalCliClient
            .InvokeMethodAsync(
                "quitGroup",
                parameters,
                SignalJsonContext.Default.QuitGroupParameters,
                SignalJsonContext.Default.QuitGroupResponse,
                cancellationToken).ConfigureAwait(false);

        if (response == null)
        {
            SignalGroupsLog.GroupOpNullResponse(_logger, "quitGroup");
            throw new InvalidOperationException("Отримано нульову відповідь від сервера");
        }

        // §F8: NotAGroupMemberException upstream → {} → response має обидва поля null.
        // Per CLAUDE.md rule #14 повертаємо success без винятку.
        if (response.WasAlreadyNotMember)
        {
            SignalGroupsLog.QuitGroupAlreadyNotMember(_logger);
        }
        else
        {
            SignalGroupsLog.QuitGroupOk(_logger, response.TimeStamp ?? 0L);
        }
        return response;
    }

    // Enum → kebab-case wire string (Wave-1 pattern із SignalMessage.SendReceiptAsync).
    // null → null (поле omitting на стороні STJ через DefaultIgnoreCondition.WhenWritingNull).
    private static string? ToWireLinkState(GroupLinkState? state) => state switch
    {
        null => null,
        GroupLinkState.Enabled => "enabled",
        GroupLinkState.EnabledWithApproval => "enabled-with-approval",
        GroupLinkState.Disabled => "disabled",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Невідоме значення GroupLinkState"),
    };

    private static string? ToWirePermission(GroupPermission? permission) => permission switch
    {
        null => null,
        GroupPermission.EveryMember => "every-member",
        GroupPermission.OnlyAdmins => "only-admins",
        _ => throw new ArgumentOutOfRangeException(nameof(permission), permission, "Невідоме значення GroupPermission"),
    };
}
