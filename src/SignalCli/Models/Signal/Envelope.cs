using JetBrains.Annotations;
using Newtonsoft.Json;

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
    [property: JsonProperty("source")] string? Source,
    [property: JsonProperty("sourceNumber")] string? SourceNumber,
    [property: JsonProperty("sourceUuid")] string? SourceUuid,
    [property: JsonProperty("sourceName")] string? SourceName,
    [property: JsonProperty("sourceDevice")] int? SourceDevice,
    [property: JsonProperty("timestamp")] long Timestamp,
    [property: JsonProperty("serverReceivedTimestamp")] long ServerReceivedTimestamp,
    [property: JsonProperty("serverDeliveredTimestamp")] long ServerDeliveredTimestamp,
    [property: JsonProperty("dataMessage", NullValueHandling = NullValueHandling.Ignore)] JsonDataMessage? DataMessage,
    [property: JsonProperty("editMessage", NullValueHandling = NullValueHandling.Ignore)] JsonEditMessage? EditMessage,
    [property: JsonProperty("storyMessage", NullValueHandling = NullValueHandling.Ignore)] JsonStoryMessage? StoryMessage,
    [property: JsonProperty("syncMessage", NullValueHandling = NullValueHandling.Ignore)] JsonSyncMessage? SyncMessage,
    [property: JsonProperty("callMessage", NullValueHandling = NullValueHandling.Ignore)] JsonCallMessage? CallMessage,
    [property: JsonProperty("receiptMessage", NullValueHandling = NullValueHandling.Ignore)] JsonReceiptMessage? ReceiptMessage,
    [property: JsonProperty("typingMessage", NullValueHandling = NullValueHandling.Ignore)] JsonTypingMessage? TypingMessage
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
    [property: JsonProperty("timestamp")] ulong Timestamp,
    [property: JsonProperty("message")] string? Message,
    [property: JsonProperty("expiresInSeconds")] int? ExpiresInSeconds,
    [property: JsonProperty("viewOnce", NullValueHandling = NullValueHandling.Ignore)] bool? ViewOnce,
    [property: JsonProperty("reaction", NullValueHandling = NullValueHandling.Ignore)] JsonReaction? Reaction,
    [property: JsonProperty("quote", NullValueHandling = NullValueHandling.Ignore)] JsonQuote? Quote,
    [property: JsonProperty("payment", NullValueHandling = NullValueHandling.Ignore)] JsonPayment? Payment,
    [property: JsonProperty("mentions", NullValueHandling = NullValueHandling.Ignore)] List<JsonMention>? Mentions,
    [property: JsonProperty("previews", NullValueHandling = NullValueHandling.Ignore)] List<JsonPreview>? Previews,
    [property: JsonProperty("attachments", NullValueHandling = NullValueHandling.Ignore)] List<JsonAttachment>? Attachments,
    [property: JsonProperty("sticker", NullValueHandling = NullValueHandling.Ignore)] JsonSticker? Sticker,
    [property: JsonProperty("remoteDelete", NullValueHandling = NullValueHandling.Ignore)] JsonRemoteDelete? RemoteDelete,
    [property: JsonProperty("contacts", NullValueHandling = NullValueHandling.Ignore)] List<JsonSharedContact>? Contacts,
    [property: JsonProperty("textStyles", NullValueHandling = NullValueHandling.Ignore)] List<JsonTextStyle>? TextStyles,
    [property: JsonProperty("groupInfo", NullValueHandling = NullValueHandling.Ignore)] JsonGroupInfo? GroupInfo,
    [property: JsonProperty("storyContext", NullValueHandling = NullValueHandling.Ignore)] JsonStoryContext? StoryContext
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
    [property: JsonProperty("emoji")] string? Emoji,
    [property: JsonProperty("targetAuthor")] string? TargetAuthor,
    [property: JsonProperty("targetAuthorNumber")] string? TargetAuthorNumber,
    [property: JsonProperty("targetAuthorUuid")] string? TargetAuthorUuid,
    [property: JsonProperty("targetSentTimestamp")] long TargetSentTimestamp,
    [property: JsonProperty("isRemove")] bool IsRemove
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
    [property: JsonProperty("id")] long Id,
    [property: JsonProperty("author")] string? Author,
    [property: JsonProperty("authorNumber")] string? AuthorNumber,
    [property: JsonProperty("authorUuid")] string? AuthorUuid,
    [property: JsonProperty("text")] string? Text,
    [property: JsonProperty("attachments", NullValueHandling = NullValueHandling.Ignore)] List<JsonAttachment>? Attachments
);

