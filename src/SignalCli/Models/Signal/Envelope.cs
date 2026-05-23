using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal;

/// <summary>
/// Конверт повідомлення JSON, який містить усі можливі типи повідомлень Signal.
/// Основний контейнер для даних, які приходять від Signal CLI.
/// </summary>
/// <param name="Source">Ідентифікатор джерела повідомлення.</param>
/// <param name="SourceNumber">Номер телефону джерела повідомлення.</param>
/// <param name="SourceUuid">UUID джерела повідомлення.</param>
/// <param name="SourceName">Ім'я джерела повідомлення.</param>
/// <param name="SourceDevice">Ідентифікатор пристрою джерела повідомлення.</param>
/// <param name="Timestamp">Часова мітка повідомлення.</param>
/// <param name="ServerReceivedTimestamp">Часова мітка отримання повідомлення сервером.</param>
/// <param name="ServerDeliveredTimestamp">Часова мітка доставки повідомлення сервером.</param>
/// <param name="DataMessage">Повідомлення з даними (текст, вкладення, тощо).</param>
/// <param name="EditMessage">Повідомлення редагування.</param>
/// <param name="StoryMessage">Повідомлення історії.</param>
/// <param name="SyncMessage">Повідомлення синхронізації.</param>
/// <param name="CallMessage">Повідомлення виклику.</param>
/// <param name="ReceiptMessage">Повідомлення квитанції (отримано, прочитано, переглянуто).</param>
/// <param name="TypingMessage">Повідомлення про набір тексту.</param>
[PublicAPI]
public record JsonMessageEnvelope(
    [property: JsonPropertyName("source")] string? Source,
    [property: JsonPropertyName("sourceNumber")] string? SourceNumber,
    [property: JsonPropertyName("sourceUuid")] string? SourceUuid,
    [property: JsonPropertyName("sourceName")] string? SourceName,
    [property: JsonPropertyName("sourceDevice")] int? SourceDevice,
    [property: JsonPropertyName("timestamp")] long Timestamp,
    [property: JsonPropertyName("serverReceivedTimestamp")] long ServerReceivedTimestamp,
    [property: JsonPropertyName("serverDeliveredTimestamp")] long ServerDeliveredTimestamp,
    [property: JsonPropertyName("dataMessage")] JsonDataMessage? DataMessage,
    [property: JsonPropertyName("editMessage")] JsonEditMessage? EditMessage,
    [property: JsonPropertyName("storyMessage")] JsonStoryMessage? StoryMessage,
    [property: JsonPropertyName("syncMessage")] JsonSyncMessage? SyncMessage,
    [property: JsonPropertyName("callMessage")] JsonCallMessage? CallMessage,
    [property: JsonPropertyName("receiptMessage")] JsonReceiptMessage? ReceiptMessage,
    [property: JsonPropertyName("typingMessage")] JsonTypingMessage? TypingMessage
);

#region DataMessage та вкладені типи

