using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Відповідь на запит списку облікових записів Signal.
/// </summary>
/// <remarks>
/// post-modernize-tuning §4.20 (audit N10): wrapper-record над <see cref="IReadOnlyList{T}"/>.
/// Раніше тип успадковував <c>List&lt;Account&gt;</c>, що давало консумеру доступ до
/// деструктивних мутаторів (<c>.Add</c>, <c>.Clear</c>, <c>.Sort</c>) на даних, які
/// прийшли від сервера. Тепер це immutable-projection — мутувати можна тільки через
/// явну побудову нового <see cref="ListAccountsResponse"/>. Wire-shape JSON-масиву
/// зберігається через <see cref="ListAccountsResponseConverter"/>.
/// </remarks>
[PublicAPI]
[JsonConverter(typeof(ListAccountsResponseConverter))]
public sealed record ListAccountsResponse(IReadOnlyList<Account> Items) : IReadOnlyList<Account>
{
    /// <summary>Кількість облікових записів у відповіді.</summary>
    public int Count => Items.Count;

    /// <summary>Доступ до облікового запису за індексом.</summary>
    public Account this[int index] => Items[index];

    /// <inheritdoc/>
    public IEnumerator<Account> GetEnumerator() => Items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Конвертер, що читає/пише <see cref="ListAccountsResponse"/> як плоский JSON-масив
/// (а не як об'єкт із полем <c>Items</c>). Делегує до built-in <c>List&lt;Account&gt;</c>
/// converter'а — джерело істини для source-gen контексту лишається спільним.
/// </summary>
internal sealed class ListAccountsResponseConverter : JsonConverter<ListAccountsResponse>
{
    public override ListAccountsResponse Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Очікувався JSON-масив для ListAccountsResponse.");
        var list = JsonSerializer.Deserialize<List<Account>>(ref reader, options)
                   ?? [];
        return new ListAccountsResponse(list);
    }

    public override void Write(Utf8JsonWriter writer, ListAccountsResponse value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, (List<Account>)value.Items, options);
}

/// <summary>
/// Інформація про обліковий запис Signal.
/// </summary>
/// <remarks>
/// Містить основні ідентифікаційні дані облікового запису.
/// </remarks>
/// <param name="Number">Номер телефону облікового запису.</param>
[PublicAPI]
public record Account([property: JsonPropertyName("number")] string Number);
