using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Events;

/// <summary>signal-cli-api-coverage Wave 7b: payment-notification receive event args. JsonPayment shape fix.</summary>
[PublicAPI]
public record PaymentEventArgs(
    int SubscriptionId,
    string Account,
    JsonPayment Payment,
    string? Source,
    string? SourceNumber,
    string? SourceUuid,
    string? SourceName,
    int? SourceDevice,
    long Timestamp,
    long ServerReceivedTimestamp,
    long ServerDeliveredTimestamp
) : BaseSignalEventArgs(SubscriptionId, Account, Source, SourceNumber, SourceUuid, SourceName, SourceDevice, Timestamp, ServerReceivedTimestamp, ServerDeliveredTimestamp);