/// <summary>
/// Повідомлення з даними, основне повідомлення Signal.
/// Містить текст, вкладення, стікери, реакції тощо.
/// </summary>
/// <param name="Timestamp">Часова мітка повідомлення.</param>
/// <param name="Message">Текст повідомлення.</param>
/// <param name="ExpiresInSeconds">Час у секундах, після якого повідомлення самознищується.</param>
/// <param name="ViewOnce">Якщо true, повідомлення можна переглянути лише один раз.</param>
/// <param name="Reaction">Реакція на інше повідомлення.</param>
/// <param name="Quote">Цитування іншого повідомлення.</param>
/// <param name="Payment">Дані про платіж.</param>
/// <param name="Mentions">Згадки користувачів у повідомленні.</param>
/// <param name="Previews">Попередній перегляд посилань у повідомленні.</param>
/// <param name="Attachments">Вкладення до повідомлення.</param>
/// <param name="Sticker">Стікер у повідомленні.</param>
/// <param name="RemoteDelete">Інформація про віддалене видалення повідомлення.</param>
/// <param name="Contacts">Контакти, якими поділилися у повідомленні.</param>
/// <param name="TextStyles">Стилі форматування тексту.</param>
/// <param name="GroupInfo">Інформація про групу, до якої належить повідомлення.</param>
/// <param name="StoryContext">Контекст історії для повідомлення.</param>
[PublicAPI]
public record JsonDataMessage(
    [property: JsonPropertyName("timestamp")] ulong Timestamp,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("expiresInSeconds")] int? ExpiresInSeconds,
    [property: JsonPropertyName("viewOnce")] bool? ViewOnce,
    [property: JsonPropertyName("reaction")] JsonReaction? Reaction,
    [property: JsonPropertyName("quote")] JsonQuote? Quote,
    [property: JsonPropertyName("payment")] JsonPayment? Payment,
    [property: JsonPropertyName("mentions")] List<JsonMention>? Mentions,
    [property: JsonPropertyName("previews")] List<JsonPreview>? Previews,
    [property: JsonPropertyName("attachments")] List<JsonAttachment>? Attachments,
    [property: JsonPropertyName("sticker")] JsonSticker? Sticker,
    [property: JsonPropertyName("remoteDelete")] JsonRemoteDelete? RemoteDelete,
    [property: JsonPropertyName("contacts")] List<JsonSharedContact>? Contacts,
    [property: JsonPropertyName("textStyles")] List<JsonTextStyle>? TextStyles,
    [property: JsonPropertyName("groupInfo")] JsonGroupInfo? GroupInfo,
    [property: JsonPropertyName("storyContext")] JsonStoryContext? StoryContext
);

/// <summary>
/// Реакція на повідомлення у вигляді емодзі.
/// </summary>
/// <param name="Emoji">Емодзі реакції.</param>
/// <param name="TargetAuthor">Автор цільового повідомлення.</param>
/// <param name="TargetAuthorNumber">Номер телефону автора цільового повідомлення.</param>
/// <param name="TargetAuthorUuid">UUID автора цільового повідомлення.</param>
/// <param name="TargetSentTimestamp">Часова мітка відправлення цільового повідомлення.</param>
/// <param name="IsRemove">Якщо true, реакція видаляється.</param>
[PublicAPI]
public record JsonReaction(
    [property: JsonPropertyName("emoji")] string? Emoji,
    [property: JsonPropertyName("targetAuthor")] string? TargetAuthor,
    [property: JsonPropertyName("targetAuthorNumber")] string? TargetAuthorNumber,
    [property: JsonPropertyName("targetAuthorUuid")] string? TargetAuthorUuid,
    [property: JsonPropertyName("targetSentTimestamp")] long TargetSentTimestamp,
    [property: JsonPropertyName("isRemove")] bool IsRemove
);

/// <summary>
/// Цитата іншого повідомлення.
/// </summary>
/// <param name="Id">Ідентифікатор цитованого повідомлення.</param>
/// <param name="Author">Автор цитованого повідомлення.</param>
/// <param name="AuthorNumber">Номер телефону автора цитованого повідомлення.</param>
/// <param name="AuthorUuid">UUID автора цитованого повідомлення.</param>
/// <param name="Text">Текст цитованого повідомлення.</param>
/// <param name="Attachments">Вкладення у цитованому повідомленні.</param>
[PublicAPI]
public record JsonQuote(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("author")] string? Author,
    [property: JsonPropertyName("authorNumber")] string? AuthorNumber,
    [property: JsonPropertyName("authorUuid")] string? AuthorUuid,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("attachments")] List<JsonAttachment>? Attachments
);

/// <summary>
/// Інформація про платіж.
/// </summary>
/// <param name="Amount">Сума платежу.</param>
/// <param name="Currency">Валюта платежу.</param>
[PublicAPI]
public record JsonPayment(
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("currency")] string? Currency
);

/// <summary>
/// Згадка користувача у повідомленні.
/// </summary>
/// <param name="Id">Ідентифікатор згаданого користувача.</param>
[PublicAPI]
public record JsonMention(
    [property: JsonPropertyName("id")] string? Id
);

/// <summary>
/// Попередній перегляд посилання у повідомленні.
/// </summary>
/// <param name="Url">URL посилання.</param>
/// <param name="Title">Заголовок попереднього перегляду.</param>
/// <param name="Description">Опис попереднього перегляду.</param>
[PublicAPI]
public record JsonPreview(
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description
);

