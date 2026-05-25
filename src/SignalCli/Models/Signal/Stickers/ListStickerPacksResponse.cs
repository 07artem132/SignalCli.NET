using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using SignalCli.Serialization;

namespace SignalCli.Models.Signal.Stickers;

/// <summary>
/// Відповідь на <c>listStickerPacks</c> — flat JSON-масив <see cref="JsonStickerPack"/>-записів.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 4. Wrapper-record + custom converter per CLAUDE.md
/// "AOT readiness" pattern (як <see cref="Accounts.ListAccountsResponse"/>).
/// </remarks>
[PublicAPI]
[JsonConverter(typeof(ListStickerPacksResponseConverter))]
public sealed record ListStickerPacksResponse(IReadOnlyList<JsonStickerPack> Items) : IReadOnlyList<JsonStickerPack>
{
    /// <summary>Кількість sticker pack'ів.</summary>
    public int Count => Items.Count;

    /// <summary>Доступ за індексом.</summary>
    public JsonStickerPack this[int index] => Items[index];

    /// <inheritdoc/>
    public IEnumerator<JsonStickerPack> GetEnumerator() => Items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>Конвертер, що читає/пише <see cref="ListStickerPacksResponse"/> як плоский JSON-масив.</summary>
internal sealed class ListStickerPacksResponseConverter : JsonConverter<ListStickerPacksResponse>
{
    public override ListStickerPacksResponse Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Очікувався JSON-масив для ListStickerPacksResponse.");
        var list = JsonSerializer.Deserialize(ref reader, SignalJsonContext.Default.ListJsonStickerPack) ?? [];
        return new ListStickerPacksResponse(list);
    }

    public override void Write(Utf8JsonWriter writer, ListStickerPacksResponse value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, (List<JsonStickerPack>)value.Items, SignalJsonContext.Default.ListJsonStickerPack);
}
