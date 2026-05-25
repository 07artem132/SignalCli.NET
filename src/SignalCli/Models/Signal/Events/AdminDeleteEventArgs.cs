using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Events;

/// <summary>signal-cli-api-coverage Wave 7b: admin-delete receive event args (group moderation).</summary>
[PublicAPI]
public record AdminDeleteEventArgs(
    int SubscriptionId,
    string Account,
    JsonAdminDelete AdminDelete,
    string? Source,
    string? SourceNumber,
    string? SourceUuid,
    string? SourceName,
    int? SourceDevice,
    long Timestamp,
    long ServerReceivedTimestamp,
    long ServerDeliveredTimestamp
) : BaseSignalEventArgs(SubscriptionId, Account, Source, SourceNumber, SourceUuid, SourceName, SourceDevice, Timestamp, ServerReceivedTimestamp, ServerDeliveredTimestamp);