/// <summary>
/// Вкладення до повідомлення.
/// </summary>
/// <param name="ContentType">MIME-тип вкладення.</param>
/// <param name="Filename">Ім'я файлу вкладення.</param>
/// <param name="Id">Ідентифікатор вкладення.</param>
/// <param name="Size">Розмір вкладення в байтах.</param>
/// <param name="Width">Ширина зображення, якщо вкладення є зображенням.</param>
/// <param name="Height">Висота зображення, якщо вкладення є зображенням.</param>
/// <param name="Caption">Підпис до вкладення.</param>
/// <param name="UploadTimestamp">Часова мітка завантаження вкладення.</param>
[PublicAPI]
public record JsonAttachment(
    [property: JsonPropertyName("contentType")] string? ContentType,
    [property: JsonPropertyName("filename")] string? Filename,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("width")] int? Width,
    [property: JsonPropertyName("height")] int? Height,
    [property: JsonPropertyName("caption")] string? Caption,
    [property: JsonPropertyName("uploadTimestamp")] long? UploadTimestamp
);

/// <summary>
/// Стікер у повідомленні.
/// </summary>
/// <param name="PackId">Ідентифікатор пакету стікерів.</param>
/// <param name="StickerId">Ідентифікатор стікера в пакеті.</param>
[PublicAPI]
public record JsonSticker(
    [property: JsonPropertyName("packId")] string? PackId,
    [property: JsonPropertyName("stickerId")] int? StickerId
);

/// <summary>
/// Інформація про віддалене видалення повідомлення.
/// </summary>
/// <param name="RemoteDeleteId">Ідентифікатор повідомлення для видалення. Завжди присутній — signal-cli не емітить RemoteDelete без targetSentTimestamp.</param>
/// <remarks>
/// post-modernize-tuning §4.17 (audit N3): <c>[JsonRequired]</c> — десеріалізатор кидає
/// <see cref="System.Text.Json.JsonException"/>, якщо поле відсутнє у wire-JSON; раніше
/// non-nullable string мовчки приймав null, що далі вибухало NRE у downstream.
/// </remarks>
[PublicAPI]
public record JsonRemoteDelete(
    [property: JsonPropertyName("remoteDeleteId"), JsonRequired] string RemoteDeleteId
);

/// <summary>
/// Контакт, яким поділилися у повідомленні.
/// </summary>
/// <param name="Name">Ім'я контакту.</param>
/// <param name="PhoneNumber">Номер телефону контакту.</param>
[PublicAPI]
public record JsonSharedContact(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("phoneNumber")] string? PhoneNumber
);

/// <summary>
/// Стиль форматування тексту.
/// </summary>
/// <param name="Style">Тип стилю (наприклад, "BOLD", "ITALIC", "MONOSPACE").</param>
/// <param name="RangeStart">Початкова позиція стилю у тексті.</param>
/// <param name="RangeEnd">Кінцева позиція стилю у тексті.</param>
[PublicAPI]
public record JsonTextStyle(
    [property: JsonPropertyName("style")] string? Style,
    [property: JsonPropertyName("rangeStart")] int RangeStart,
    [property: JsonPropertyName("rangeEnd")] int RangeEnd
);

/// <summary>
/// Інформація про групу у повідомленні.
/// </summary>
/// <param name="GroupId">Ідентифікатор групи.</param>
/// <param name="GroupName">Назва групи.</param>
/// <param name="Revision">Ревізія групи.</param>
/// <param name="Type">Тип групової події.</param>
[PublicAPI]
public record JsonGroupInfo(
    [property: JsonPropertyName("groupId")] string? GroupId,
    [property: JsonPropertyName("groupName")] string? GroupName,
    [property: JsonPropertyName("revision")] int Revision,
    [property: JsonPropertyName("type")] string? Type
);

/// <summary>
/// Контекст історії для повідомлення.
/// </summary>
/// <param name="ContextInfo">Інформація про контекст історії.</param>
[PublicAPI]
public record JsonStoryContext(
    [property: JsonPropertyName("contextInfo")] string? ContextInfo
);

