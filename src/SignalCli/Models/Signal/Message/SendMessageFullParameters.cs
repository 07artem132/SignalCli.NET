using JetBrains.Annotations;
using Newtonsoft.Json;

namespace SignalCli.Models.Signal.Message;

/// <summary>
/// Повний набір параметрів для відправки повідомлення через Signal.
/// </summary>
/// <remarks>
/// Інкапсулює всі можливі опції для відправки повідомлень різних типів:
/// текстові повідомлення, вкладення, стікери, цитування, форматування,
/// посилання з прев'ю, редагування повідомлень, тощо.
/// </remarks>
/// <param name="Account">Ідентифікатор акаунта відправника.</param>
/// <param name="Recipients">Колекція ідентифікаторів індивідуальних одержувачів.</param>
/// <param name="GroupIds">Колекція ідентифікаторів груп (зазвичай використовується лише один).</param>
/// <param name="NoteToSelf">Якщо true, відправляє повідомлення самому собі без сповіщення.</param>
/// <param name="EndSession">Якщо true, скидає стан сесії та відправляє повідомлення про завершення сесії.</param>
/// <param name="Message">Текст повідомлення для відправки.</param>
/// <param name="Attachments">Вкладення: шляхи до файлів або дані у форматі Data URI.</param>
/// <param name="Mentions">Згадування користувачів у форматі "start:length:recipientNumber".</param>
/// <param name="TextStyle">Стилі форматування тексту у форматі "start:length:STYLE".</param>
/// <param name="QuoteTimestamp">Часова мітка цитованого повідомлення.</param>
/// <param name="QuoteAuthor">Ідентифікатор автора цитованого повідомлення.</param>
/// <param name="QuoteMessage">Текст цитованого повідомлення.</param>
/// <param name="QuoteMention">Згадування в цитованому повідомленні.</param>
/// <param name="QuoteTextStyle">Стилі форматування в цитованому повідомленні.</param>
/// <param name="QuoteAttachment">Вкладення в цитованому повідомленні.</param>
/// <param name="PreviewUrl">URL для прев'ю посилання.</param>
/// <param name="PreviewTitle">Заголовок для прев'ю посилання.</param>
/// <param name="PreviewDescription">Опис для прев'ю посилання.</param>
/// <param name="PreviewImage">Зображення для прев'ю посилання.</param>
/// <param name="Sticker">Ідентифікатор стікера у форматі "stickerPackId:stickerId".</param>
/// <param name="StoryTimestamp">Часова мітка історії, на яку надсилається відповідь.</param>
/// <param name="StoryAuthor">Автор історії, на яку надсилається відповідь.</param>
/// <param name="EditTimestamp">Часова мітка повідомлення, яке редагується.</param>
[PublicAPI]
public sealed record SendMessageFullParameters(
    [property: JsonProperty("account", NullValueHandling = NullValueHandling.Ignore)]
    string Account,

    [property: JsonProperty("recipient", NullValueHandling = NullValueHandling.Ignore)]
    IEnumerable<string> Recipients,

    [property: JsonProperty("group-id", NullValueHandling = NullValueHandling.Ignore)]
    IEnumerable<string>? GroupIds,
    [property: JsonProperty("note-to-self", NullValueHandling = NullValueHandling.Ignore)]
    bool NoteToSelf,
    [property: JsonProperty("endSession", NullValueHandling = NullValueHandling.Ignore)]
    bool EndSession,
    [property: JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
    string Message,

    [property: JsonProperty("attachment", NullValueHandling = NullValueHandling.Ignore)]
    IEnumerable<string>? Attachments,
    [property: JsonProperty("mentions", NullValueHandling = NullValueHandling.Ignore)]
    IEnumerable<string>? Mentions,
    [property: JsonProperty("text-style", NullValueHandling = NullValueHandling.Ignore)]
    IEnumerable<string>? TextStyle,
    [property: JsonProperty("quote-timestamp", NullValueHandling = NullValueHandling.Ignore)]
    ulong? QuoteTimestamp,
    [property: JsonProperty("quote-author", NullValueHandling = NullValueHandling.Ignore)]
    string? QuoteAuthor,
    [property: JsonProperty("quote-message", NullValueHandling = NullValueHandling.Ignore)]
    string? QuoteMessage,
    [property: JsonProperty("quote-mention", NullValueHandling = NullValueHandling.Ignore)]
    IEnumerable<string>? QuoteMention,
    [property: JsonProperty("quote-text-style", NullValueHandling = NullValueHandling.Ignore)]
    IEnumerable<string>? QuoteTextStyle,
    [property: JsonProperty("quote-attachment", NullValueHandling = NullValueHandling.Ignore)]
    IEnumerable<string>? QuoteAttachment,
    [property: JsonProperty("preview_url", NullValueHandling = NullValueHandling.Ignore)]
    string? PreviewUrl,
    [property: JsonProperty("preview_title", NullValueHandling = NullValueHandling.Ignore)]
    string? PreviewTitle,
    [property: JsonProperty("preview_description", NullValueHandling = NullValueHandling.Ignore)]
    string? PreviewDescription,
    [property: JsonProperty("preview_image", NullValueHandling = NullValueHandling.Ignore)]
    string? PreviewImage,
    [property: JsonProperty("sticker", NullValueHandling = NullValueHandling.Ignore)]
    string? Sticker,
    [property: JsonProperty("storyTimestamp", NullValueHandling = NullValueHandling.Ignore)]
    ulong? StoryTimestamp,
    [property: JsonProperty("storyAuthor", NullValueHandling = NullValueHandling.Ignore)]
    string? StoryAuthor,
    [property: JsonProperty("editTimestamp", NullValueHandling = NullValueHandling.Ignore)]
    ulong? EditTimestamp
);