using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Events;

/// <summary>signal-cli-api-coverage Wave 7b: unpin-message receive event args.</summary>
[PublicAPI]
public record UnpinMessageEventArgs(
    int SubscriptionId,
    string Account,
    JsonUnpinMessage UnpinMessage,
    string? Source,
    string? SourceNumber,
    string? SourceUuid,
    string? SourceName,
    int? SourceDevice,
    long Timestamp,
    long ServerReceivedTimestamp,
    long ServerDeliveredTimestamp
) : BaseSignalEventArgs(SubscriptionId, Account, Source, SourceNumber, SourceUuid, SourceName, SourceDevice, Timestamp, ServerReceivedTimestamp, ServerDeliveredTimestamp);
