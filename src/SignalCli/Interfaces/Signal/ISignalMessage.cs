using SignalCli.Exceptions;
using SignalCli.Interfaces.FileSystem;
using SignalCli.Models.Signal.Message;

namespace SignalCli.Interfaces.Signal
{
    /// <summary>
    /// Сервіс для відправки повідомлень Signal.
    /// </summary>
    /// <remarks>
    /// Надає методи для відправки різних типів повідомлень: текстових, 
    /// з вкладеннями, стікерів, з можливістю стилізації, цитування тощо.
    /// </remarks>
    public interface ISignalMessage
    {
        /// <summary>
        /// Відправляє текстове повідомлення.
        /// </summary>
        /// <param name="options">
        /// Об'єкт параметрів відправки текстового повідомлення, представлений класом TextMessageOptions.
        /// Обов’язкові параметри, які потрібно передати через конструктор билдера: 
        /// Account, Recipients і Message.
        /// </param>
        /// <returns>Результати відправки повідомлень.</returns>
        /// <exception cref="ArgumentNullException">
        /// Виникає, якщо один із обов’язкових параметрів не заданий.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Виникає, якщо передані параметри некоректні (наприклад, список отримувачів порожній).
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Виникає при помилці відправки повідомлення.
        /// </exception>
        /// <exception cref="JsonRpcException">
        /// Виникає при помилці JSON-RPC запиту.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Виникає, якщо операцію скасовано.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Виникає, якщо signal-cli не відповів за <see cref="Models.SignalCliOptions.RequestTimeoutSeconds"/>.
        /// </exception>
        /// <example>
        /// <code>
        /// var textOptions = new TextMessageOptions.Builder("+380501234567",
        ///                          [new UserRecipient("+380501234567")],
        ///                          "Привіт, світ!")
        ///                          .UseStyle()
        ///                          .Build();
        /// var response = await signalMessage.SendTextMessageAsync(textOptions);
        /// </code>
        /// </example>
        /// <param name="cancellationToken">Токен скасування операції.</param>
        public Task<SendMessageResponse> SendTextMessageAsync(TextMessageOptions options, CancellationToken cancellationToken = default);

        /// <summary>
        /// Відправляє повідомлення з вкладеннями.
        /// </summary>
        /// <param name="options">
        /// Об'єкт параметрів відправки повідомлення з вкладеннями, представлений класом AttachmentMessageOptions.
        /// Обов’язкові параметри, які потрібно передати через конструктор билдера: 
        /// Account, Recipients та Attachments.
        /// Повідомлення (Message) є опціональним.
        /// </param>
        /// <returns>Результати відправки повідомлень.</returns>
        /// <exception cref="ArgumentNullException">
        /// Виникає, якщо один із обов’язкових параметрів не заданий.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Виникає, якщо передані параметри некоректні (наприклад, список отримувачів порожній або для групових повідомлень вказано більше одного отримувача).
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Виникає при помилці обробки вкладень або відправки повідомлення.
        /// </exception>
        /// <exception cref="JsonRpcException">
        /// Виникає при помилці JSON-RPC запиту.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Виникає, якщо операцію скасовано.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Виникає, якщо signal-cli не відповів за <see cref="Models.SignalCliOptions.RequestTimeoutSeconds"/>.
        /// </exception>
        /// <example>
        /// <code>
        /// var attachmentOptions = new AttachmentMessageOptions.Builder("+380501234567",
        ///                                [new UserRecipient("+380501234567")],
        ///                                attachmentsList)
        ///                                .WithMessage("Привіт, світ з вкладенням!")
        ///                                .Build();
        /// var response = await signalMessage.SendAttachmentAsync(attachmentOptions);
        /// </code>
        /// </example>
        /// <param name="cancellationToken">Токен скасування операції.</param>
        public Task<SendMessageResponse> SendAttachmentAsync(AttachmentMessageOptions options, CancellationToken cancellationToken = default);

