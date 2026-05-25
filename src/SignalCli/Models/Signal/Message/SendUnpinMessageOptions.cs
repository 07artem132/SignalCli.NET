using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Message;

/// <summary>Опції для <c>sendUnpinMessage</c>. Symmetric до Pin минус <c>PinDurationSeconds</c>.</summary>
/// <remarks>signal-cli-api-coverage Wave 7. Pinned до <c>SendUnpinMessageCommand.java @ bda4e7fc</c>.</remarks>
[PublicAPI]
public sealed record SendUnpinMessageOptions
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
    /// <summary>Phone-number автора оригіналу.</summary>
    public string TargetAuthor { get; private set; } = string.Empty;
    /// <summary>Timestamp оригіналу.</summary>
    public long TargetTimestamp { get; private set; }
    /// <summary>Unpin story.</summary>
    public bool Story { get; private set; }

    /// <summary>Builder.</summary>
    [PublicAPI]
    public sealed class Builder
    {
        private readonly SendUnpinMessageOptions _o;
        /// <summary>Ctor required: account + targetAuthor + targetTimestamp.</summary>
        public Builder(string account, string targetAuthor, long targetTimestamp)
        {
            ArgumentException.ThrowIfNullOrEmpty(account);
            ArgumentException.ThrowIfNullOrEmpty(targetAuthor);
            _o = new SendUnpinMessageOptions { Account = account, TargetAuthor = targetAuthor, TargetTimestamp = targetTimestamp };
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
        /// <summary>Unpin story.</summary>
        public Builder AsStoryUnpin() { _o.Story = true; return this; }
        /// <summary>Build.</summary>
        public SendUnpinMessageOptions Build() => _o;
    }
}
