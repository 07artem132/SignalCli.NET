using JetBrains.Annotations;
using System.Text.Json.Serialization;
using SignalCli.Models.Signal.Message;

namespace SignalCli.Models.Signal.Groups;

/// <summary>
/// Відповідь на <c>quitGroup</c>. Поля nullable бо §F8 idempotent NotAGroupMember case
/// повертає порожній <c>{}</c> — обидва <see cref="TimeStamp"/> та <see cref="Results"/>
/// будуть <c>null</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 2. Pinned до
/// <c>src/main/java/org/asamk/signal/util/SendMessageResultUtils.java:29-41 @ bda4e7fc</c> +
/// <c>QuitGroupCommand.java:59-61 @ bda4e7fc</c> (silent-log idempotent path).
/// </remarks>
/// <param name="TimeStamp">Send-timestamp quit-повідомлення. <c>null</c> якщо вже не member.</param>
/// <param name="Results">Per-recipient send results. <c>null</c> якщо вже не member.</param>
[PublicAPI]
public sealed record QuitGroupResponse(
    [property: JsonPropertyName("timestamp")] long? TimeStamp,
    [property: JsonPropertyName("results")] List<SendMessageResult>? Results)
{
    /// <summary>
    /// Зручний прапорець: <c>true</c> якщо це idempotent-no-op (акаунт уже не був member'ом
    /// групи перед викликом). Тоді upstream signal-cli мовчки логує "User is not a group member"
    /// та повертає <c>{}</c>.
    /// </summary>
    [JsonIgnore]
    public bool WasAlreadyNotMember => TimeStamp is null && Results is null;
}
