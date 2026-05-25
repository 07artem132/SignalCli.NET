using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Message;

/// <summary>Опції для <c>sendPollVote</c>.</summary>
/// <remarks>
/// signal-cli-api-coverage Wave 7. Pinned до <c>SendPollVoteCommand.java @ bda4e7fc</c>.
/// <para><b>§F22:</b> <see cref="OptionIndexes"/> — zero-based індекси в original poll's options[].</para>
/// <para><b>VoteCount</b> — monotonic per-voter counter; consumer must increment by 1 on re-vote.</para>
/// </remarks>
[PublicAPI]
public sealed record SendPollVoteOptions
{
    /// <summary>E.164 номер.</summary>
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
    /// <summary>Phone-number of poll author (null = self).</summary>
    public string? PollAuthor { get; private set; }
    /// <summary>Timestamp оригіналу poll-create (identifies which poll).</summary>
    public long PollTimestamp { get; private set; }
    /// <summary>§F22 zero-based indexes до original options[]. Empty = clear vote.</summary>
    public IReadOnlyList<int> OptionIndexes { get; private set; } = [];
    /// <summary>Monotonic per-voter counter. Increment by 1 on re-vote.</summary>
    public int VoteCount { get; private set; }

    /// <summary>Builder.</summary>
    [PublicAPI]
    public sealed class Builder
    {
        private readonly SendPollVoteOptions _options;

        /// <summary>Ctor з required fields.</summary>
        public Builder(string account, long pollTimestamp, int voteCount)
        {
            ArgumentException.ThrowIfNullOrEmpty(account);
            _options = new SendPollVoteOptions { Account = account, PollTimestamp = pollTimestamp, VoteCount = voteCount };
        }

        /// <summary>Recipients.</summary>
        public Builder WithRecipients(IEnumerable<string> r) { _options.Recipients = [.. r]; return this; }
        /// <summary>Group ids.</summary>
        public Builder WithGroupIds(IEnumerable<string> g) { _options.GroupIds = [.. g]; return this; }
        /// <summary>Usernames.</summary>
        public Builder WithUsernames(IEnumerable<string> u) { _options.Usernames = [.. u]; return this; }
        /// <summary>NoteToSelf.</summary>
        public Builder WithNoteToSelf(bool v = true) { _options.NoteToSelf = v; return this; }
        /// <summary>NotifySelf.</summary>
        public Builder WithNotifySelf(bool v = true) { _options.NotifySelf = v; return this; }
        /// <summary>Explicit poll-author (null defaults to self при wire-dispatch).</summary>
        public Builder WithPollAuthor(string pollAuthor) { _options.PollAuthor = pollAuthor; return this; }
        /// <summary>§F22 zero-based option indexes. Empty list = clear vote.</summary>
        public Builder WithOptionIndexes(IEnumerable<int> indexes) { _options.OptionIndexes = [.. indexes]; return this; }

        /// <summary>Build.</summary>
        public SendPollVoteOptions Build() => _options;
    }
}
