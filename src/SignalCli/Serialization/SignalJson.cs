using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace SignalCli.Serialization;

/// <summary>
/// Спільні налаштування System.Text.Json для протоколу signal-cli JSON-RPC.
/// </summary>
/// <remarks>
/// Newtonsoft за замовчуванням робив case-insensitive біндинг — відтворюємо це.
/// Null-значення не пишемо (signal-cli очікує відсутні поля, а не null).
/// Рядкові enum обробляються через атрибут [JsonConverter] на самих enum.
/// </remarks>
internal static class SignalJson
{
    /// <summary>
    /// Єдиний екземпляр налаштувань серіалізації (потокобезпечний після створення).
    /// </summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Newtonsoft був толерантним; зберігаємо стійкість до зайвих ком/коментарів.
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        // Це локальний stdin/stdout канал (не HTML), тож мінімальне екранування —
        // не екрануємо '+', '<' тощо у "+" (як це робив Newtonsoft).
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        // Source-gen метадані для відомих типів + reflection-fallback для решти
        // (анонімні типи в тестах, JsonElement тощо).
        TypeInfoResolver = JsonTypeInfoResolver.Combine(
            SignalJsonContext.Default, new DefaultJsonTypeInfoResolver())
    };
}
