using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Resources;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>getAvatar</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 4. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/GetAvatarCommand.java @ bda4e7fc</c>.
/// <para>
/// §F19 3-way XOR: рівно одне з <see cref="Contact"/>/<see cref="Profile"/>/<see cref="GroupId"/>
/// має бути non-null. Service-метод гарантує це через <see cref="GetAvatarOptions.Builder"/>.
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
/// <param name="Contact">Phone/UUID — local contact-карта. XOR.</param>
/// <param name="Profile">Phone/UUID — public profile avatar. XOR.</param>
/// <param name="GroupId">Base64 group-id. XOR.</param>
[PublicAPI]
public sealed record GetAvatarParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("contact")] string? Contact,
    [property: JsonPropertyName("profile")] string? Profile,
    [property: JsonPropertyName("groupId")] string? GroupId);
