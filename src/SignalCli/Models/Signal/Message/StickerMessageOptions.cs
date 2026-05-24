using SignalCli.Interfaces.Signal;

namespace SignalCli.Models.Signal.Message;

/// <summary>Опції для відправки стікера. Створюється через <see cref="Builder"/>.</summary>
public record StickerMessageOptions
{
    /// <summary>Номер акаунту-відправника.</summary>
    public string Account { get; private set; } = string.Empty;
    /// <summary>Один або кілька отримувачів.</summary>
    public IEnumerable<IRecipient> Recipients { get; private set; } = [];
    /// <summary>Ідентифікатор стікера у форматі signal-cli (packId:stickerId).</summary>
    public string Sticker { get; private set; } = string.Empty;
    /// <summary>Згадки користувачів (рідкісне використання для стікерів).</summary>
    public IEnumerable<string>? Mentions { get; private set; }

    /// <summary>Будівельник <see cref="StickerMessageOptions"/>.</summary>
    public class Builder
    {
        private readonly StickerMessageOptions _options;

        /// <summary>Створює будівельник з обов'язковими акаунтом, отримувачами та ідентифікатором стікера.</summary>
        /// <param name="account">Номер акаунту-відправника.</param>
        /// <param name="recipients">Отримувачі.</param>
        /// <param name="sticker">Ідентифікатор стікера.</param>
        public Builder(string account, List<IRecipient> recipients, string sticker)
        {
            if (string.IsNullOrEmpty(account))
                throw new ArgumentException("Account обов'язковий.", nameof(account));
            if (recipients == null || recipients.Count == 0)
                throw new ArgumentException("Recipients обов'язковий.", nameof(recipients));
            if (string.IsNullOrEmpty(sticker))
                throw new ArgumentException("Sticker обов'язковий.", nameof(sticker));

            _options = new StickerMessageOptions
            {
                Account = account,
                Recipients = recipients,
                Sticker = sticker
            };
        }

        /// <summary>Додати згадки користувачів.</summary>
        public Builder WithMentions(IEnumerable<string> mentions)
        {
            _options.Mentions = mentions;
            return this;
        }

        /// <summary>Будує <see cref="StickerMessageOptions"/>.</summary>
        /// <exception cref="InvalidOperationException">
        /// post-modernize-tuning §4.15 (D9): post-mutation guard.
        /// </exception>
        public StickerMessageOptions Build()
        {
            if (string.IsNullOrEmpty(_options.Account))
                throw new InvalidOperationException("Account був скинутий після конструювання Builder.");
            if (!_options.Recipients.Any())
                throw new InvalidOperationException("Recipients було очищено після конструювання Builder.");
            if (string.IsNullOrEmpty(_options.Sticker))
                throw new InvalidOperationException("Sticker був скинутий після конструювання Builder.");
            return _options;
        }
    }
}
