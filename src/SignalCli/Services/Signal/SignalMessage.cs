using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.FileSystem;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.Signal.Message;
using SignalCli.Services.FileSystem;
using SignalCli.Utilities;

namespace SignalCli.Services.Signal
{
    internal sealed class SignalMessage(ISignalCliClient signalCliClient, ILogger<SignalMessage> logger)
        : ISignalMessage, IDisposable
    {
        private readonly ISignalCliClient _signalCliClient =
            signalCliClient ?? throw new ArgumentNullException(nameof(signalCliClient));

        private readonly ILogger<SignalMessage> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private bool _disposed;

        private async Task<SendMessageResponse> SendUnifiedMessageAsync(
            string account,
            IEnumerable<IRecipient> recipients,
            string message,
            bool noteToSelf,
            bool endSession,
            // Режим стилізації, наприклад "styled" або null
            string? textMode,
            // Вкладення, що реалізують IAttachmentEntry (конвертуються в Data URI)
            IEnumerable<IAttachmentEntry>? attachments,
            IEnumerable<string>? mentions,
            ulong? quoteTimestamp,
            string? quoteAuthor,
            string? quoteMessage,
            IEnumerable<string>? quoteMentions,
            IEnumerable<string>? quoteTextStyles,
            IEnumerable<string>? quoteAttachments,
            IEnumerable<string>? externalTextStyles,
            ulong? editTimestamp,
            string? sticker,
            string? previewUrl,
            string? previewTitle,
            string? previewDescription,
            string? previewImage,
            ulong? storyTimestamp,
            string? storyAuthor,
            CancellationToken cancellationToken = default)
        {
            ValidateRecipients(recipients);

            // Якщо увімкнено режим стилізації, обробляємо текст за допомогою TextStyleParser і отримуємо форматувальні рядки
            List<string> parsedTextStyles = [];
            if (!string.IsNullOrEmpty(textMode) &&
                textMode.Equals("styled", StringComparison.OrdinalIgnoreCase))
            {
                var parser = new TextStyleParser(message);
                (message, parsedTextStyles) = parser.Parse();
            }

            // Розділяємо отримувачів на звичайних (користувачів) і групових
            var userRecipients = recipients.Where(r => !r.IsGroup)
                .Select(r => r.Identifier)
                .ToList();
            var groupRecipients = recipients.Where(r => r.IsGroup)
                .Select(r => r.Identifier)
                .ToList();
            if (groupRecipients.Count > 1)
            {
                throw new ArgumentException("Для групових повідомлень допускається лише один отримувач",
                    nameof(recipients));
            }

            if (groupRecipients.Count > 1 && userRecipients.Count > 1)
            {
                throw new ArgumentException("Допускаэться лише один тип отримувача", nameof(recipients));
            }

            if ((quoteTimestamp != null || quoteAuthor != null || quoteMessage != null) &&
                (quoteTimestamp == null || quoteAuthor == null || quoteMessage == null))
            {
                throw new ArgumentException(
                    "Для цитирования сообщения необходимо указать все три параметра: " +
                    $"{nameof(quoteTimestamp)}, {nameof(quoteAuthor)} и {nameof(quoteMessage)}",
                    string.Join(", ", nameof(quoteTimestamp), nameof(quoteAuthor), nameof(quoteMessage))
                );
            }

            // Обробка вкладень: конвертуємо кожне вкладення в рядок Data URI
            List<string> processedAttachments = [];
            var attachmentEntries = attachments as IAttachmentEntry[] ??
                                    (attachments ?? Array.Empty<IAttachmentEntry>()).ToArray();

            var size = attachmentEntries.Select(x => x.Data.Length).Sum();
            if (15000000 > (size / 3) * 4)
            {
                foreach (var attach in attachmentEntries)
                {
                    if (attach is AttachmentEntry entry)
                    {
                        processedAttachments.Add(entry.ToDataUri());
                    }
                    else
                    {
                        throw new InvalidOperationException("Непідтримуваний тип вкладення.");
                    }
                }
            }
            else
            {
                foreach (var attach in attachmentEntries)
                {
                    if (attach is AttachmentEntry entry)
                    {
                        entry.SaveToTempFile();
                        if (string.IsNullOrEmpty(entry.FilePath))
                            throw new InvalidOperationException("Помилка при збереженні вкладення.");
                        processedAttachments.Add(entry.FilePath);
                    }
                    else
                    {
                        throw new InvalidOperationException("Непідтримуваний тип вкладення.");
                    }
                }
            }

            // Формуємо DTO для відправки, включаючи параметри цитування (QuoteTextStyle та QuoteAttachment)
            var parameters = new SendMessageFullParameters(
                Account: account,
                Recipients: userRecipients,
                GroupIds: groupRecipients.Count == 0 ? null : groupRecipients,
                NoteToSelf: noteToSelf,
                EndSession: endSession,
                Message: message,
                Attachments: processedAttachments.Count == 0 ? null : processedAttachments,
                Mentions: mentions ?? null,
                TextStyle: parsedTextStyles.Any() ? parsedTextStyles : (externalTextStyles ?? null),
                QuoteTimestamp: quoteTimestamp,
                QuoteAuthor: quoteAuthor,
                QuoteMessage: quoteMessage,
                QuoteMention: quoteMentions ?? null,
                QuoteTextStyle: quoteTextStyles ?? null,
                QuoteAttachment: quoteAttachments ?? null,
                PreviewUrl: previewUrl,
                PreviewTitle: previewTitle,
                PreviewDescription: previewDescription,
                PreviewImage: previewImage,
                Sticker: sticker,
                StoryTimestamp: storyTimestamp,
                StoryAuthor: storyAuthor,
                EditTimestamp: editTimestamp
            );

            try
            {
                var response = await _signalCliClient
                    .InvokeMethodAsync<SendMessageResponse, SendMessageFullParameters>("send", parameters,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (response == null)
                {
                    _logger.LogError("Отримано null-відповідь від сервера при відправці повідомлення");
                    throw new InvalidOperationException("Null response from server");
                }

                _logger.LogInformation("Повідомлення відправлено успішно. TimeStamp={TimeStamp}", response.TimeStamp);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при відправці повідомлення");
                throw;
            }
            finally
            {
                foreach (var attach in attachmentEntries)
                {
                    if (attach is AttachmentEntry entry)
                    {
                        entry.DeleteTempFile();
                    }
                }
            }
        }

        /// <summary>
        /// Обгортка для відправки звичайного текстового повідомлення без вкладень.
        /// Підтримує цитування та прев’ю посилань.
        /// </summary>
        public async Task<List<SendMessageResponse>> SendTextMessageAsync(TextMessageOptions options)
        {
            var response = await SendUnifiedMessageAsync(
                account: options.Account,
                recipients: options.Recipients,
                message: options.Message,
                noteToSelf: false,
                endSession: false,
                // Если режим стилизации включён через UseStyle(), устанавливаем "styled"
                textMode: options.UseStyle ? "styled" : null,
                attachments: null,
                mentions: options.Mentions,
                // Параметры цитирования и прочие специфичные поля отсутствуют для текстового сообщения
                quoteTimestamp: null,
                quoteAuthor: null,
                quoteMessage: null,
                quoteMentions: null,
                quoteTextStyles: null,
                quoteAttachments: null,
                externalTextStyles: null,
                editTimestamp: null,
                sticker: null,
                previewUrl: options.PreviewUrl,
                previewTitle: options.PreviewTitle,
                previewDescription: options.PreviewDescription,
                previewImage: options.PreviewImage,
                storyTimestamp: null,
                storyAuthor: null,
                cancellationToken: options.CancellationToken
            ).ConfigureAwait(false);


            return [response];
        }

        /// <summary>
        /// Обгортка для відправки повідомлення з вкладенням.
        /// Підтримує цитування. Для групових повідомлень допускається лише один отримувач.
        /// </summary>
        public async Task<List<SendMessageResponse>> SendAttachmentAsync(AttachmentMessageOptions options)
        {
            var response = await SendUnifiedMessageAsync(
                account: options.Account,
                recipients: options.Recipients,
                // Сообщение может быть опциональным
                message: options.Message,
                noteToSelf: false,
                endSession: false,
                textMode: options.UseStyle ? "styled" : null,
                attachments: options.Attachments,
                mentions: options.Mentions,
                quoteTimestamp: null,
                quoteAuthor: null,
                quoteMessage: null,
                quoteMentions: null,
                quoteTextStyles: null,
                quoteAttachments: null,
                externalTextStyles: null,
                editTimestamp: null,
                sticker: null,
                previewUrl: null,
                previewTitle: null,
                previewDescription: null,
                previewImage: null,
                storyTimestamp: null,
                storyAuthor: null,
                cancellationToken: options.CancellationToken
            ).ConfigureAwait(false);


            return [response];
        }

        /// <summary>
        /// Обгортка для відправки стікера.
        /// Для групових повідомлень допускається лише один отримувач.
        /// </summary>
        public async Task<List<SendMessageResponse>> SendStickerAsync(StickerMessageOptions options)
        {
            var response = await SendUnifiedMessageAsync(
                account: options.Account,
                recipients: options.Recipients,
                // Для стикера текст оставляем пустым
                message: string.Empty,
                noteToSelf: false,
                endSession: false,
                textMode: null,
                attachments: null,
                mentions: options.Mentions,
                quoteTimestamp: null,
                quoteAuthor: null,
                quoteMessage: null,
                quoteMentions: null,
                quoteTextStyles: null,
                quoteAttachments: null,
                externalTextStyles: null,
                editTimestamp: null,
                // Передаем стикер из опций
                sticker: options.Sticker,
                previewUrl: null,
                previewTitle: null,
                previewDescription: null,
                previewImage: null,
                storyTimestamp: null,
                storyAuthor: null,
                cancellationToken: options.CancellationToken
            ).ConfigureAwait(false);

            return [response];
        }

        /// <summary>
        /// Перевіряє, що список отримувачів не порожній.
        /// </summary>
        private void ValidateRecipients(IEnumerable<IRecipient> recipients, string paramName = "recipients")
        {
            if (recipients == null || !recipients.Any())
            {
                throw new ArgumentException("Отримувач не може бути порожнім", paramName);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
        }
    }
}