#endregion

#region EditMessage

/// <summary>
/// Повідомлення редагування попереднього повідомлення.
/// </summary>
/// <param name="TargetSentTimestamp">Часова мітка цільового повідомлення для редагування.</param>
/// <param name="DataMessage">Нові дані повідомлення, які замінять старі.</param>
[PublicAPI]
public record JsonEditMessage(
    [property: JsonPropertyName("targetSentTimestamp")] long TargetSentTimestamp,
    [property: JsonPropertyName("dataMessage")] JsonDataMessage DataMessage
);

#endregion

#region StoryMessage та вкладені типи

/// <summary>
/// Повідомлення історії у Signal.
/// </summary>
/// <param name="AllowsReplies">Чи дозволено відповіді на історію.</param>
/// <param name="GroupId">Ідентифікатор групи, якщо це групова історія.</param>
/// <param name="FileAttachment">Файлове вкладення історії.</param>
/// <param name="TextAttachment">Текстове вкладення історії.</param>
[PublicAPI]
public record JsonStoryMessage(
    [property: JsonPropertyName("allowsReplies")] bool AllowsReplies,
    [property: JsonPropertyName("groupId")] string? GroupId,
    [property: JsonPropertyName("fileAttachment")] JsonAttachment? FileAttachment,
    [property: JsonPropertyName("textAttachment")] TextAttachment? TextAttachment
);

/// <summary>
/// Текстове вкладення для історії.
/// </summary>
/// <param name="Text">Текст вкладення.</param>
/// <param name="Style">Стиль тексту.</param>
/// <param name="TextForegroundColor">Колір тексту.</param>
/// <param name="TextBackgroundColor">Колір фону тексту.</param>
/// <param name="Preview">Попередній перегляд посилання у тексті.</param>
/// <param name="BackgroundGradient">Градієнт фону.</param>
/// <param name="BackgroundColor">Колір фону.</param>
[PublicAPI]
public record TextAttachment(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("style")] string? Style,
    [property: JsonPropertyName("textForegroundColor")] string? TextForegroundColor,
    [property: JsonPropertyName("textBackgroundColor")] string? TextBackgroundColor,
    [property: JsonPropertyName("preview")] JsonPreview? Preview,
    [property: JsonPropertyName("backgroundGradient")] Gradient? BackgroundGradient,
    [property: JsonPropertyName("backgroundColor")] string? BackgroundColor
);

/// <summary>
/// Градієнт кольорів для фону.
/// </summary>
/// <param name="StartColor">Початковий колір градієнта.</param>
/// <param name="EndColor">Кінцевий колір градієнта.</param>
/// <param name="Colors">Список кольорів для градієнта.</param>
/// <param name="Positions">Позиції кольорів у градієнті.</param>
/// <param name="Angle">Кут нахилу градієнта.</param>
[PublicAPI]
public record Gradient(
    [property: JsonPropertyName("startColor")] string? StartColor,
    [property: JsonPropertyName("endColor")] string? EndColor,
    [property: JsonPropertyName("colors")] List<string> Colors,
    [property: JsonPropertyName("positions")] List<float> Positions,
    [property: JsonPropertyName("angle")] int? Angle
);

#endregion

#region SyncMessage та вкладені типи

/// <summary>
/// Повідомлення синхронізації між пристроями користувача.
/// </summary>
/// <param name="SentMessage">Відправлене повідомлення, що синхронізується.</param>
/// <param name="SentStoryMessage">Відправлене повідомлення історії, що синхронізується.</param>
/// <param name="BlockedNumbers">Заблоковані номери.</param>
/// <param name="BlockedGroupIds">Заблоковані ідентифікатори груп.</param>
/// <param name="ReadMessages">Прочитані повідомлення.</param>
/// <param name="Type">Тип повідомлення синхронізації.</param>
[PublicAPI]
public record JsonSyncMessage(
    [property: JsonPropertyName("sentMessage")] JsonSyncDataMessage? SentMessage,
    [property: JsonPropertyName("sentStoryMessage")] JsonSyncStoryMessage? SentStoryMessage,
    [property: JsonPropertyName("blockedNumbers")] List<string>? BlockedNumbers,
    [property: JsonPropertyName("blockedGroupIds")] List<string>? BlockedGroupIds,
    [property: JsonPropertyName("readMessages")] List<JsonSyncReadMessage>? ReadMessages,
    [property: JsonPropertyName("type")] JsonSyncMessageType? Type
);