        /// <summary>
        /// Відправляє стікер.
        /// </summary>
        /// <param name="options">
        /// Об'єкт параметрів відправки стікера, представлений класом StickerMessageOptions.
        /// Обов’язкові параметри, які потрібно передати через конструктор билдера: 
        /// Account, Recipients і Sticker.
        /// </param>
        /// <returns>Результати відправки повідомлень.</returns>
        /// <exception cref="ArgumentNullException">
        /// Виникає, якщо один із обов’язкових параметрів не заданий.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Виникає, якщо передані параметри некоректні (наприклад, список отримувачів порожній або для групових повідомлень вказано більше одного отримувача).
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Виникає при помилці відправки стікера.
        /// </exception>
        /// <exception cref="JsonRpcException">
        /// Виникає при помилці JSON-RPC запиту.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Виникає, якщо операцію скасовано.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Виникає, якщо signal-cli не відповів за <see cref="Models.SignalCliOptions.RequestTimeoutSeconds"/>.
        /// </exception>
        /// <example>
        /// <code>
        /// var stickerOptions = new StickerMessageOptions.Builder("+380501234567",
        ///                          [new UserRecipient("+380501234567")],
        ///                          "stickerPackId:stickerId")
        ///                          .Build();
        /// var response = await signalMessage.SendStickerAsync(stickerOptions);
        /// </code>
        /// </example>
        /// <param name="cancellationToken">Токен скасування операції.</param>
        public Task<SendMessageResponse> SendStickerAsync(StickerMessageOptions options, CancellationToken cancellationToken = default);

        /// <summary>
        /// Відправляє emoji-реакцію на існуюче повідомлення Signal (signal-cli-api-coverage Wave 1).
        /// </summary>
        /// <remarks>
        /// signal-cli RPC mapping: see
        /// <c>src/main/java/org/asamk/signal/commands/SendReactionCommand.java</c> @ <c>bda4e7fc</c>.
        /// Field names mirror Jackson record exactly.
        /// <para>
        /// <b>§F20 quirk:</b> серед 4-х Wave-1 send-методів тільки sendReaction має
        /// <see cref="ReactionOptions.NotifySelf"/> flag і catch'ить <c>UnregisteredRecipientException</c>
        /// на top-level. Для решти 3-х (Receipt/Typing/RemoteDelete) unregistered-recipients
        /// surface'ять як per-recipient <c>UNREGISTERED_FAILURE</c> у <see cref="SendMessageResponse.Results"/>.
        /// </para>
        /// <para>
        /// <b>§F10 IOException divergence:</b> <c>IOException</c> у upstream'і мапиться на код
        /// <c>-32603 INTERNAL_ERROR</c> (через <c>UnexpectedErrorException</c>), не на <c>-3 IoError</c>.
        /// </para>
        /// </remarks>
        /// <param name="options">
        /// Опції reaction'а (acc, recipients/groups/usernames/noteToSelf, emoji, targetAuthor,
        /// targetTimestamp, remove, story, notifySelf).
        /// </param>
        /// <returns>Результат відправки реакції (timestamp + per-recipient статуси).</returns>
        /// <exception cref="ArgumentNullException">Якщо <paramref name="options"/> = null.</exception>
        /// <exception cref="JsonRpcException">При помилці JSON-RPC.</exception>
        /// <exception cref="UntrustedIdentityException">Код <c>-4</c> — identity всіх recipient'ів неверифікована.</exception>
        /// <exception cref="IdentityChangedException">
        /// Підтип <see cref="UntrustedIdentityException"/>: identity ВЖЕ ВІДОМОГО контакту змінилася (re-install).
        /// </exception>
        /// <exception cref="RateLimitException">Код <c>-5</c> — server-rate-limit для усіх recipient'ів.</exception>
        /// <exception cref="CaptchaRequiredException">Код <c>-6</c> — CAPTCHA challenge requested.</exception>
        /// <exception cref="OperationCanceledException">При скасуванні операції.</exception>
        /// <exception cref="TimeoutException">При відсутності відповіді signal-cli у межах <see cref="Models.SignalCliOptions.RequestTimeoutSeconds"/>.</exception>
        /// <param name="cancellationToken">Токен скасування.</param>
        public Task<SendMessageResponse> SendReactionAsync(ReactionOptions options, CancellationToken cancellationToken = default);

