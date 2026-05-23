using SignalCli.Interfaces.Signal;

namespace SignalCli.Models.Signal.Message;

/// <summary>
/// Опції для відправки текстового повідомлення Signal.
/// Створюється через <see cref="Builder"/>.
/// </summary>
public record TextMessageOptions
{
    /// <summary>Номер акаунту-відправника.</summary>
    public string Account { get; private set; } = string.Empty;
    /// <summary>Один або кілька отримувачів (користувачі або одна група).</summary>
    public IEnumerable<IRecipient> Recipients { get; private set; } = [];
    /// <summary>Тіло повідомлення.</summary>
    public string Message { get; private set; } = string.Empty;
    /// <summary>Чи парсити markdown-подібну стилізацію (*курсив*, **жирний**, `код`, ~закреслений~, ||спойлер||).</summary>
    public bool UseStyle { get; private set; }
    /// <summary>Згадки (UUID/номери, на які слід посилатися у тексті).</summary>
    public IEnumerable<string>? Mentions { get; private set; }
    /// <summary>URL у попередньому перегляді посилання.</summary>
    public string? PreviewUrl { get; private set; }
    /// <summary>Заголовок попереднього перегляду посилання.</summary>
    public string? PreviewTitle { get; private set; }
    /// <summary>Опис попереднього перегляду посилання.</summary>
    public string? PreviewDescription { get; private set; }
    /// <summary>Зображення попереднього перегляду посилання (шлях або URI).</summary>
    public string? PreviewImage { get; private set; }
    /// <summary>Токен скасування відправлення.</summary>
    public CancellationToken CancellationToken { get; private set; } = CancellationToken.None;


    /// <summary>Будівельник <see cref="TextMessageOptions"/>.</summary>
    public class Builder
    {
        private readonly TextMessageOptions _options;

        /// <summary>Створює будівельник з обов'язковими параметрами акаунту, отримувачів та тексту.</summary>
        /// <param name="account">Номер акаунту-відправника.</param>
        /// <param name="recipients">Отримувачі.</param>
        /// <param name="message">Тіло повідомлення.</param>
        public Builder(string account, List<IRecipient> recipients, string message)
        {
            if (string.IsNullOrEmpty(account))
                throw new ArgumentException("Account обов'язковий.", nameof(account));
            if (recipients == null || recipients.Count == 0)
                throw new ArgumentException("Recipients обов'язковий.", nameof(recipients));
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Message обов'язковий.", nameof(message));

            _options = new TextMessageOptions
            {
                Account = account,
                Recipients = recipients,
                Message = message
            };
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

        /// <summary>Додати попередній перегляд посилання.</summary>
        public Builder WithPreview(string previewUrl, string previewTitle, string previewDescription, string previewImage)
        {
            _options.PreviewUrl = previewUrl;
            _options.PreviewTitle = previewTitle;
            _options.PreviewDescription = previewDescription;
            _options.PreviewImage = previewImage;
            return this;
        }

        /// <summary>Задати токен скасування відправки.</summary>
        public Builder WithCancellationToken(CancellationToken cancellationToken)
        {
            _options.CancellationToken = cancellationToken;
            return this;
        }

        /// <summary>Будує <see cref="TextMessageOptions"/>.</summary>
        public TextMessageOptions Build()
        {
            return _options;
        }
    }
}
