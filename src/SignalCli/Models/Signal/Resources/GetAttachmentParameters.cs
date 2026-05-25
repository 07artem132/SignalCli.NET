using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Resources;

/// <summary>
/// Wire-DTO для JSON-RPC методу <c>getAttachment</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 4. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/GetAttachmentCommand.java @ bda4e7fc</c>.
/// <para>
/// <b>Quirk:</b> upstream argparse має mutex-required group <c>recipient</c>/<c>groupId</c>,
/// але <c>handleCommand</c> їх НЕ читає — vestigial argparse-noise. Ми НЕ відсилаємо ці поля
/// на wire — інакше довелось би вибрати фіктивний recipient, що додавало б risk silent
/// argparse-fail у майбутніх версіях. Якщо integration-test показує що signal-cli
/// reject'ить виклик без них — додамо optional поля у follow-up.
/// </para>
/// </remarks>
/// <param name="Account">E.164 номер акаунту.</param>
/// <param name="Id">ID attachment'а у локальному кеші signal-cli.</param>
[PublicAPI]
public sealed record GetAttachmentParameters(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("id")] string Id);
