using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Message;

/// <summary>Wire-DTO для <c>sendMessageRequestResponse</c>.</summary>
/// <remarks>
/// signal-cli-api-coverage Wave 7. §F2: send-side <see cref="Type"/> wire — lowercase
/// <c>"accept"</c>/<c>"delete"</c> (argparse <c>.choices(...)</c> з lowercase). Service-метод
/// мапить .NET <see cref="MessageRequestResponseType"/> у відповідну lowercase string.
/// </remarks>
[PublicAPI]
public sealed record SendMessageRequestResponseParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("recipient")] IEnumerable<string>? Recipients,
    [property: JsonPropertyName("groupId")] IEnumerable<string>? GroupIds,
    [property: JsonPropertyName("username")] IEnumerable<string>? Usernames,
    [property: JsonPropertyName("type")] string Type);
