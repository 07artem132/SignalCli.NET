using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Devices;

/// <summary>
/// Відповідь на запит завершення зв'язування пристрою.
/// </summary>
/// <remarks>
/// Містить ідентифікатор облікового запису, з яким був зв'язаний пристрій.
/// Повертається після успішного завершення процесу зв'язування.
/// post-modernize-tuning §4.1 (audit D1): PascalCase property (Microsoft *Capitalization conventions*);
/// wire-level JSON field name `number` зберігається через <c>[JsonPropertyName]</c>.
/// </remarks>
/// <param name="Number">Номер телефону зв'язаного облікового запису.</param>
[PublicAPI]
public sealed record FinishLinkResponse([property: JsonPropertyName("number")] string Number);