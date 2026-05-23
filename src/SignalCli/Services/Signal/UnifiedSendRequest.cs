using SignalCli.Interfaces.FileSystem;
using SignalCli.Interfaces.Signal;
using SignalCli.Models.Signal.Message;

namespace SignalCli.Services.Signal;

/// <summary>
/// post-modernize-tuning §8c.6 (audit C7): internal DTO для <c>SignalMessage.SendUnifiedMessageAsync</c>.
/// Раніше метод приймав 23 окремих параметри — це робило сigna нечитабельною, а додавання
/// нового поля (типу <c>storyTimestamp</c>) — болючим. Тепер усі параметри пакуються в один record,
/// а public-обгортки (<c>SendTextMessageAsync</c>/<c>SendAttachmentAsync</c>/<c>SendStickerAsync</c>)
/// будують його з типобезпечного <c>*MessageOptions</c>. <b>Не public</b> — тільки internal.
/// </summary>
/// <param name="Account">Акаунт-відправник.</param>
/// <param name="Recipients">Список отримувачів (user або group, узгоджуються <see cref="SignalMessage"/>-валідацією).</param>
/// <param name="Message">Тіло повідомлення (може бути порожнім для стікерів).</param>
/// <param name="NoteToSelf">Чи відправляти у власний "Note to Self"-чат.</param>
/// <param name="EndSession">Чи завершувати session-key для отримувача.</param>
/// <param name="TextMode">Режим парсингу markdown-стилів (None / Styled).</param>
/// <param name="Attachments">Вкладення (data-URI або temp-file шляхи — вибирається в <see cref="SignalMessage"/>).</param>
/// <param name="Mentions">UUID/номери для згадки у тексті.</param>
/// <param name="QuoteTimestamp">Timestamp цитованого повідомлення.</param>
/// <param name="QuoteAuthor">Автор цитованого повідомлення.</param>
/// <param name="QuoteMessage">Текст цитованого повідомлення.</param>
/// <param name="QuoteMentions">Згадки у цитаті.</param>
/// <param name="QuoteTextStyles">Стилі тексту у цитаті.</param>
/// <param name="QuoteAttachments">Вкладення цитати.</param>
/// <param name="ExternalTextStyles">Стилі тексту, передані ззовні (мають пріоритет якщо <c>TextMode == None</c>).</param>
/// <param name="EditTimestamp">Timestamp повідомлення, яке редагується.</param>
/// <param name="Sticker">Sticker id у форматі <c>packId:stickerId</c>.</param>
/// <param name="PreviewUrl">URL для попереднього перегляду посилання.</param>
/// <param name="PreviewTitle">Заголовок попереднього перегляду.</param>
/// <param name="PreviewDescription">Опис попереднього перегляду.</param>
/// <param name="PreviewImage">Шлях до зображення попереднього перегляду.</param>
/// <param name="StoryTimestamp">Timestamp story-контексту (для відповідей на сторіс).</param>
/// <param name="StoryAuthor">Автор story-контексту.</param>
internal sealed record UnifiedSendRequest(
    string Account,
    IEnumerable<IRecipient> Recipients,
    string Message,
    bool NoteToSelf,
    bool EndSession,
    TextStyleMode TextMode,
    IEnumerable<IAttachmentEntry>? Attachments,
    IEnumerable<string>? Mentions,
    ulong? QuoteTimestamp,
    string? QuoteAuthor,
    string? QuoteMessage,
    IEnumerable<string>? QuoteMentions,
    IEnumerable<string>? QuoteTextStyles,
    IEnumerable<string>? QuoteAttachments,
    IEnumerable<string>? ExternalTextStyles,
    ulong? EditTimestamp,
    string? Sticker,
    string? PreviewUrl,
    string? PreviewTitle,
    string? PreviewDescription,
    string? PreviewImage,
    ulong? StoryTimestamp,
    string? StoryAuthor);