/// <summary>
/// Дані повідомлення, що синхронізується.
/// </summary>
/// <param name="Destination">Отримувач повідомлення.</param>
/// <param name="DestinationNumber">Номер телефону отримувача.</param>
/// <param name="DestinationUuid">UUID отримувача.</param>
/// <param name="Timestamp">Часова мітка повідомлення.</param>
/// <param name="Message">Текст повідомлення.</param>
/// <param name="ExpiresInSeconds">Час самознищення повідомлення у секундах.</param>
/// <param name="ViewOnce">Чи можна переглянути повідомлення лише один раз.</param>
/// <param name="Quote">Цитата в повідомленні.</param>
/// <param name="Attachments">Вкладення в повідомленні.</param>
[PublicAPI]
public record JsonSyncDataMessage(
    [property: JsonPropertyName("destination")] string? Destination,
    [property: JsonPropertyName("destinationNumber")] string? DestinationNumber,
    [property: JsonPropertyName("destinationUuid")] string? DestinationUuid,
    [property: JsonPropertyName("timestamp")] long Timestamp,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("expiresInSeconds")] int? ExpiresInSeconds,
    [property: JsonPropertyName("viewOnce")] bool? ViewOnce,
    [property: JsonPropertyName("quote")] JsonQuote? Quote,
    [property: JsonPropertyName("attachments")] List<JsonAttachment>? Attachments
);

/// <summary>
/// Повідомлення історії, що синхронізується.
/// </summary>
/// <param name="StoryId">Ідентифікатор історії.</param>
/// <param name="Timestamp">Часова мітка історії.</param>
[PublicAPI]
public record JsonSyncStoryMessage(
    [property: JsonPropertyName("storyId")] string? StoryId,
    [property: JsonPropertyName("timestamp")] long Timestamp
);

/// <summary>
/// Прочитане повідомлення в синхронізації.
/// </summary>
/// <param name="Sender">Відправник повідомлення.</param>
/// <param name="SenderNumber">Номер телефону відправника.</param>
/// <param name="SenderUuid">UUID відправника.</param>
/// <param name="Timestamp">Часова мітка повідомлення.</param>
[PublicAPI]
public record JsonSyncReadMessage(
    [property: JsonPropertyName("sender")] string? Sender,
    [property: JsonPropertyName("senderNumber")] string? SenderNumber,
    [property: JsonPropertyName("senderUuid")] string? SenderUuid,
    [property: JsonPropertyName("timestamp")] long Timestamp
);

/// <summary>
/// Типи повідомлень синхронізації.
/// </summary>
[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter<JsonSyncMessageType>))]
public enum JsonSyncMessageType
{
    /// <summary>
    /// Синхронізація контактів.
    /// </summary>
    CONTACTS_SYNC,
    
    /// <summary>
    /// Синхронізація груп.
    /// </summary>
    GROUPS_SYNC,
    
    /// <summary>
    /// Запит на синхронізацію.
    /// </summary>
    REQUEST_SYNC
}

#endregion

#region CallMessage та вкладені типи

/// <summary>
/// Повідомлення виклику.
/// </summary>
/// <param name="OfferMessage">Повідомлення пропозиції виклику.</param>
/// <param name="AnswerMessage">Повідомлення відповіді на виклик.</param>
/// <param name="BusyMessage">Повідомлення про зайнятість для виклику.</param>
/// <param name="HangupMessage">Повідомлення про завершення виклику.</param>
/// <param name="IceUpdateMessages">Повідомлення про оновлення ICE для виклику.</param>
[PublicAPI]
public record JsonCallMessage(
    [property: JsonPropertyName("offerMessage")] Offer? OfferMessage,
    [property: JsonPropertyName("answerMessage")] Answer? AnswerMessage,
    [property: JsonPropertyName("busyMessage")] Busy? BusyMessage,
    [property: JsonPropertyName("hangupMessage")] Hangup? HangupMessage,
    [property: JsonPropertyName("iceUpdateMessages")] List<IceUpdate>? IceUpdateMessages
);

