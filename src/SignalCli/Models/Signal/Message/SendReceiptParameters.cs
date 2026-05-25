using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Message;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>sendReceipt</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 1. §F7: <see cref="Recipient"/> — singular string, не список,
/// унікально серед Wave-1 send-методів. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/SendReceiptCommand.java:25 @ bda4e7fc</c>.
/// <para>
/// <see cref="Type"/> — lowercase wire string (<c>"read"</c>/<c>"viewed"</c>), а не Java-enum.
/// Сервісний метод мапить .NET <see cref="ReceiptType"/> у lowercase string перед серіалізацією.
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
/// <param name="Recipient">Phone/UUID отримувача (singular).</param>
/// <param name="TargetTimestamps">Список timestamps повідомлень.</param>
/// <param name="Type">"read" або "viewed" (lowercase per upstream argparse choices).</param>
[PublicAPI]
public sealed record SendReceiptParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("recipient")] string Recipient,
    [property: JsonPropertyName("target-timestamp")] IEnumerable<long> TargetTimestamps,
    [property: JsonPropertyName("type")] string Type);
