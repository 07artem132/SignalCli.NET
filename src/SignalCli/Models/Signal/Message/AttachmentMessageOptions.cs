using SignalCli.Interfaces.FileSystem;
using SignalCli.Interfaces.Signal;

namespace SignalCli.Models.Signal.Message;

      // Опции для отправки сообщения с вложениями
    public record AttachmentMessageOptions
    {
        public string Account { get; private set; }
        public IEnumerable<IRecipient> Recipients { get; private set; }
        // Сообщение оставляем опциональным
        public string Message { get; private set; } = "";
        public IEnumerable<IAttachmentEntry> Attachments { get; private set; }
        public bool UseStyle { get; private set; }
        public IEnumerable<string>? Mentions { get; private set; }
        public CancellationToken CancellationToken { get; private set; } = CancellationToken.None;
        
        
        public class Builder
        {
            private readonly AttachmentMessageOptions _options;
            
            // Обязательные параметры: первые 2 (Account, Recipients) и 4-й (Attachments).
            public Builder(string account, List<IRecipient> recipients, List<IAttachmentEntry> attachments)
            {
                if (string.IsNullOrEmpty(account))
                    throw new ArgumentException("Account обязателен.", nameof(account));
                if (recipients == null || recipients.Count == 0)
                    throw new ArgumentException("Recipients обязателен.", nameof(recipients));
                if (attachments == null || attachments.Count == 0)
                    throw new ArgumentException("Attachments обязателен.", nameof(attachments));
                    
                _options = new AttachmentMessageOptions
                {
                    Account = account,
                    Recipients = recipients,
                    Attachments = attachments
                };
            }
            
            // Опциональное задание сообщения
            public Builder WithMessage(string message)
            {
                _options.Message = message;
                return this;
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
            
            public Builder WithCancellationToken(CancellationToken cancellationToken)
            {
                _options.CancellationToken = cancellationToken;
                return this;
            }
            
            public AttachmentMessageOptions Build()
            {
                // Проверка обязательных полей уже выполнена в конструкторе.
                return _options;
            }
        }
    }
