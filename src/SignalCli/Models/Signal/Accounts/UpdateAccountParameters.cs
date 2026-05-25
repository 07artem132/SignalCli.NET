using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>updateAccount</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 6 (account-lifecycle). Pinned до
/// <c>src/main/java/org/asamk/signal/commands/UpdateAccountCommand.java @ bda4e7fc</c>.
/// </remarks>
/// <param name="Account">E.164 номер.</param>
/// <param name="DeviceName">Нова назва device.</param>
/// <param name="UnrestrictedUnidentifiedSender">Toggle.</param>
/// <param name="DiscoverableByNumber">Toggle.</param>
/// <param name="NumberSharing">§F3: bool, не enum.</param>
/// <param name="Username">Новий username.</param>
/// <param name="DeleteUsername">Видалити username.</param>
[PublicAPI]
public sealed record UpdateAccountParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("deviceName")] string? DeviceName,
    [property: JsonPropertyName("unrestrictedUnidentifiedSender")] bool? UnrestrictedUnidentifiedSender,
    [property: JsonPropertyName("discoverableByNumber")] bool? DiscoverableByNumber,
    [property: JsonPropertyName("numberSharing")] bool? NumberSharing,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("deleteUsername")] bool DeleteUsername);
