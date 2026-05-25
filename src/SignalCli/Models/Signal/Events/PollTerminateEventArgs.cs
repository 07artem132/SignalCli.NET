using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Events;

/// <summary>signal-cli-api-coverage Wave 7b: poll-terminate receive event args.</summary>
[PublicAPI]
public record PollTerminateEventArgs(
    int SubscriptionId,
    string Account,
    JsonPollTerminate PollTerminate,
    string? Source,
    string? SourceNumber,
    string? SourceUuid,
    string? SourceName,
    int? SourceDevice,
    long Timestamp,
    long ServerReceivedTimestamp,
    long ServerDeliveredTimestamp
) : BaseSignalEventArgs(SubscriptionId, Account, Source, SourceNumber, SourceUuid, SourceName, SourceDevice, Timestamp, ServerReceivedTimestamp, ServerDeliveredTimestamp);
