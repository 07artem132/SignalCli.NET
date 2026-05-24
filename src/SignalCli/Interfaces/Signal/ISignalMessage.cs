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
    }
}
