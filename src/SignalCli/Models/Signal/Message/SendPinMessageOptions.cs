using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Message;

/// <summary>Опції для <c>sendPinMessage</c>.</summary>
/// <remarks>
/// signal-cli-api-coverage Wave 7. Pinned до <c>SendPinMessageCommand.java @ bda4e7fc</c>.
/// <para><b>§F23 sentinel:</b> <see cref="PinDurationSeconds"/> = <c>-1</c> означає pin-forever.
/// Positive — seconds until auto-unpin. Default <c>-1</c>.</para>
/// <para><b>§F16:</b> send-side <c>int</c>; receive-side <c>JsonPinMessage.pinDurationSeconds</c> — <c>long</c>.</para>
/// </remarks>
[PublicAPI]
public sealed record SendPinMessageOptions
{
    /// <summary>E.164.</summary>
    public string Account { get; private set; } = string.Empty;
    /// <summary>Recipients.</summary>
    public IReadOnlyList<string>? Recipients { get; private set; }
    /// <summary>Group ids.</summary>
    public IReadOnlyList<string>? GroupIds { get; private set; }
    /// <summary>Usernames.</summary>
    public IReadOnlyList<string>? Usernames { get; private set; }
    /// <summary>NoteToSelf.</summary>
    public bool NoteToSelf { get; private set; }
    /// <summary>NotifySelf.</summary>
    public bool NotifySelf { get; private set; }
    /// <summary>§F23: <c>-1</c> = forever (default).</summary>
    public int PinDurationSeconds { get; private set; } = -1;
    /// <summary>Phone-number автора повідомлення, що пиниться.</summary>
    public string TargetAuthor { get; private set; } = string.Empty;
    /// <summary>Timestamp повідомлення, що пиниться.</summary>
    public long TargetTimestamp { get; private set; }
    /// <summary>Pin story замість message.</summary>
    public bool Story { get; private set; }

    /// <summary>Builder.</summary>
    [PublicAPI]
    public sealed class Builder
    {
        private readonly SendPinMessageOptions _o;
        /// <summary>Ctor required: account + targetAuthor + targetTimestamp.</summary>
        public Builder(string account, string targetAuthor, long targetTimestamp)
        {
            ArgumentException.ThrowIfNullOrEmpty(account);
            ArgumentException.ThrowIfNullOrEmpty(targetAuthor);
            _o = new SendPinMessageOptions { Account = account, TargetAuthor = targetAuthor, TargetTimestamp = targetTimestamp };
        }
        /// <summary>Recipients.</summary>
        public Builder WithRecipients(IEnumerable<string> r) { _o.Recipients = [.. r]; return this; }
        /// <summary>Group ids.</summary>
        public Builder WithGroupIds(IEnumerable<string> g) { _o.GroupIds = [.. g]; return this; }
        /// <summary>Usernames.</summary>
        public Builder WithUsernames(IEnumerable<string> u) { _o.Usernames = [.. u]; return this; }
        /// <summary>NoteToSelf.</summary>
        public Builder WithNoteToSelf(bool v = true) { _o.NoteToSelf = v; return this; }
        /// <summary>NotifySelf.</summary>
        public Builder WithNotifySelf(bool v = true) { _o.NotifySelf = v; return this; }
        /// <summary>§F23: positive seconds. Pass <c>-1</c> (or skip) для pin-forever.</summary>
        public Builder WithPinDurationSeconds(int seconds) { _o.PinDurationSeconds = seconds; return this; }
        /// <summary>Pin story.</summary>
        public Builder AsStoryPin() { _o.Story = true; return this; }
        /// <summary>Build.</summary>
        public SendPinMessageOptions Build() => _o;
    }
}
