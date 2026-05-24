using SignalCli.Interfaces.FileSystem;
using SignalCli.Interfaces.Signal;

namespace SignalCli.Models.Signal.Message;

/// <summary>Опції для відправки повідомлення з вкладеннями. Створюється через <see cref="Builder"/>.</summary>
public record AttachmentMessageOptions
{
    /// <summary>Номер акаунту-відправника.</summary>
    public string Account { get; private set; } = string.Empty;
    /// <summary>Один або кілька отримувачів.</summary>
    public IEnumerable<IRecipient> Recipients { get; private set; } = [];
    /// <summary>Опціональне тіло повідомлення (підпис до вкладень).</summary>
    public string Message { get; private set; } = "";
    /// <summary>Список вкладень для відправки.</summary>
    public IEnumerable<IAttachmentEntry> Attachments { get; private set; } = [];
    /// <summary>Чи парсити markdown-стилізацію в тексті.</summary>
    public bool UseStyle { get; private set; }
    /// <summary>Згадки користувачів у тексті.</summary>
    public IEnumerable<string>? Mentions { get; private set; }


    /// <summary>Будівельник <see cref="AttachmentMessageOptions"/>.</summary>
    public class Builder
    {
        private readonly AttachmentMessageOptions _options;

        /// <summary>Створює будівельник з обов'язковими акаунтом, отримувачами та списком вкладень.</summary>
        /// <param name="account">Номер акаунту-відправника.</param>
        /// <param name="recipients">Отримувачі.</param>
        /// <param name="attachments">Вкладення.</param>
        public Builder(string account, List<IRecipient> recipients, List<IAttachmentEntry> attachments)
        {
            if (string.IsNullOrEmpty(account))
                throw new ArgumentException("Account обов'язковий.", nameof(account));
            if (recipients == null || recipients.Count == 0)
                throw new ArgumentException("Recipients обов'язковий.", nameof(recipients));
            if (attachments == null || attachments.Count == 0)
                throw new ArgumentException("Attachments обов'язковий.", nameof(attachments));

            _options = new AttachmentMessageOptions
            {
                Account = account,
                Recipients = recipients,
                Attachments = attachments
            };
        }

        /// <summary>Додати опціональний текст-підпис до вкладень.</summary>
        public Builder WithMessage(string message)
        {
            _options.Message = message;
            return this;
        }

        /// <summary>Увімкнути парсинг markdown-стилізації тексту.</summary>
        public Builder UseStyle()
        {
            _options.UseStyle = true;
            return this;
        }

        /// <summary>Додати згадки користувачів.</summary>
        public Builder WithMentions(IEnumerable<string> mentions)
        {
            _options.Mentions = mentions;
            return this;
        }

        /// <summary>Будує <see cref="AttachmentMessageOptions"/>.</summary>
        /// <exception cref="InvalidOperationException">
        /// post-modernize-tuning §4.15 (D9): post-mutation guard.
        /// </exception>
        public AttachmentMessageOptions Build()
        {
            if (string.IsNullOrEmpty(_options.Account))
                throw new InvalidOperationException("Account був скинутий після конструювання Builder.");
            if (!_options.Recipients.Any())
                throw new InvalidOperationException("Recipients було очищено після конструювання Builder.");
            if (!_options.Attachments.Any())
                throw new InvalidOperationException("Attachments було очищено після конструювання Builder.");
            return _options;
        }
    }
}
