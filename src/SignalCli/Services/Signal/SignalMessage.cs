using Microsoft.Extensions.Logging;
using SignalCli.Interfaces.FileSystem;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Logging;
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

        // post-modernize-tuning §8c.6 (audit C7): уніфікований DTO замість 23 окремих
        // параметрів. Public-обгортки нижче (SendTextMessageAsync/SendAttachmentAsync/
        // SendStickerAsync) будують UnifiedSendRequest з типобезпечного *MessageOptions.
        //
        // CA2208 suppressed: цей internal-метод пере-throw'ує ArgumentException у caller-context;
        // paramName має лишатися consumer-visible (`recipients`/`quoteTimestamp`), а не `req`.
        // Analyzer перевіряє локальний scope методу, що тут helpful-friendly false-positive.
#pragma warning disable CA2208
        private async Task<SendMessageResponse> SendUnifiedMessageAsync(
            UnifiedSendRequest req,
            CancellationToken cancellationToken = default)
        {
            // post-modernize-tuning §8c.5 (audit C9): single-pass materialization —
            // якщо викликач передав stateful IEnumerable (зчитує файл, ітерує DB),
            // ми обходимо джерело РІВНО ОДИН раз. Інакше ValidateRecipients + 2× Where
            // = 3 ітерації, що для side-effect'ивних джерел — баг.
            var recipientList = req.Recipients as IReadOnlyList<IRecipient> ?? req.Recipients.ToList();
            ValidateRecipients(recipientList);

            // A.10: switch по enum-у замість порівняння рядків.
            string message = req.Message;
            List<string> parsedTextStyles = [];
            if (req.TextMode == TextStyleMode.Styled)
            {
                var parser = new TextStyleParser(message);
                (message, parsedTextStyles) = parser.Parse();
            }

            // §8c.5: single-pass split на users vs groups; уникаємо двох окремих Where-проходів.
            List<string> userRecipients = [];
            List<string> groupRecipients = [];
            foreach (var r in recipientList)
            {
                (r.IsGroup ? groupRecipients : userRecipients).Add(r.Identifier);
            }
            if (groupRecipients.Count > 1)
            {
                throw new ArgumentException("Для групових повідомлень допускається лише один отримувач",
                    paramName: "recipients");
            }

            // F8: змішування «1 група + N користувачів» раніше проходило цю перевірку
            // (Count>1 && Count>1 — недосяжно для першого > 1), і signal-cli отримував
            // некоректний send. Тепер відкидаємо БУДЬ-ЯКЕ змішування user+group в одному виклику.
            if (groupRecipients.Count > 0 && userRecipients.Count > 0)
            {
                throw new ArgumentException(
                    "Не можна змішувати отримувачів-користувачів і отримувачів-груп у одному повідомленні",
                    paramName: "recipients");
            }

            if ((req.QuoteTimestamp != null || req.QuoteAuthor != null || req.QuoteMessage != null) &&
                (req.QuoteTimestamp == null || req.QuoteAuthor == null || req.QuoteMessage == null))
            {
                // F24: ArgumentException.ParamName має бути одне ім'я параметра — не joined-list.
                // Перелік невказаних полів кладемо в текст повідомлення.
                throw new ArgumentException(
                    "Для цитування повідомлення необхідно вказати всі три параметри: " +
                    $"{nameof(req.QuoteTimestamp)}, {nameof(req.QuoteAuthor)} та {nameof(req.QuoteMessage)}",
                    paramName: "quoteTimestamp"
                );
            }

            // Обробка вкладень: конвертуємо кожне вкладення в рядок Data URI
            List<string> processedAttachments = [];
            var attachmentEntries = req.Attachments as IAttachmentEntry[] ??
                                    (req.Attachments ?? Array.Empty<IAttachmentEntry>()).ToArray();

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
                Account: req.Account,
                Recipients: userRecipients,
                GroupIds: groupRecipients.Count == 0 ? null : groupRecipients,
                NoteToSelf: req.NoteToSelf,
                EndSession: req.EndSession,
                Message: message,
                Attachments: processedAttachments.Count == 0 ? null : processedAttachments,
                Mentions: req.Mentions ?? null,
                TextStyle: parsedTextStyles.Count > 0 ? parsedTextStyles : (req.ExternalTextStyles ?? null),
                QuoteTimestamp: req.QuoteTimestamp,
                QuoteAuthor: req.QuoteAuthor,
                QuoteMessage: req.QuoteMessage,
                QuoteMention: req.QuoteMentions ?? null,
                QuoteTextStyle: req.QuoteTextStyles ?? null,
                QuoteAttachment: req.QuoteAttachments ?? null,
                PreviewUrl: req.PreviewUrl,
                PreviewTitle: req.PreviewTitle,
                PreviewDescription: req.PreviewDescription,
                PreviewImage: req.PreviewImage,
                Sticker: req.Sticker,
                StoryTimestamp: req.StoryTimestamp,
                StoryAuthor: req.StoryAuthor,
                EditTimestamp: req.EditTimestamp
            );

            try
            {
                var response = await _signalCliClient
                    .InvokeMethodAsync<SendMessageResponse, SendMessageFullParameters>("send", parameters,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (response == null)
                {
                    SignalMessageLog.SendNullResponse(_logger);
                    throw new InvalidOperationException("Null response from server");
                }

                SignalMessageLog.SendOk(_logger, response.TimeStamp);
                return response;
            }
            // post-modernize-tuning §8c.7 (audit C8): bare catch-and-rethrow прибрано.
            // ActivitySource у JsonRpcClient (§11.A.2) уже фіксує exception-type-name.
            // finally-блок зберігаємо — він диспозить temp-файли вкладень, які
            // ОБОВ'ЯЗКОВО мають бути прибрані незалежно від результату send'у.
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
#pragma warning restore CA2208

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
            // post-modernize-tuning §8c.6 (audit C7): UnifiedSendRequest DTO замість 23 params.
            var req = new UnifiedSendRequest(
                Account: options.Account,
                Recipients: options.Recipients,
                Message: options.Message,
                NoteToSelf: false,
                EndSession: false,
                // A.10: enum замість stringly-typed "styled".
                TextMode: options.UseStyle ? TextStyleMode.Styled : TextStyleMode.None,
                Attachments: null,
                Mentions: options.Mentions,
                QuoteTimestamp: null,
                QuoteAuthor: null,
                QuoteMessage: null,
                QuoteMentions: null,
                QuoteTextStyles: null,
                QuoteAttachments: null,
                ExternalTextStyles: null,
                EditTimestamp: null,
                Sticker: null,
                PreviewUrl: options.PreviewUrl,
                PreviewTitle: options.PreviewTitle,
                PreviewDescription: options.PreviewDescription,
                PreviewImage: options.PreviewImage,
                StoryTimestamp: null,
                StoryAuthor: null);
            var response = await SendUnifiedMessageAsync(req, linked.Token).ConfigureAwait(false);
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
            var req = new UnifiedSendRequest(
                Account: options.Account,
                Recipients: options.Recipients,
                Message: options.Message, // може бути опціональним для attachments
                NoteToSelf: false,
                EndSession: false,
                TextMode: options.UseStyle ? TextStyleMode.Styled : TextStyleMode.None,
                Attachments: options.Attachments,
                Mentions: options.Mentions,
                QuoteTimestamp: null,
                QuoteAuthor: null,
                QuoteMessage: null,
                QuoteMentions: null,
                QuoteTextStyles: null,
                QuoteAttachments: null,
                ExternalTextStyles: null,
                EditTimestamp: null,
                Sticker: null,
                PreviewUrl: null,
                PreviewTitle: null,
                PreviewDescription: null,
                PreviewImage: null,
                StoryTimestamp: null,
                StoryAuthor: null);
            var response = await SendUnifiedMessageAsync(req, linked.Token).ConfigureAwait(false);
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
            var req = new UnifiedSendRequest(
                Account: options.Account,
                Recipients: options.Recipients,
                Message: string.Empty, // стікер — без тексту
                NoteToSelf: false,
                EndSession: false,
                TextMode: TextStyleMode.None,
                Attachments: null,
                Mentions: options.Mentions,
                QuoteTimestamp: null,
                QuoteAuthor: null,
                QuoteMessage: null,
                QuoteMentions: null,
                QuoteTextStyles: null,
                QuoteAttachments: null,
                ExternalTextStyles: null,
                EditTimestamp: null,
                Sticker: options.Sticker,
                PreviewUrl: null,
                PreviewTitle: null,
                PreviewDescription: null,
                PreviewImage: null,
                StoryTimestamp: null,
                StoryAuthor: null);
            var response = await SendUnifiedMessageAsync(req, linked.Token).ConfigureAwait(false);
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