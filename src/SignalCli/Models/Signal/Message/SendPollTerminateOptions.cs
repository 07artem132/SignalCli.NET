using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Message;

/// <summary>Опції для <c>sendPollTerminate</c>. NO poll-author — terminator MUST be original author.</summary>
/// <remarks>signal-cli-api-coverage Wave 7. Pinned до <c>SendPollTerminateCommand.java @ bda4e7fc</c>.</remarks>
[PublicAPI]
public sealed record SendPollTerminateOptions
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
    /// <summary>Timestamp оригіналу poll-create.</summary>
    public long PollTimestamp { get; private set; }

    /// <summary>Builder.</summary>
    [PublicAPI]
    public sealed class Builder
    {
        private readonly SendPollTerminateOptions _o;
        /// <summary>Ctor required: account + pollTimestamp.</summary>
        public Builder(string account, long pollTimestamp)
        {
            ArgumentException.ThrowIfNullOrEmpty(account);
            _o = new SendPollTerminateOptions { Account = account, PollTimestamp = pollTimestamp };
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
        /// <summary>Build.</summary>
        public SendPollTerminateOptions Build() => _o;
    }
}
