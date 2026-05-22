using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Devices;

/// <summary>
/// Відповідь на запит ініціалізації зв'язування пристрою.
/// </summary>
/// <remarks>
/// Містить URI для створення QR-коду, який сканується новим пристроєм
/// для початку процесу зв'язування.
/// </remarks>
/// <param name="DeviceLinkUri">URI для зв'язування пристрою, використовується для генерації QR-коду.</param>
[PublicAPI]
public sealed record StartLinkResponse(string DeviceLinkUri);