using JetBrains.Annotations;
using Newtonsoft.Json;

namespace SignalCli.Models.Signal.Groups;

/// <summary>
/// Параметри запиту списку груп Signal.
/// </summary>
/// <remarks>
/// Використовується для методу listGroups, щоб отримати групи для вказаного акаунту.
/// </remarks>
/// <param name="Account">Ідентифікатор облікового запису (номер телефону).</param>
[PublicAPI]
public record ListGroupsParameters(
    [property: JsonProperty("account", NullValueHandling = NullValueHandling.Ignore)]
    string Account);