/// <summary>
/// Інформація про платіж.
/// </summary>
/// <param name="Amount">Сума платежу.</param>
/// <param name="Currency">Валюта платежу.</param>
[PublicAPI]
public record JsonPayment(
    [property: JsonProperty("amount")] decimal Amount,
    [property: JsonProperty("currency")] string? Currency
);

/// <summary>
/// Згадка користувача у повідомленні.
/// </summary>
/// <param name="Id">Ідентифікатор згаданого користувача.</param>
[PublicAPI]
public record JsonMention(
    [property: JsonProperty("id")] string? Id
);

/// <summary>
/// Попередній перегляд посилання у повідомленні.
/// </summary>
/// <param name="Url">URL посилання.</param>
/// <param name="Title">Заголовок попереднього перегляду.</param>
/// <param name="Description">Опис попереднього перегляду.</param>
[PublicAPI]
public record JsonPreview(
    [property: JsonProperty("url")] string? Url,
    [property: JsonProperty("title")] string? Title,
    [property: JsonProperty("description")] string? Description
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
    [property: JsonProperty("contentType")] string? ContentType,
    [property: JsonProperty("filename")] string? Filename,
    [property: JsonProperty("id")] string? Id,
    [property: JsonProperty("size")] long Size,
    [property: JsonProperty("width")] int? Width,
    [property: JsonProperty("height")] int? Height,
    [property: JsonProperty("caption")] string? Caption,
    [property: JsonProperty("uploadTimestamp")] long? UploadTimestamp
);

/// <summary>
/// Стікер у повідомленні.
/// </summary>
/// <param name="PackId">Ідентифікатор пакету стікерів.</param>
/// <param name="StickerId">Ідентифікатор стікера в пакеті.</param>
[PublicAPI]
public record JsonSticker(
    [property: JsonProperty("packId")] string? PackId,
    [property: JsonProperty("stickerId")] int? StickerId
);

/// <summary>
/// Інформація про віддалене видалення повідомлення.
/// </summary>
/// <param name="RemoteDeleteId">Ідентифікатор повідомлення для видалення.</param>
[PublicAPI]
public record JsonRemoteDelete(
    [property: JsonProperty("remoteDeleteId")] string RemoteDeleteId
);

/// <summary>
/// Контакт, яким поділилися у повідомленні.
/// </summary>
/// <param name="Name">Ім'я контакту.</param>
/// <param name="PhoneNumber">Номер телефону контакту.</param>
[PublicAPI]
public record JsonSharedContact(
    [property: JsonProperty("name")] string? Name,
    [property: JsonProperty("phoneNumber")] string? PhoneNumber
);

/// <summary>
/// Стиль форматування тексту.
/// </summary>
/// <param name="Style">Тип стилю (наприклад, "BOLD", "ITALIC", "MONOSPACE").</param>
/// <param name="RangeStart">Початкова позиція стилю у тексті.</param>
/// <param name="RangeEnd">Кінцева позиція стилю у тексті.</param>
[PublicAPI]
public record JsonTextStyle(
    [property: JsonProperty("style")] string? Style,
    [property: JsonProperty("rangeStart")] int RangeStart,
    [property: JsonProperty("rangeEnd")] int RangeEnd
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
    [property: JsonProperty("groupId")] string? GroupId,
    [property: JsonProperty("groupName")] string? GroupName,
    [property: JsonProperty("revision")] int Revision,
    [property: JsonProperty("type")] string? Type
);

