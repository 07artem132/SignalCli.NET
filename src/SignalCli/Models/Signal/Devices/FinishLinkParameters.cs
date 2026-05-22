using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Devices;

/// <summary>
/// Параметри для завершення зв'язування пристрою.
/// </summary>
/// <remarks>
/// Використовується для методу finishLink для завершення процесу
/// зв'язування нового пристрою з обліковим записом Signal.
/// </remarks>
/// <param name="deviceLinkUri">URI для зв'язування пристрою, отриманий після сканування QR-коду.</param>
/// <param name="deviceName">Назва нового пристрою.</param>
[PublicAPI]
public sealed record FinishLinkParameters(
    [property: JsonPropertyName("deviceLinkUri")]
    string deviceLinkUri,
    [property: JsonPropertyName("deviceName")] string deviceName);