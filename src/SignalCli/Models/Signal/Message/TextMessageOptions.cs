using System.Diagnostics.CodeAnalysis;
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
    [StringSyntax(StringSyntaxAttribute.Uri)]
    public string? PreviewUrl { get; private set; }
    /// <summary>Заголовок попереднього перегляду посилання.</summary>
    public string? PreviewTitle { get; private set; }
    /// <summary>Опис попереднього перегляду посилання.</summary>
    public string? PreviewDescription { get; private set; }
    /// <summary>Зображення попереднього перегляду посилання (шлях або URI).</summary>
    [StringSyntax(StringSyntaxAttribute.Uri)]
    public string? PreviewImage { get; private set; }


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
        public Builder WithPreview(
            [StringSyntax(StringSyntaxAttribute.Uri)] string previewUrl,
            string previewTitle,
            string previewDescription,
            [StringSyntax(StringSyntaxAttribute.Uri)] string previewImage)
        {
            _options.PreviewUrl = previewUrl;
            _options.PreviewTitle = previewTitle;
            _options.PreviewDescription = previewDescription;
            _options.PreviewImage = previewImage;
            return this;
        }

        /// <summary>Будує <see cref="TextMessageOptions"/>.</summary>
        /// <exception cref="InvalidOperationException">
        /// post-modernize-tuning §4.15 (D9): post-mutation guard — якщо хтось обнулив
        /// обов'язкове поле через reflection / set-через-record-with, ловимо тут.
        /// Ctor-validation у Builder ловить початковий стан; цей guard — захист від
        /// мутацій між ctor і Build.
        /// </exception>
        public TextMessageOptions Build()
        {
            if (string.IsNullOrEmpty(_options.Account))
                throw new InvalidOperationException("Account був скинутий після конструювання Builder.");
            if (!_options.Recipients.Any())
                throw new InvalidOperationException("Recipients було очищено після конструювання Builder.");
            if (string.IsNullOrEmpty(_options.Message))
                throw new InvalidOperationException("Message був скинутий після конструювання Builder.");
            return _options;
        }
    }
}