/// <summary>
/// Контекст історії для повідомлення.
/// </summary>
/// <param name="ContextInfo">Інформація про контекст історії.</param>
[PublicAPI]
public record JsonStoryContext(
    [property: JsonProperty("contextInfo")] string? ContextInfo
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
    [property: JsonProperty("targetSentTimestamp")] long TargetSentTimestamp,
    [property: JsonProperty("dataMessage")] JsonDataMessage DataMessage
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
    [property: JsonProperty("allowsReplies")] bool AllowsReplies,
    [property: JsonProperty("groupId", NullValueHandling = NullValueHandling.Ignore)] string? GroupId,
    [property: JsonProperty("fileAttachment", NullValueHandling = NullValueHandling.Ignore)] JsonAttachment? FileAttachment,
    [property: JsonProperty("textAttachment", NullValueHandling = NullValueHandling.Ignore)] TextAttachment? TextAttachment
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
    [property: JsonProperty("text")] string Text,
    [property: JsonProperty("style", NullValueHandling = NullValueHandling.Ignore)] string? Style,
    [property: JsonProperty("textForegroundColor", NullValueHandling = NullValueHandling.Ignore)] string? TextForegroundColor,
    [property: JsonProperty("textBackgroundColor", NullValueHandling = NullValueHandling.Ignore)] string? TextBackgroundColor,
    [property: JsonProperty("preview", NullValueHandling = NullValueHandling.Ignore)] JsonPreview? Preview,
    [property: JsonProperty("backgroundGradient", NullValueHandling = NullValueHandling.Ignore)] Gradient? BackgroundGradient,
    [property: JsonProperty("backgroundColor", NullValueHandling = NullValueHandling.Ignore)] string? BackgroundColor
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
    [property: JsonProperty("startColor", NullValueHandling = NullValueHandling.Ignore)] string? StartColor,
    [property: JsonProperty("endColor", NullValueHandling = NullValueHandling.Ignore)] string? EndColor,
    [property: JsonProperty("colors")] List<string> Colors,
    [property: JsonProperty("positions")] List<float> Positions,
    [property: JsonProperty("angle", NullValueHandling = NullValueHandling.Ignore)] int? Angle
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
    [property: JsonProperty("sentMessage", NullValueHandling = NullValueHandling.Ignore)] JsonSyncDataMessage? SentMessage,
    [property: JsonProperty("sentStoryMessage", NullValueHandling = NullValueHandling.Ignore)] JsonSyncStoryMessage? SentStoryMessage,
    [property: JsonProperty("blockedNumbers", NullValueHandling = NullValueHandling.Ignore)] List<string>? BlockedNumbers,
    [property: JsonProperty("blockedGroupIds", NullValueHandling = NullValueHandling.Ignore)] List<string>? BlockedGroupIds,
    [property: JsonProperty("readMessages", NullValueHandling = NullValueHandling.Ignore)] List<JsonSyncReadMessage>? ReadMessages,
    [property: JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)] JsonSyncMessageType? Type
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
    [property: JsonProperty("destination")] string? Destination,
    [property: JsonProperty("destinationNumber", NullValueHandling = NullValueHandling.Ignore)] string? DestinationNumber,
    [property: JsonProperty("destinationUuid", NullValueHandling = NullValueHandling.Ignore)] string? DestinationUuid,
    [property: JsonProperty("timestamp")] long Timestamp,
    [property: JsonProperty("message")] string? Message,
    [property: JsonProperty("expiresInSeconds")] int? ExpiresInSeconds,
    [property: JsonProperty("viewOnce")] bool? ViewOnce,
    [property: JsonProperty("quote", NullValueHandling = NullValueHandling.Ignore)] JsonQuote? Quote,
    [property: JsonProperty("attachments", NullValueHandling = NullValueHandling.Ignore)] List<JsonAttachment>? Attachments
);