        /// <summary>
        /// Відправляє read/viewed receipt-acknowledgement автору повідомлення (signal-cli-api-coverage Wave 1).
        /// </summary>
        /// <remarks>
        /// signal-cli RPC mapping: see
        /// <c>src/main/java/org/asamk/signal/commands/SendReceiptCommand.java</c> @ <c>bda4e7fc</c>.
        /// Field names mirror Jackson record exactly.
        /// <para>
        /// <b>§F7 quirk:</b> <see cref="ReceiptOptions.Recipient"/> — singular string, не список.
        /// Унікально серед Wave-1 send-методів. Wire-shape: <c>"recipient": "+1..."</c>, не <c>[...]</c>.
        /// </para>
        /// <para>
        /// <b>§F10 IOException:</b> у <c>sendReceipt</c> взагалі немає <c>IOException</c>-catch-path
        /// — <c>sendReadReceipt</c>/<c>sendViewedReceipt</c> не declare'ять <c>throws IOException</c>.
        /// Receipts є best-effort fire-and-log.
        /// </para>
        /// <para>
        /// <b>Side-effect.</b> НЕ марк'ить повідомлення як прочитане локально (signal-cli не має
        /// local read-state) — це notification до <i>remote sender'а</i>.
        /// </para>
        /// </remarks>
        /// <param name="options">Опції receipt'у (acc, singular recipient, timestamps, type).</param>
        /// <returns>Результат відправки.</returns>
        /// <exception cref="ArgumentNullException">Якщо <paramref name="options"/> = null.</exception>
        /// <exception cref="JsonRpcException">При помилці JSON-RPC.</exception>
        /// <exception cref="UntrustedIdentityException">Код <c>-4</c>.</exception>
        /// <exception cref="RateLimitException">Код <c>-5</c>.</exception>
        /// <exception cref="CaptchaRequiredException">Код <c>-6</c>.</exception>
        /// <exception cref="OperationCanceledException">При скасуванні.</exception>
        /// <exception cref="TimeoutException">При тайм-ауті.</exception>
        /// <param name="cancellationToken">Токен скасування.</param>
        public Task<SendMessageResponse> SendReceiptAsync(ReceiptOptions options, CancellationToken cancellationToken = default);

        /// <summary>
        /// Відправляє typing-indicator (START або STOP) (signal-cli-api-coverage Wave 1).
        /// </summary>
        /// <remarks>
        /// signal-cli RPC mapping: see
        /// <c>src/main/java/org/asamk/signal/commands/SendTypingCommand.java</c> @ <c>bda4e7fc</c>.
        /// Field names mirror Jackson record exactly.
        /// <para>
        /// <b>§F10 IOException divergence:</b> у <c>sendTyping</c> <c>IOException</c> мапиться на
        /// <c>-1 UserError</c> (через <c>UserErrorException</c>), <i>не</i> на <c>-32603</c> як у
        /// sendReaction/remoteDelete. Inconsistent upstream-choice, не нормалізуємо.
        /// </para>
        /// <para>
        /// <b>Note.</b> START auto-expires через ~15 секунд на стороні receiver'а; explicit STOP опційний.
        /// </para>
        /// </remarks>
        /// <param name="options">Опції typing'у (acc, recipients/groups, stop).</param>
        /// <returns>Результат відправки.</returns>
        /// <exception cref="ArgumentNullException">Якщо <paramref name="options"/> = null.</exception>
        /// <exception cref="JsonRpcException">При помилці JSON-RPC (включно з IOException → -1).</exception>
        /// <exception cref="GroupAdminRequiredException">Якщо група вимагає admin'а для typing-indicator'ів.</exception>
        /// <exception cref="UntrustedIdentityException">Код <c>-4</c>.</exception>
        /// <exception cref="RateLimitException">Код <c>-5</c>.</exception>
        /// <exception cref="OperationCanceledException">При скасуванні.</exception>
        /// <exception cref="TimeoutException">При тайм-ауті.</exception>
        /// <param name="cancellationToken">Токен скасування.</param>
        public Task<SendMessageResponse> SendTypingAsync(TypingOptions options, CancellationToken cancellationToken = default);

        /// <summary>
        /// Відправляє remote-delete-запит — попросити recipient'ів видалити власну копію повідомлення
        /// (signal-cli-api-coverage Wave 1).
        /// </summary>
        /// <remarks>
        /// signal-cli RPC mapping: see
        /// <c>src/main/java/org/asamk/signal/commands/RemoteDeleteCommand.java</c> @ <c>bda4e7fc</c>.
        /// Field names mirror Jackson record exactly.
        /// <para>
        /// <b>§F10 IOException:</b> мапиться на <c>-32603 INTERNAL_ERROR</c>, консистентно з sendReaction.
        /// </para>
        /// <para>
        /// <b>Best-effort.</b> Receivers' Signal clients можуть відмовити (поза часовим вікном). Локальна
        /// копія повідомлення у signal-cli НЕ видаляється — consumers повинні трекати sent-messages самі.
        /// </para>
        /// </remarks>
        /// <param name="options">Опції delete'у (acc, targetTimestamp, recipients/groups/usernames/noteToSelf).</param>
        /// <returns>Результат відправки.</returns>
        /// <exception cref="ArgumentNullException">Якщо <paramref name="options"/> = null.</exception>
        /// <exception cref="JsonRpcException">При помилці JSON-RPC.</exception>
        /// <exception cref="UntrustedIdentityException">Код <c>-4</c>.</exception>
        /// <exception cref="RateLimitException">Код <c>-5</c>.</exception>
        /// <exception cref="OperationCanceledException">При скасуванні.</exception>
        /// <exception cref="TimeoutException">При тайм-ауті.</exception>
        /// <param name="cancellationToken">Токен скасування.</param>
        public Task<SendMessageResponse> SendRemoteDeleteAsync(RemoteDeleteOptions options, CancellationToken cancellationToken = default);