/// <summary>
/// Пропозиція виклику.
/// </summary>
/// <param name="Id">Ідентифікатор виклику.</param>
/// <param name="Type">Тип пропозиції.</param>
/// <param name="Opaque">Непрозорі дані пропозиції виклику.</param>
[PublicAPI]
public record Offer(
    [property: JsonPropertyName("id")] long Id,
    // post-modernize-tuning §4.17 (audit N3): always-present у wire-контракті — [JsonRequired]
    [property: JsonPropertyName("type"), JsonRequired] string Type,
    [property: JsonPropertyName("opaque"), JsonRequired] string Opaque
);

/// <summary>
/// Відповідь на виклик.
/// </summary>
/// <param name="Id">Ідентифікатор виклику.</param>
/// <param name="Opaque">Непрозорі дані відповіді на виклик.</param>
[PublicAPI]
public record Answer(
    [property: JsonPropertyName("id")] long Id,
    // post-modernize-tuning §4.17: always-present у wire-контракті signal-cli.
    [property: JsonPropertyName("opaque"), JsonRequired] string Opaque
);

/// <summary>
/// Повідомлення про зайнятість для виклику.
/// </summary>
/// <param name="Id">Ідентифікатор виклику.</param>
[PublicAPI]
public record Busy(
    [property: JsonPropertyName("id")] long Id
);

/// <summary>
/// Повідомлення про завершення виклику.
/// </summary>
/// <param name="Id">Ідентифікатор виклику.</param>
/// <param name="Type">Тип завершення виклику.</param>
/// <param name="DeviceId">Ідентифікатор пристрою.</param>
[PublicAPI]
public record Hangup(
    [property: JsonPropertyName("id")] long Id,
    // post-modernize-tuning §4.17: signal-cli не емітить Hangup без type.
    [property: JsonPropertyName("type"), JsonRequired] string Type,
    [property: JsonPropertyName("deviceId")] int DeviceId
);

/// <summary>
/// Оновлення ICE для виклику.
/// </summary>
/// <param name="Id">Ідентифікатор виклику.</param>
/// <param name="Opaque">Непрозорі дані оновлення ICE.</param>
[PublicAPI]
public record IceUpdate(
    [property: JsonPropertyName("id")] long Id,
    // post-modernize-tuning §4.17: signal-cli не емітить IceUpdate без opaque payload.
    [property: JsonPropertyName("opaque"), JsonRequired] string Opaque
);

#endregion

#region ReceiptMessage

/// <summary>
/// Повідомлення квитанції (підтвердження отримання, прочитання, перегляду).
/// </summary>
/// <param name="When">Часова мітка квитанції.</param>
/// <param name="IsDelivery">Чи є квитанція підтвердженням доставки.</param>
/// <param name="IsRead">Чи є квитанція підтвердженням прочитання.</param>
/// <param name="IsViewed">Чи є квитанція підтвердженням перегляду.</param>
/// <param name="Timestamps">Часові мітки повідомлень, до яких відноситься квитанція.</param>
[PublicAPI]
public record JsonReceiptMessage(
    [property: JsonPropertyName("when")] long When,
    [property: JsonPropertyName("isDelivery")] bool IsDelivery,
    [property: JsonPropertyName("isRead")] bool IsRead,
    [property: JsonPropertyName("isViewed")] bool IsViewed,
    [property: JsonPropertyName("timestamps")] List<long> Timestamps
);

#endregion

#region TypingMessage

/// <summary>
/// Повідомлення про набір тексту користувачем.
/// </summary>
/// <param name="Action">Дія друку (початок, завершення).</param>
/// <param name="Timestamp">Часова мітка повідомлення про набір тексту.</param>
/// <param name="GroupId">Ідентифікатор групи, якщо набір тексту відбувається в групі.</param>
[PublicAPI]
public record JsonTypingMessage(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("timestamp")] long Timestamp,
    [property: JsonPropertyName("groupId")] string? GroupId
);

#endregion