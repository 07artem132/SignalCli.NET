using JetBrains.Annotations;
using Newtonsoft.Json;

namespace SignalCli.Models.Signal.Events;

/// <summary>
/// Параметри для запиту підписки на події Signal.
/// </summary>
/// <remarks>
/// Використовується в методі subscribeReceive для вказання облікового запису,
/// події якого потрібно отримувати.
/// </remarks>
/// <param name="Account">Ідентифікатор облікового запису (номер телефону).</param>
[PublicAPI]
public sealed record SubscribeReceiveParameters(
    [property: JsonProperty("account")] string Account);