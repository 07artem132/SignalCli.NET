using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using SignalCli.Serialization;

namespace SignalCli.Models.Signal.Contacts;

/// <summary>
/// Відповідь на <c>listIdentities</c> — flat JSON-масив <see cref="JsonIdentity"/>-записів.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 3. Wrapper-record + custom converter per CLAUDE.md
/// "AOT readiness" pattern.
/// </remarks>
[PublicAPI]
[JsonConverter(typeof(ListIdentitiesResponseConverter))]
public sealed record ListIdentitiesResponse(IReadOnlyList<JsonIdentity> Items) : IReadOnlyList<JsonIdentity>
{
    /// <summary>Кількість identity-записів.</summary>
    public int Count => Items.Count;

    /// <summary>Доступ за індексом.</summary>
    public JsonIdentity this[int index] => Items[index];

    /// <inheritdoc/>
    public IEnumerator<JsonIdentity> GetEnumerator() => Items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>Конвертер, що читає/пише <see cref="ListIdentitiesResponse"/> як плоский JSON-масив.</summary>
internal sealed class ListIdentitiesResponseConverter : JsonConverter<ListIdentitiesResponse>
{
    public override ListIdentitiesResponse Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Очікувався JSON-масив для ListIdentitiesResponse.");
        var list = JsonSerializer.Deserialize(ref reader, SignalJsonContext.Default.ListJsonIdentity) ?? [];
        return new ListIdentitiesResponse(list);
    }

    public override void Write(Utf8JsonWriter writer, ListIdentitiesResponse value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, (List<JsonIdentity>)value.Items, SignalJsonContext.Default.ListJsonIdentity);
}
