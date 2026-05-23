using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.FileSystem;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.Signal.Message;
using SignalCli.Services.FileSystem;
using SignalCli.Utilities;

namespace SignalCli.Services.Signal
{
    // A.13: IDisposable прибрано — клас не тримає жодних ресурсів (тимчасові файли вкладень
    // диспозяться у finally внутрі SendUnifiedMessageAsync).
    internal sealed class SignalMessage(ISignalCliClient signalCliClient, ILogger<SignalMessage> logger)
        : ISignalMessage
    {
        private readonly ISignalCliClient _signalCliClient =
            signalCliClient ?? throw new ArgumentNullException(nameof(signalCliClient));

        private readonly ILogger<SignalMessage> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // signal-cli приймає вкладення двома способами:
        //   1) data-URI з base64 — вбудовується прямо в JSON-RPC запит (не потребує
        //      спільної файлової системи);
        //   2) шлях до файлу — демон читає файл із диска (JSON лишається малим).
        //
        // ЧОМУ ВИБІР: signal-cli парсить вхідний JSON через Jackson, у якого
        // StreamReadConstraints.maxStringLength за замовчуванням = 20 000 000 символів.
        // base64 роздуває дані на 4/3 (X байт -> (X/3)*4 символів), тож великий інлайн
        // перевищить цей ліміт і запит впаде (StreamConstraintsException).
        // Поріг 15 000 000 закодованих символів тримає інлайн-варіант нижче 20M
        // із запасом на решту полів JSON; вкладення більші за поріг ідуть через temp-файл.
        // (Клієнт додатково перевіряє довжину всього рядка запиту проти 20 000 000.)
        private const long MaxInlineEncodedAttachmentBytes = 15_000_000;

        private async Task<SendMessageResponse> SendUnifiedMessageAsync(
            string account,
            IEnumerable<IRecipient> recipients,
            string message,
            bool noteToSelf,
            bool endSession,
            // A.10: типобезпечний режим стилізації замість stringly-typed "styled"-прапорця.
            TextStyleMode textMode,
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

            // A.10: switch по enum-у замість порівняння рядків.
            List<string> parsedTextStyles = [];
            if (textMode == TextStyleMode.Styled)
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

            // F8: змішування «1 група + N користувачів» раніше проходило цю перевірку
            // (Count>1 && Count>1 — недосяжно для першого > 1), і signal-cli отримував
            // некоректний send. Тепер відкидаємо БУДЬ-ЯКЕ змішування user+group в одному виклику.
            if (groupRecipients.Count > 0 && userRecipients.Count > 0)
            {
                throw new ArgumentException(
                    "Не можна змішувати отримувачів-користувачів і отримувачів-груп у одному повідомленні",
                    nameof(recipients));
            }

            if ((quoteTimestamp != null || quoteAuthor != null || quoteMessage != null) &&
                (quoteTimestamp == null || quoteAuthor == null || quoteMessage == null))
            {
                // F24: ArgumentException.ParamName має бути одне ім'я параметра — не joined-list.
                // Перелік невказаних полів кладемо в текст повідомлення.
                throw new ArgumentException(
                    "Для цитування повідомлення необхідно вказати всі три параметри: " +
                    $"{nameof(quoteTimestamp)}, {nameof(quoteAuthor)} та {nameof(quoteMessage)}",
                    nameof(quoteTimestamp)
                );
            }

            // Обробка вкладень: конвертуємо кожне вкладення в рядок Data URI
            List<string> processedAttachments = [];
            var attachmentEntries = attachments as IAttachmentEntry[] ??
                                    (attachments ?? Array.Empty<IAttachmentEntry>()).ToArray();

            // Сумарний розмір після base64 (4/3 від сирого). Порівнюємо із порогом, нижчим
            // за ліміт Jackson (див. MaxInlineEncodedAttachmentBytes), щоб вирішити спосіб передачі.
            var rawSize = attachmentEntries.Sum(x => (long)x.Data.Length);
            var encodedSize = (rawSize / 3) * 4;
            if (encodedSize < MaxInlineEncodedAttachmentBytes)
            {
                // Малий обсяг -> інлайн data-URI (JSON лишається в межах ліміту Jackson).
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
                // Великий обсяг -> temp-файли + шляхи, щоб JSON-рядок не перевищив ліміт Jackson.
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
                TextStyle: parsedTextStyles.Count > 0 ? parsedTextStyles : (externalTextStyles ?? null),
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
        public async Task<List<SendMessageResponse>> SendTextMessageAsync(
            TextMessageOptions options,
            CancellationToken cancellationToken = default)
        {
            // F12: явний guard замість NRE при доступі до options.Account.
            ArgumentNullException.ThrowIfNull(options);
            // A.3/A.4: лінкуємо токен-параметр з options.CancellationToken (deprecated),
            // щоб обидва шляхи скасування продовжували працювати під час shim-вікна.
#pragma warning disable CS0618 // A.5: shim-вікно — читаємо deprecated CancellationToken із options
            using var linked = LinkTokens(cancellationToken, options.CancellationToken);
#pragma warning restore CS0618
            var response = await SendUnifiedMessageAsync(
                account: options.Account,
                recipients: options.Recipients,
                message: options.Message,
                noteToSelf: false,
                endSession: false,
                // A.10: enum замість stringly-typed "styled".
                textMode: options.UseStyle ? TextStyleMode.Styled : TextStyleMode.None,
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
                cancellationToken: linked.Token
            ).ConfigureAwait(false);


            return [response];
        }

        /// <summary>
        /// Обгортка для відправки повідомлення з вкладенням.
        /// Підтримує цитування. Для групових повідомлень допускається лише один отримувач.
        /// </summary>
        public async Task<List<SendMessageResponse>> SendAttachmentAsync(
            AttachmentMessageOptions options,
            CancellationToken cancellationToken = default)
        {
            // F12: явний guard замість NRE при доступі до options.Account.
            ArgumentNullException.ThrowIfNull(options);
#pragma warning disable CS0618 // A.5: shim-вікно — читаємо deprecated CancellationToken із options
            using var linked = LinkTokens(cancellationToken, options.CancellationToken);
#pragma warning restore CS0618
            var response = await SendUnifiedMessageAsync(
                account: options.Account,
                recipients: options.Recipients,
                // Сообщение может быть опциональным
                message: options.Message,
                noteToSelf: false,
                endSession: false,
                textMode: options.UseStyle ? TextStyleMode.Styled : TextStyleMode.None,
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
                cancellationToken: linked.Token
            ).ConfigureAwait(false);


            return [response];
        }

        /// <summary>
        /// Обгортка для відправки стікера.
        /// Для групових повідомлень допускається лише один отримувач.
        /// </summary>
        public async Task<List<SendMessageResponse>> SendStickerAsync(
            StickerMessageOptions options,
            CancellationToken cancellationToken = default)
        {
            // F12: явний guard замість NRE при доступі до options.Account.
            ArgumentNullException.ThrowIfNull(options);
#pragma warning disable CS0618 // A.5: shim-вікно — читаємо deprecated CancellationToken із options
            using var linked = LinkTokens(cancellationToken, options.CancellationToken);
#pragma warning restore CS0618
            var response = await SendUnifiedMessageAsync(
                account: options.Account,
                recipients: options.Recipients,
                // Для стикера текст оставляем пустым
                message: string.Empty,
                noteToSelf: false,
                endSession: false,
                textMode: TextStyleMode.None,
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
                cancellationToken: linked.Token
            ).ConfigureAwait(false);

            return [response];
        }

        /// <summary>
        /// A.3: безпечно лінкує два токени: значення з аргументу метода та deprecated-поле в Options.
        /// Якщо обидва None — повертає CTS без активної реєстрації (нульовий runtime-cost).
        /// </summary>
        private static CancellationTokenSource LinkTokens(CancellationToken a, CancellationToken b)
        {
            // CreateLinkedTokenSource приймає 0..N токенів; передаємо обидва.
            // Якщо обидва — CancellationToken.None, повернений CTS просто ніколи не скасується.
            return CancellationTokenSource.CreateLinkedTokenSource(a, b);
        }

        /// <summary>
        /// Перевіряє, що список отримувачів не порожній.
        /// </summary>
        /// <remarks>
        /// A.12: <c>paramName</c> автоматично береться з виразу-аргументу через
        /// <see cref="System.Runtime.CompilerServices.CallerArgumentExpressionAttribute"/>;
        /// викликачу не треба явно передавати ім’я (рефакторинг безпечний).
        /// </remarks>
        private static void ValidateRecipients(
            IEnumerable<IRecipient> recipients,
            [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(recipients))]
            string? paramName = null)
        {
            if (recipients == null || !recipients.Any())
            {
                throw new ArgumentException("Отримувач не може бути порожнім", paramName);
            }
        }

    }
}