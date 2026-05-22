using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Devices;

/// <summary>
/// Відповідь на запит завершення зв'язування пристрою.
/// </summary>
/// <remarks>
/// Містить ідентифікатор облікового запису, з яким був зв'язаний пристрій.
/// Повертається після успішного завершення процесу зв'язування.
/// </remarks>
/// <param name="number">Номер телефону зв'язаного облікового запису.</param>
[PublicAPI]
public sealed record FinishLinkResponse(string number);