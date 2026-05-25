using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Message;

/// <summary>Опції для <c>sendAdminDelete</c>. <b>Group-only</b> — no DM support.</summary>
/// <remarks>
/// signal-cli-api-coverage Wave 7. Pinned до <c>SendAdminDeleteCommand.java @ bda4e7fc</c>.
/// Server-side rejects if sender не admin групи; client-side НЕ pre-validates.
/// </remarks>
[PublicAPI]
public sealed record SendAdminDeleteOptions
{
    /// <summary>E.164.</summary>
    public string Account { get; private set; } = string.Empty;
    /// <summary>Group ids (required — group-only operation).</summary>
    public IReadOnlyList<string> GroupIds { get; private set; } = [];
    /// <summary>NotifySelf.</summary>
    public bool NotifySelf { get; private set; }
    /// <summary>Phone-number автора повідомлення, що видаляється.</summary>
    public string TargetAuthor { get; private set; } = string.Empty;
    /// <summary>Timestamp повідомлення, що видаляється.</summary>
    public long TargetTimestamp { get; private set; }
    /// <summary>Якщо true — admin-delete story замість звичайного message.</summary>
    public bool Story { get; private set; }

    /// <summary>Builder.</summary>
    [PublicAPI]
    public sealed class Builder
    {
        private readonly SendAdminDeleteOptions _o;
        /// <summary>Ctor required: account + targetAuthor + targetTimestamp + ≥1 group.</summary>
        public Builder(string account, string targetAuthor, long targetTimestamp, IEnumerable<string> groupIds)
        {
            ArgumentException.ThrowIfNullOrEmpty(account);
            ArgumentException.ThrowIfNullOrEmpty(targetAuthor);
            ArgumentNullException.ThrowIfNull(groupIds);
            var list = groupIds.ToList();
            if (list.Count == 0)
                throw new ArgumentException("AdminDelete потребує ≥1 group-id (group-only operation).", nameof(groupIds));
            _o = new SendAdminDeleteOptions { Account = account, TargetAuthor = targetAuthor, TargetTimestamp = targetTimestamp, GroupIds = list };
        }
        /// <summary>NotifySelf.</summary>
        public Builder WithNotifySelf(bool v = true) { _o.NotifySelf = v; return this; }
        /// <summary>Mark as story.</summary>
        public Builder AsStoryDelete() { _o.Story = true; return this; }
        /// <summary>Build.</summary>
        public SendAdminDeleteOptions Build() => _o;
    }
}
