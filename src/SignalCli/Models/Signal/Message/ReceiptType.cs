using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Message;

/// <summary>
/// Тип receipt-повідомлення, який відправляється на оригінального автора.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 1 (`messaging-interactive`). Wire-shape — lowercase string
/// (<c>"read"</c>/<c>"viewed"</c>), не Java-enum upper-snake — бо upstream визначає це як
/// argparse <c>.choices("read","viewed")</c>, а не enum-type. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/SendReceiptCommand.java:31-33 @ bda4e7fc</c>.
/// </remarks>
[PublicAPI]
public enum ReceiptType
{
    /// <summary>Receipt типу "read" — повідомлення прочитане.</summary>
    Read,

    /// <summary>Receipt типу "viewed" — view-once медіа переглянуте.</summary>
    Viewed,
}
