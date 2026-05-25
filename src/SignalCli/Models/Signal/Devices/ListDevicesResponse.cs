using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using SignalCli.Serialization;

namespace SignalCli.Models.Signal.Devices;

/// <summary>
/// Відповідь на <c>listDevices</c> — flat JSON-масив <see cref="Device"/>-записів.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 5. Wrapper-record + custom converter per CLAUDE.md
/// "AOT readiness" pattern (як <see cref="Accounts.ListAccountsResponse"/>).
/// </remarks>
[PublicAPI]
[JsonConverter(typeof(ListDevicesResponseConverter))]
public sealed record ListDevicesResponse(IReadOnlyList<Device> Items) : IReadOnlyList<Device>
{
    /// <summary>Кількість linked devices.</summary>
    public int Count => Items.Count;

    /// <summary>Доступ за індексом.</summary>
    public Device this[int index] => Items[index];

    /// <inheritdoc/>
    public IEnumerator<Device> GetEnumerator() => Items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>Конвертер, що читає/пише <see cref="ListDevicesResponse"/> як плоский JSON-масив.</summary>
internal sealed class ListDevicesResponseConverter : JsonConverter<ListDevicesResponse>
{
    public override ListDevicesResponse Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Очікувався JSON-масив для ListDevicesResponse.");
        var list = JsonSerializer.Deserialize(ref reader, SignalJsonContext.Default.ListDevice) ?? [];
        return new ListDevicesResponse(list);
    }

    public override void Write(Utf8JsonWriter writer, ListDevicesResponse value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, (List<Device>)value.Items, SignalJsonContext.Default.ListDevice);
}
