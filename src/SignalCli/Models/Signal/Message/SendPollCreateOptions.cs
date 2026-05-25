using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Message;

/// <summary>
/// Опції для <c>sendPollCreate</c>. Створюється через <see cref="Builder"/>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 7. Pinned до
/// <c>SendPollCreateCommand.java @ bda4e7fc</c>.
/// <para>
/// <b>§F15 validation:</b> options.Count має бути 2-10, кожен option ≤100 chars.
/// Builder.Build() pre-validates client-side щоб уникнути upstream UserError.
/// </para>
/// <para>
/// <b>§F21 polarity:</b> .NET API expose'ить <c>AllowMultipleVotes</c> (default <c>true</c>),
/// яке мапиться у wire <c>noMulti = !AllowMultipleVotes</c> (upstream CLI flag inverted).
/// </para>
/// </remarks>
[PublicAPI]
public sealed record SendPollCreateOptions
{
    /// <summary>E.164 номер.</summary>
    public string Account { get; private set; } = string.Empty;
    /// <summary>Recipients (опційно — комбінується з інших targeting fields).</summary>
    public IReadOnlyList<string>? Recipients { get; private set; }
    /// <summary>Group-ids (опційно).</summary>
    public IReadOnlyList<string>? GroupIds { get; private set; }
    /// <summary>Usernames (опційно).</summary>
    public IReadOnlyList<string>? Usernames { get; private set; }
    /// <summary>Send до noteToSelf.</summary>
    public bool NoteToSelf { get; private set; }
    /// <summary>Self-copy semantics.</summary>
    public bool NotifySelf { get; private set; }
    /// <summary>Poll question text (required).</summary>
    public string Question { get; private set; } = string.Empty;
    /// <summary>§F21 polarity: default <c>true</c> (multi-vote дозволено).</summary>
    public bool AllowMultipleVotes { get; private set; } = true;
    /// <summary>Poll options (2-10 strings, кожен ≤100 chars per §F15).</summary>
    public IReadOnlyList<string> Options { get; private set; } = [];

    /// <summary>Builder.</summary>
    [PublicAPI]
    public sealed class Builder
    {
        private readonly SendPollCreateOptions _options;

        /// <summary>Ctor з required fields.</summary>
        public Builder(string account, string question, IEnumerable<string> pollOptions)
        {
            ArgumentException.ThrowIfNullOrEmpty(account);
            ArgumentException.ThrowIfNullOrEmpty(question);
            ArgumentNullException.ThrowIfNull(pollOptions);
            _options = new SendPollCreateOptions
            {
                Account = account,
                Question = question,
                Options = [.. pollOptions],
            };
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
        /// <summary>§F21: за замовчуванням true. Виставити false щоб обмежити voter одним варіантом.</summary>
        public Builder WithAllowMultipleVotes(bool v) { _options.AllowMultipleVotes = v; return this; }

        /// <summary>Будує + §F15 validation (2-10 options, ≤100 chars кожен).</summary>
        public SendPollCreateOptions Build()
        {
            if (_options.Options.Count < 2)
                throw new InvalidOperationException("§F15: Poll потребує щонайменше 2 опцій.");
            if (_options.Options.Count > 10)
                throw new InvalidOperationException("§F15: Poll не може мати більше 10 опцій.");
            foreach (var opt in _options.Options)
            {
                if (string.IsNullOrEmpty(opt))
                    throw new InvalidOperationException("§F15: option-string не може бути порожнім.");
                if (opt.Length > 100)
                    throw new InvalidOperationException($"§F15: option-string перевищує 100 chars: \"{opt[..Math.Min(30, opt.Length)]}...\".");
            }
            return _options;
        }
    }
}
