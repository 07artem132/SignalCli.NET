using JetBrains.Annotations;
using System.Text.Json.Serialization;

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
    [property: JsonPropertyName("account")]
    string Account,

    [property: JsonPropertyName("recipient")]
    IEnumerable<string> Recipients,

    [property: JsonPropertyName("group-id")]
    IEnumerable<string>? GroupIds,
    [property: JsonPropertyName("note-to-self")]
    bool NoteToSelf,
    [property: JsonPropertyName("endSession")]
    bool EndSession,
    [property: JsonPropertyName("message")]
    string Message,

    [property: JsonPropertyName("attachment")]
    IEnumerable<string>? Attachments,
    [property: JsonPropertyName("mentions")]
    IEnumerable<string>? Mentions,
    [property: JsonPropertyName("text-style")]
    IEnumerable<string>? TextStyle,
    [property: JsonPropertyName("quote-timestamp")]
    ulong? QuoteTimestamp,
    [property: JsonPropertyName("quote-author")]
    string? QuoteAuthor,
    [property: JsonPropertyName("quote-message")]
    string? QuoteMessage,
    [property: JsonPropertyName("quote-mention")]
    IEnumerable<string>? QuoteMention,
    [property: JsonPropertyName("quote-text-style")]
    IEnumerable<string>? QuoteTextStyle,
    [property: JsonPropertyName("quote-attachment")]
    IEnumerable<string>? QuoteAttachment,
    [property: JsonPropertyName("preview_url")]
    string? PreviewUrl,
    [property: JsonPropertyName("preview_title")]
    string? PreviewTitle,
    [property: JsonPropertyName("preview_description")]
    string? PreviewDescription,
    [property: JsonPropertyName("preview_image")]
    string? PreviewImage,
    [property: JsonPropertyName("sticker")]
    string? Sticker,
    [property: JsonPropertyName("storyTimestamp")]
    ulong? StoryTimestamp,
    [property: JsonPropertyName("storyAuthor")]
    string? StoryAuthor,
    [property: JsonPropertyName("editTimestamp")]
    ulong? EditTimestamp
);