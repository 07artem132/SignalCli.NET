using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Devices;

/// <summary>
/// Параметри для запиту ініціалізації зв'язування пристрою.
/// </summary>
/// <remarks>
/// Порожній запис, що використовується з методом startLink
/// для початку процесу зв'язування нового пристрою.
/// </remarks>
[PublicAPI]
public sealed record StartLinkParameters;