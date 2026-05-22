using SignalCli.Interfaces.Signal;

namespace SignalCli.Models.Signal.Message;


    // Опции для отправки текстового сообщения
    public record TextMessageOptions
    {
        public string Account { get; private set; }
        public IEnumerable<IRecipient> Recipients { get; private set; }
        public string Message { get; private set; }
        public bool UseStyle { get; private set; }
        public IEnumerable<string>? Mentions { get; private set; }
        public string? PreviewUrl { get; private set; }
        public string? PreviewTitle { get; private set; }
        public string? PreviewDescription { get; private set; }
        public string? PreviewImage { get; private set; }
        public CancellationToken CancellationToken { get; private set; } = CancellationToken.None;
        
        
        public class Builder
        {
            private readonly TextMessageOptions _options;
            
            // Обязательные первые 3 параметра: Account, Recipients, Message.
            public Builder(string account, List<IRecipient> recipients, string message)
            {
                if (string.IsNullOrEmpty(account))
                    throw new ArgumentException("Account обязателен.", nameof(account));
                if (recipients == null || recipients.Count == 0)
                    throw new ArgumentException("Recipients обязателен.", nameof(recipients));
                if (string.IsNullOrEmpty(message))
                    throw new ArgumentException("Message обязателен.", nameof(message));
                
                _options = new TextMessageOptions
                {
                    Account = account,
                    Recipients = recipients,
                    Message = message
                };
            }
            
            public Builder UseStyle()
            {
                _options.UseStyle = true;
                return this;
            }
            
            public Builder WithMentions(IEnumerable<string> mentions)
            {
                _options.Mentions = mentions;
                return this;
            }
            
            public Builder WithPreview(string previewUrl, string previewTitle, string previewDescription, string previewImage)
            {
                _options.PreviewUrl = previewUrl;
                _options.PreviewTitle = previewTitle;
                _options.PreviewDescription = previewDescription;
                _options.PreviewImage = previewImage;
                return this;
            }
            
            public Builder WithCancellationToken(CancellationToken cancellationToken)
            {
                _options.CancellationToken = cancellationToken;
                return this;
            }
            
            public TextMessageOptions Build()
            {
                return _options;
            }
        }
    }
