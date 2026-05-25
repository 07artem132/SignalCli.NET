using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Message;

/// <summary>
/// Type для <c>sendMessageRequestResponse</c> — лише 2 send-side values per §F2.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 7 (`messaging-power-user`). Pinned до
/// <c>src/main/java/org/asamk/signal/commands/MessageRequestResponseType.java @ bda4e7fc</c>
/// (CLI-layer 2-value enum).
/// <para>
/// <b>§F2 critical:</b> upstream <i>send-side</i> CLI restricts до ACCEPT/DELETE. Receive-side
/// <c>MessageEnvelope.Sync.MessageRequestResponse.Type</c> має 8 значень
/// (UNKNOWN/ACCEPT/DELETE/BLOCK/BLOCK_AND_DELETE/UNBLOCK_AND_ACCEPT/SPAM/BLOCK_AND_SPAM) —
/// але вони доступні ЛИШЕ для decoding incoming sync messages, не для send. Block-style
/// operations виконуй через Wave-3 <c>BlockAsync</c>/<c>UnblockAsync</c>.
/// </para>
/// </remarks>
[PublicAPI]
public enum MessageRequestResponseType
{
    /// <summary>Прийняти message-request — unblocks conversation.</summary>
    Accept,

    /// <summary>Видалити message-request — removes з inbox.</summary>
    Delete,
}
