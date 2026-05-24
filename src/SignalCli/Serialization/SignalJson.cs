using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace SignalCli.Serialization;

/// <summary>
/// Спільні налаштування System.Text.Json для протоколу signal-cli JSON-RPC.
/// </summary>
/// <remarks>
/// <para>
/// post-modernize-tuning §6.4 (audit P6): production-options використовують **виключно**
/// source-generated <see cref="SignalJsonContext.Default"/>-резолвер. Reflection-fallback
/// (<see cref="DefaultJsonTypeInfoResolver"/>) <b>видалено</b>. Кожен тип, що крос-уйде
/// JSON-кордон, МАЄ бути зареєстрований у <see cref="SignalJsonContext"/> через
/// <c>[JsonSerializable(typeof(T))]</c>; інакше <see cref="JsonSerializer"/> кине
/// <see cref="NotSupportedException"/> на runtime (захист — <c>JsonContextRegistrationTests</c> §6.12).
/// </para>
/// <para>
/// Це робить <c>SignalCli.csproj</c> повністю AOT-сумісним: жодного reflection-emit на
/// JSON-шляху, <c>&lt;IsAotCompatible&gt;true&lt;/IsAotCompatible&gt;</c> проходить без
/// IL2026/IL3050 warnings із цього модуля.
/// </para>
/// <para>
/// Newtonsoft за замовчуванням робив case-insensitive біндинг — відтворюємо це.
/// Null-значення не пишемо (signal-cli очікує відсутні поля, а не null).
/// Рядкові enum обробляються через атрибут [JsonConverter] на самих enum.
/// </para>
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
        // §6.4: ТІЛЬКИ source-gen контекст. Reflection-fallback видалено заради AOT-готовності.
        TypeInfoResolver = SignalJsonContext.Default,
    };

    /// <summary>
    /// post-modernize-tuning §6.10 (audit P6): test-only options-instance із reflection-fallback.
    /// Дозволяє тестам використовувати анонімні типи (<c>new { Hello = "world" }</c>) як payload.
    /// </summary>
    /// <remarks>
    /// Production-код НЕ повинен використовувати це — він зобовʼязаний працювати через
    /// <see cref="Options"/> і реєструвати всі типи в <see cref="SignalJsonContext"/>.
    /// Видимий лише для <c>SignalCli.Tests</c> через <c>[InternalsVisibleTo]</c>.
    /// Атрибути <see cref="RequiresUnreferencedCodeAttribute"/>/<see cref="RequiresDynamicCodeAttribute"/>
    /// сигналізують AOT-аналізатору, що цей шлях небезпечний; consumer'и (тести) приймають це
    /// явно. Атрибути валідні на методах, не на полях — тому property-getter.
    /// </remarks>
    public static JsonSerializerOptions OptionsForTests
    {
        [RequiresUnreferencedCode("Test-only — uses reflection-based DefaultJsonTypeInfoResolver. Production code MUST use Options + SignalJsonContext.")]
        [RequiresDynamicCode("Test-only — uses reflection-based DefaultJsonTypeInfoResolver. Production code MUST use Options + SignalJsonContext.")]
        get => GetOptionsForTestsImpl();
    }

    [RequiresUnreferencedCode("Test-only — uses reflection-based DefaultJsonTypeInfoResolver. Production code MUST use Options + SignalJsonContext.")]
    [RequiresDynamicCode("Test-only — uses reflection-based DefaultJsonTypeInfoResolver. Production code MUST use Options + SignalJsonContext.")]
    private static JsonSerializerOptions GetOptionsForTestsImpl() => _optionsForTests.Value;

    // Lazy<>-ініціалізація не приймає [RequiresUnreferencedCode] на field-initializer.
    // Suppress is justified: реальний access іде ВИКЛЮЧНО через `OptionsForTests`-property,
    // що несе ті ж самі IL2026/IL3050-атрибути. Lazy-field — захоплення factory-ref,
    // фактичний виклик відбувається при першому read'і property — звідти warnings уже
    // прокидаються консумеру (тестам). Production-код використовує `Options` — без reflection.
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Test-only path; gated by OptionsForTests property which carries RequiresUnreferencedCode.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Test-only path; gated by OptionsForTests property which carries RequiresDynamicCode.")]
    private static readonly Lazy<JsonSerializerOptions> _optionsForTests =
        new(CreateOptionsForTests);

    [RequiresUnreferencedCode("Test-only path — DefaultJsonTypeInfoResolver pulls reflection.")]
    [RequiresDynamicCode("Test-only path — DefaultJsonTypeInfoResolver pulls reflection.")]
    private static JsonSerializerOptions CreateOptionsForTests() => new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = JsonTypeInfoResolver.Combine(
            SignalJsonContext.Default, new DefaultJsonTypeInfoResolver()),
    };
}
