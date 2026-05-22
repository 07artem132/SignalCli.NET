using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Events;

/// <summary>
/// Аргументи події Signal, отримані від JSON-RPC сервера.
/// </summary>
/// <remarks>
/// Є контейнером для конверта повідомлення, який містить інформацію про
/// тип повідомлення та його вміст. Використовується в нотифікаціях від Signal CLI.
/// </remarks>
/// <param name="Account">Ідентифікатор облікового запису одержувача.</param>
/// <param name="Envelope">Конверт повідомлення з детальною інформацією.</param>
[method: JsonConstructor]
[PublicAPI]
public record SignalEventArgs(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("envelope")] JsonMessageEnvelope? Envelope);