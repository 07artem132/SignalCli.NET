using SignalCli.Interfaces.Signal;

namespace SignalCli.Models.Signal.Message;

public record StickerMessageOptions
{
    public string Account { get; private set; }
    public IEnumerable<IRecipient> Recipients { get; private set; }
    public string Sticker { get; private set; }
    public IEnumerable<string>? Mentions { get; private set; }
    public CancellationToken CancellationToken { get; private set; } = CancellationToken.None;
        
    public class Builder
    {
        private readonly StickerMessageOptions _options;
            
        // Обязательные первые 3 параметра: Account, Recipients, Sticker.
        public Builder(string account, List<IRecipient> recipients, string sticker)
        {
            if (string.IsNullOrEmpty(account))
                throw new ArgumentException("Account обязателен.", nameof(account));
            if (recipients == null || !recipients.Any())
                throw new ArgumentException("Recipients обязателен.", nameof(recipients));
            if (string.IsNullOrEmpty(sticker))
                throw new ArgumentException("Sticker обязателен.", nameof(sticker));
                    
            _options = new StickerMessageOptions
            {
                Account = account,
                Recipients = recipients,
                Sticker = sticker
            };
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
            
        public StickerMessageOptions Build()
        {
            return _options;
        }
    }
}