        // ===== signal-cli-api-coverage Wave 7 (`polls` + `messaging-power-user`) =====

        /// <summary>Створює новий poll. §F15 client-side validation (2-10 options, ≤100 chars кожен). §F21 polarity-inverted на wire.</summary>
        /// <remarks>signal-cli RPC mapping: see <c>SendPollCreateCommand.java</c> @ <c>bda4e7fc</c>.</remarks>
        Task<SendMessageResponse> SendPollCreateAsync(SendPollCreateOptions options, CancellationToken cancellationToken = default);

        /// <summary>Voteєш у polls. §F22 zero-based indexes. VoteCount monotonic per-voter.</summary>
        /// <remarks>signal-cli RPC mapping: see <c>SendPollVoteCommand.java</c> @ <c>bda4e7fc</c>.</remarks>
        Task<SendMessageResponse> SendPollVoteAsync(SendPollVoteOptions options, CancellationToken cancellationToken = default);

        /// <summary>Завершує poll. No poll-author — terminator MUST be original author.</summary>
        /// <remarks>signal-cli RPC mapping: see <c>SendPollTerminateCommand.java</c> @ <c>bda4e7fc</c>.</remarks>
        Task<SendMessageResponse> SendPollTerminateAsync(SendPollTerminateOptions options, CancellationToken cancellationToken = default);

        /// <summary>Admin-delete повідомлення у групі. <b>Group-only</b>.</summary>
        /// <remarks>signal-cli RPC mapping: see <c>SendAdminDeleteCommand.java</c> @ <c>bda4e7fc</c>. Server-side rejects if не admin.</remarks>
        Task<SendMessageResponse> SendAdminDeleteAsync(SendAdminDeleteOptions options, CancellationToken cancellationToken = default);

        /// <summary>Pin повідомлення. §F23 PinDurationSeconds=-1 sentinel = forever.</summary>
        /// <remarks>signal-cli RPC mapping: see <c>SendPinMessageCommand.java</c> @ <c>bda4e7fc</c>.</remarks>
        Task<SendMessageResponse> SendPinMessageAsync(SendPinMessageOptions options, CancellationToken cancellationToken = default);

        /// <summary>Unpin повідомлення. Symmetric до Pin без duration.</summary>
        /// <remarks>signal-cli RPC mapping: see <c>SendUnpinMessageCommand.java</c> @ <c>bda4e7fc</c>.</remarks>
        Task<SendMessageResponse> SendUnpinMessageAsync(SendUnpinMessageOptions options, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends sync message що ділиться з linked devices acceptance/deletion стану message-request'у.
        /// §F2: ONLY Accept/Delete supported send-side. Block-стайл — через Wave-3 Block/Unblock.
        /// </summary>
        /// <remarks>signal-cli RPC mapping: see <c>SendMessageRequestResponseCommand.java</c> @ <c>bda4e7fc</c>. Wire response empty.</remarks>
        Task SendMessageRequestResponseAsync(
            string account,
            MessageRequestResponseType type,
            IEnumerable<string>? recipients = null,
            IEnumerable<string>? groupIds = null,
            IEnumerable<string>? usernames = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends payment notification. <b>Single recipient only</b>; <paramref name="receiptBase64"/> — base64 MobileCoin receipt blob.
        /// </summary>
        /// <remarks>signal-cli RPC mapping: see <c>SendPaymentNotificationCommand.java</c> @ <c>bda4e7fc</c>.</remarks>
        Task<SendMessageResponse> SendPaymentNotificationAsync(
            string account,
            string recipient,
            string receiptBase64,
            string? note = null,
            CancellationToken cancellationToken = default);
    }
}
