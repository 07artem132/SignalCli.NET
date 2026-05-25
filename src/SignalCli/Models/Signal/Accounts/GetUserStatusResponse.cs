using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using SignalCli.Serialization;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Відповідь на <c>getUserStatus</c> — flat JSON-масив <see cref="JsonUserStatus"/>-записів.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 8. Wrapper-record + custom converter per CLAUDE.md
/// "AOT readiness" pattern (як <see cref="ListAccountsResponse"/>).
/// </remarks>
[PublicAPI]
[JsonConverter(typeof(GetUserStatusResponseConverter))]
public sealed record GetUserStatusResponse(IReadOnlyList<JsonUserStatus> Items) : IReadOnlyList<JsonUserStatus>
{
    /// <summary>Кількість записів.</summary>
    public int Count => Items.Count;

    /// <summary>Доступ за індексом.</summary>
    public JsonUserStatus this[int index] => Items[index];

    /// <inheritdoc/>
    public IEnumerator<JsonUserStatus> GetEnumerator() => Items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>Конвертер, що читає/пише <see cref="GetUserStatusResponse"/> як плоский JSON-масив.</summary>
internal sealed class GetUserStatusResponseConverter : JsonConverter<GetUserStatusResponse>
{
    public override GetUserStatusResponse Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Очікувався JSON-масив для GetUserStatusResponse.");
        var list = JsonSerializer.Deserialize(ref reader, SignalJsonContext.Default.ListJsonUserStatus) ?? [];
        return new GetUserStatusResponse(list);
    }

    public override void Write(Utf8JsonWriter writer, GetUserStatusResponse value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, (List<JsonUserStatus>)value.Items, SignalJsonContext.Default.ListJsonUserStatus);
}
