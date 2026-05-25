using System.Text.Json;
using SignalCli.Models.Signal.Message;
using SignalCli.Serialization;

namespace SignalCli.Tests.Serialization;

/// <summary>
/// signal-cli-api-coverage Wave 1 (messaging-interactive): wire-shape pinning для 4-х нових
/// send-методів. Тести підтверджують kebab-case field names per research §0.5 (read from
/// signal-cli @ bda4e7fc), а не "правдоподібну" camelCase guess.
/// <para>
/// Тести парсять JSON у <see cref="JsonDocument"/> щоб уникнути sensitivity до STJ-encoder'у
/// (за замовчуванням <c>+</c> → <c>+</c>, що валідно на wire, але ламає raw-substring-asserts).
/// </para>
/// </summary>
public class MessagingInteractiveSerializationTests
{
    private static JsonDocument SerializeAndParse<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        var json = JsonSerializer.Serialize(value, typeInfo);
        return JsonDocument.Parse(json);
    }

    // ---- sendReaction ----

    [Fact]
    public void SendReactionParameters_SerializesKebabCaseFields()
    {
        var p = new SendReactionParameters(
            Account: "+380501234567",
            Recipients: ["+380509999999"],
            GroupIds: null,
            Usernames: null,
            NoteToSelf: false,
            NotifySelf: true,
            Emoji: "👍",
            TargetAuthor: "+380509999999",
            TargetTimestamp: 1717181920000L,
            Remove: false,
            Story: false);

        using var doc = SerializeAndParse(p, SignalJsonContext.Default.SendReactionParameters);
        var root = doc.RootElement;

        Assert.Equal("+380501234567", root.GetProperty("account").GetString());
        Assert.Equal("+380509999999", root.GetProperty("recipient")[0].GetString());
        Assert.False(root.GetProperty("note-to-self").GetBoolean());
        Assert.True(root.GetProperty("notify-self").GetBoolean());
        Assert.Equal("👍", root.GetProperty("emoji").GetString());
        Assert.Equal("+380509999999", root.GetProperty("target-author").GetString());
        Assert.Equal(1717181920000L, root.GetProperty("target-timestamp").GetInt64());
        Assert.False(root.GetProperty("remove").GetBoolean());
        Assert.False(root.GetProperty("story").GetBoolean());
    }

    [Fact]
    public void SendReactionParameters_Roundtrip_PreservesScalarFields()
    {
        var original = new SendReactionParameters(
            Account: "+1", Recipients: null, GroupIds: ["abc=="], Usernames: null,
            NoteToSelf: true, NotifySelf: false, Emoji: "❤", TargetAuthor: null,
            TargetTimestamp: 42, Remove: true, Story: true);

        var json = JsonSerializer.Serialize(original, SignalJsonContext.Default.SendReactionParameters);
        var back = JsonSerializer.Deserialize(json, SignalJsonContext.Default.SendReactionParameters)!;

        Assert.Equal(original.Account, back.Account);
        Assert.Equal(original.Emoji, back.Emoji);
        Assert.Equal(original.NoteToSelf, back.NoteToSelf);
        Assert.Equal(original.NotifySelf, back.NotifySelf);
        Assert.Equal(original.TargetTimestamp, back.TargetTimestamp);
        Assert.Equal(original.Remove, back.Remove);
        Assert.Equal(original.Story, back.Story);
        Assert.Null(back.Recipients);
        Assert.Equal(original.GroupIds!, back.GroupIds!);
    }

    // ---- sendReceipt ----

    [Fact]
    public void SendReceiptParameters_RecipientIsSingularString_NotArray()
    {
        // §F7: упор на singular recipient. Реверс-тест запобігає випадковому переходу на list.
        var p = new SendReceiptParameters(
            Account: "+1",
            Recipient: "+380501234567",
            TargetTimestamps: [1L, 2L, 3L],
            Type: "read");

        using var doc = SerializeAndParse(p, SignalJsonContext.Default.SendReceiptParameters);
        var root = doc.RootElement;

        // recipient — рядок, не масив
        Assert.Equal(JsonValueKind.String, root.GetProperty("recipient").ValueKind);
        Assert.Equal("+380501234567", root.GetProperty("recipient").GetString());
        var ts = root.GetProperty("target-timestamp");
        Assert.Equal(JsonValueKind.Array, ts.ValueKind);
        Assert.Equal(3, ts.GetArrayLength());
        Assert.Equal(1L, ts[0].GetInt64());
        Assert.Equal("read", root.GetProperty("type").GetString());
    }

    [Fact]
    public void SendReceiptParameters_TypeLowercaseOnWire()
    {
        // Per research: argparse .choices("read","viewed") — lowercase wire string, не Java-enum UPPER_SNAKE.
        var p = new SendReceiptParameters("+1", "+2", [1L], "viewed");
        using var doc = SerializeAndParse(p, SignalJsonContext.Default.SendReceiptParameters);
        Assert.Equal("viewed", doc.RootElement.GetProperty("type").GetString());
    }

    // ---- sendTyping ----

    [Fact]
    public void SendTypingParameters_StopIsBoolean_NotActionEnum()
    {
        // Per research: wire не має "action":"START|STOP" — boolean "stop" flag.
        var p = new SendTypingParameters("+1", ["+2"], null, Stop: true);
        using var doc = SerializeAndParse(p, SignalJsonContext.Default.SendTypingParameters);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("stop").GetBoolean());
        Assert.False(root.TryGetProperty("action", out _)); // ніякого action поля
        // group-id присутній як null (STJ серіалізує nullable IEnumerable як null, не omitting)
        Assert.Equal(JsonValueKind.Null, root.GetProperty("group-id").ValueKind);
    }

    [Fact]
    public void SendTypingParameters_Roundtrip_StartCase()
    {
        var original = new SendTypingParameters("+1", null, ["g1=="], Stop: false);
        var json = JsonSerializer.Serialize(original, SignalJsonContext.Default.SendTypingParameters);
        var back = JsonSerializer.Deserialize(json, SignalJsonContext.Default.SendTypingParameters)!;

        Assert.Equal(original.Account, back.Account);
        Assert.Equal(original.Stop, back.Stop);
        Assert.Null(back.Recipients);
        Assert.Equal(original.GroupIds!, back.GroupIds!);
    }

    // ---- remoteDelete ----

    [Fact]
    public void RemoteDeleteParameters_TargetTimestampIsSingleLong_NotList()
    {
        // Per research: remoteDelete.target-timestamp — single Long, не nargs("+") як у sendReceipt.
        var p = new RemoteDeleteParameters(
            Account: "+1", TargetTimestamp: 1717181920000L,
            GroupIds: null, Recipients: ["+2"], Usernames: null, NoteToSelf: false);

        using var doc = SerializeAndParse(p, SignalJsonContext.Default.RemoteDeleteParameters);
        var ts = doc.RootElement.GetProperty("target-timestamp");
        Assert.Equal(JsonValueKind.Number, ts.ValueKind);
        Assert.Equal(1717181920000L, ts.GetInt64());
    }

    [Fact]
    public void RemoteDeleteParameters_KebabCaseFields()
    {
        var p = new RemoteDeleteParameters("+1", 42L, ["g=="], ["+r"], ["u"], NoteToSelf: true);
        using var doc = SerializeAndParse(p, SignalJsonContext.Default.RemoteDeleteParameters);
        var root = doc.RootElement;

        Assert.Equal("+1", root.GetProperty("account").GetString());
        Assert.Equal(42L, root.GetProperty("target-timestamp").GetInt64());
        Assert.Equal("g==", root.GetProperty("group-id")[0].GetString());
        Assert.Equal("+r", root.GetProperty("recipient")[0].GetString());
        Assert.Equal("u", root.GetProperty("username")[0].GetString());
        Assert.True(root.GetProperty("note-to-self").GetBoolean());
    }
}
