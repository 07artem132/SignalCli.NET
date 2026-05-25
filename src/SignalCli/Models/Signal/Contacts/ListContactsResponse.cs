using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using SignalCli.Serialization;

namespace SignalCli.Models.Signal.Contacts;

/// <summary>
/// Відповідь на <c>listContacts</c> — flat JSON-масив <see cref="JsonContact"/>-записів.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 3. Wrapper-record + custom converter per CLAUDE.md
/// "AOT readiness" pattern (як <see cref="Accounts.ListAccountsResponse"/>).
/// </remarks>
[PublicAPI]
[JsonConverter(typeof(ListContactsResponseConverter))]
public sealed record ListContactsResponse(IReadOnlyList<JsonContact> Items) : IReadOnlyList<JsonContact>
{
    /// <summary>Кількість контактів.</summary>
    public int Count => Items.Count;

    /// <summary>Доступ за індексом.</summary>
    public JsonContact this[int index] => Items[index];

    /// <inheritdoc/>
    public IEnumerator<JsonContact> GetEnumerator() => Items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>Конвертер, що читає/пише <see cref="ListContactsResponse"/> як плоский JSON-масив.</summary>
internal sealed class ListContactsResponseConverter : JsonConverter<ListContactsResponse>
{
    public override ListContactsResponse Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Очікувався JSON-масив для ListContactsResponse.");
        var list = JsonSerializer.Deserialize(ref reader, SignalJsonContext.Default.ListJsonContact) ?? [];
        return new ListContactsResponse(list);
    }

    public override void Write(Utf8JsonWriter writer, ListContactsResponse value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, (List<JsonContact>)value.Items, SignalJsonContext.Default.ListJsonContact);
}