/// <summary>
/// Повідомлення історії, що синхронізується.
/// </summary>
/// <param name="StoryId">Ідентифікатор історії.</param>
/// <param name="Timestamp">Часова мітка історії.</param>
[PublicAPI]
public record JsonSyncStoryMessage(
    [property: JsonProperty("storyId", NullValueHandling = NullValueHandling.Ignore)] string? StoryId,
    [property: JsonProperty("timestamp")] long Timestamp
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
    [property: JsonProperty("sender")] string? Sender,
    [property: JsonProperty("senderNumber")] string? SenderNumber,
    [property: JsonProperty("senderUuid")] string? SenderUuid,
    [property: JsonProperty("timestamp")] long Timestamp
);

/// <summary>
/// Типи повідомлень синхронізації.
/// </summary>
[PublicAPI]
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
    [property: JsonProperty("offerMessage", NullValueHandling = NullValueHandling.Ignore)] Offer? OfferMessage,
    [property: JsonProperty("answerMessage", NullValueHandling = NullValueHandling.Ignore)] Answer? AnswerMessage,
    [property: JsonProperty("busyMessage", NullValueHandling = NullValueHandling.Ignore)] Busy? BusyMessage,
    [property: JsonProperty("hangupMessage", NullValueHandling = NullValueHandling.Ignore)] Hangup? HangupMessage,
    [property: JsonProperty("iceUpdateMessages", NullValueHandling = NullValueHandling.Ignore)] List<IceUpdate>? IceUpdateMessages
);

/// <summary>
/// Пропозиція виклику.
/// </summary>
/// <param name="Id">Ідентифікатор виклику.</param>
/// <param name="Type">Тип пропозиції.</param>
/// <param name="Opaque">Непрозорі дані пропозиції виклику.</param>
[PublicAPI]
public record Offer(
    [property: JsonProperty("id")] long Id,
    [property: JsonProperty("type")] string Type,
    [property: JsonProperty("opaque")] string Opaque
);

/// <summary>
/// Відповідь на виклик.
/// </summary>
/// <param name="Id">Ідентифікатор виклику.</param>
/// <param name="Opaque">Непрозорі дані відповіді на виклик.</param>
[PublicAPI]
public record Answer(
    [property: JsonProperty("id")] long Id,
    [property: JsonProperty("opaque")] string Opaque
);

/// <summary>
/// Повідомлення про зайнятість для виклику.
/// </summary>
/// <param name="Id">Ідентифікатор виклику.</param>
[PublicAPI]
public record Busy(
    [property: JsonProperty("id")] long Id
);

/// <summary>
/// Повідомлення про завершення виклику.
/// </summary>
/// <param name="Id">Ідентифікатор виклику.</param>
/// <param name="Type">Тип завершення виклику.</param>
/// <param name="DeviceId">Ідентифікатор пристрою.</param>
[PublicAPI]
public record Hangup(
    [property: JsonProperty("id")] long Id,
    [property: JsonProperty("type")] string Type,
    [property: JsonProperty("deviceId")] int DeviceId
);

/// <summary>
/// Оновлення ICE для виклику.
/// </summary>
/// <param name="Id">Ідентифікатор виклику.</param>
/// <param name="Opaque">Непрозорі дані оновлення ICE.</param>
[PublicAPI]
public record IceUpdate(
    [property: JsonProperty("id")] long Id,
    [property: JsonProperty("opaque")] string Opaque
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
    [property: JsonProperty("when")] long When,
    [property: JsonProperty("isDelivery")] bool IsDelivery,
    [property: JsonProperty("isRead")] bool IsRead,
    [property: JsonProperty("isViewed")] bool IsViewed,
    [property: JsonProperty("timestamps")] List<long> Timestamps
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
    [property: JsonProperty("action")] string Action,
    [property: JsonProperty("timestamp")] long Timestamp,
    [property: JsonProperty("groupId", NullValueHandling = NullValueHandling.Ignore)] string? GroupId
);

#endregion