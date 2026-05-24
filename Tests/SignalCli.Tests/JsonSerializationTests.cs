using System.Text.Json;
using SignalCli.Models.Rpc;
using SignalCli.Models.Signal;
using SignalCli.Models.SignalCli;
using SignalCli.Serialization;

namespace SignalCli.Tests;

/// <summary>
/// Round-trip тести протоколу signal-cli на System.Text.Json (захист міграції з Newtonsoft).
/// </summary>
public class JsonSerializationTests
{
    // post-modernize-tuning §6.10: тест-only OptionsForTests із reflection-fallback —
    // дозволяє анонімні типи (`new { account = ... }`) у round-trip тестах. Production-
    // код не може цього робити (Options тепер source-gen-only). Властивість анотована
    // [RequiresUnreferencedCode]/[RequiresDynamicCode]; для тестів пригнічуємо.
#pragma warning disable IL2026, IL3050
    private static readonly JsonSerializerOptions Opt = SignalJson.OptionsForTests;
#pragma warning restore IL2026, IL3050

    [Fact]
    public void Request_SerializesCamelCase_AndOmitsNulls()
    {
        var paramsElement = JsonSerializer.SerializeToElement(
            new { account = "+380501234567", message = (string?)null }, Opt);
        var req = new JsonRpcRequest("send", paramsElement, "1");

        var json = JsonSerializer.Serialize(req, Opt);

        Assert.Contains("\"jsonrpc\":\"2.0\"", json);
        Assert.Contains("\"method\":\"send\"", json);
        Assert.Contains("\"id\":\"1\"", json);
        Assert.Contains("\"account\":\"+380501234567\"", json);
        // null-поля не пишемо (DefaultIgnoreCondition.WhenWritingNull)
        Assert.DoesNotContain("message", json);
    }

    [Fact]
    public void Response_Deserializes_AndResultElementIsUsable()
    {
        const string json = """{"jsonrpc":"2.0","result":{"version":"0.14.3"},"id":"42"}""";

        var resp = JsonSerializer.Deserialize<JsonRpcResponse>(json, Opt)!;

        Assert.Equal("42", resp.Id);
        Assert.Null(resp.Error);
        var version = resp.Result.Deserialize<VersionResponse>(Opt)!;
        Assert.Equal("0.14.3", version.Version);
    }

    [Fact]
    public void Error_Deserializes()
    {
        const string json = """{"jsonrpc":"2.0","error":{"code":-32602,"message":"bad"},"id":"1"}""";

        var resp = JsonSerializer.Deserialize<JsonRpcResponse>(json, Opt)!;

        Assert.NotNull(resp.Error);
        Assert.Equal(-32602, resp.Error!.Code);
        Assert.Equal("bad", resp.Error.Message);
    }

    [Fact]
    public void Envelope_WithCaptionAndAttachment_BothPreserved()
    {
        const string json = """
        {"source":"+1","sourceNumber":"+1","sourceUuid":"u","sourceName":"n","sourceDevice":1,
         "timestamp":1,"serverReceivedTimestamp":1,"serverDeliveredTimestamp":1,
         "dataMessage":{"timestamp":1,"message":"caption",
           "attachments":[{"contentType":"image/png","filename":"p.png","id":"i","size":10}]}}
        """;

        var env = JsonSerializer.Deserialize<JsonMessageEnvelope>(json, Opt)!;

        Assert.NotNull(env.DataMessage);
        Assert.Equal("caption", env.DataMessage!.Message);
        Assert.NotNull(env.DataMessage.Attachments);
        Assert.Single(env.DataMessage.Attachments!);
        Assert.Equal("p.png", env.DataMessage.Attachments![0].Filename);
    }

    [Fact]
    public void SyncMessageType_ParsedFromStringName()
    {
        const string json = """{"type":"CONTACTS_SYNC"}""";

        var sync = JsonSerializer.Deserialize<JsonSyncMessage>(json, Opt)!;

        Assert.Equal(JsonSyncMessageType.CONTACTS_SYNC, sync.Type);
    }

    [Fact]
    public void CaseInsensitive_DeserializationWorks()
    {
        // signal-cli шле camelCase; перевіряємо, що нечутливість до регістру збережено
        const string json = """{"JSONRPC":"2.0","ID":"7","result":{"version":"x"}}""";

        var resp = JsonSerializer.Deserialize<JsonRpcResponse>(json, Opt)!;

        Assert.Equal("2.0", resp.JsonRpc);
        Assert.Equal("7", resp.Id);
    }

    /// <summary>
    /// post-modernize-tuning §4.20 (audit N10): після конвертації <see cref="Models.Signal.Accounts.ListAccountsResponse"/>
    /// у wrapper-record над <c>IReadOnlyList&lt;Account&gt;</c> wire-формат МАЄ лишитися плоским JSON-масивом.
    /// Без custom-конвертера буде <c>{"Items":[...]}</c> — це зламає wire-compatibility з signal-cli.
    /// </summary>
    [Fact]
    public void ListAccountsResponse_RoundTrip_PreservesFlatJsonArrayShape()
    {
        const string wireJson = """[{"number":"+380501234567"},{"number":"+380509999999"}]""";

        var response = JsonSerializer.Deserialize<Models.Signal.Accounts.ListAccountsResponse>(wireJson, Opt)!;

        Assert.Equal(2, response.Count);
        Assert.Equal("+380501234567", response[0].Number);
        Assert.Equal("+380509999999", response[1].Number);

        var roundtripped = JsonSerializer.Serialize(response, Opt);
        // Плоский масив, а не {"Items":[...]} — критичне для wire-compat.
        Assert.StartsWith("[", roundtripped);
        Assert.EndsWith("]", roundtripped);
        Assert.DoesNotContain("Items", roundtripped);
        Assert.Contains("\"number\":\"+380501234567\"", roundtripped);
    }

    /// <summary>
    /// audit-followup-2026 (json-hardening): .NET 10 `AllowDuplicateProperties=false` —
    /// duplicate keys у JSON-відповіді відкидаються з JsonException замість тихого last-wins.
    /// </summary>
    /// <summary>
    /// audit-followup-2026 §6.i (edge-case-coverage): JSON-RPC 2.0 spec забороняє і result,
    /// і error разом, але defensive — якщо signal-cli раптом violate'нув, пінуємо що ми
    /// віддаємо перевагу error. JsonRpcException — ловиться JsonRpcClient.InvokeMethodAsync
    /// раніше за typed-result deserialization (див. JsonRpcClient.cs `if (response.Error != null) throw`).
    /// На рівні DTO — обидва поля просто заповнюються.
    /// </summary>
    [Fact]
    public void Response_WithBothResultAndError_DeserializesBothFields()
    {
        const string json = """{"jsonrpc":"2.0","result":{"version":"x"},"error":{"code":-1,"message":"E"},"id":"1"}""";
        var resp = JsonSerializer.Deserialize<JsonRpcResponse>(json, Opt)!;

        // DTO desеріалізує обидва — error.Code = -1 і result.Version = "x" одночасно присутні.
        // JsonRpcClient.InvokeMethodAsync робить розрізнення (error wins) на business-level.
        Assert.NotNull(resp.Error);
        Assert.Equal(-1, resp.Error!.Code);
        Assert.Equal("E", resp.Error.Message);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, resp.Result.ValueKind);
    }

    [Fact]
    public void DuplicateProperty_FailsDeserialization()
    {
        // {"jsonrpc":"2.0","jsonrpc":"X",...} — два рази "jsonrpc". JSON-RPC 2.0 SHALL NOT
        // дублювати ключі; .NET 10 за default'ом мовчки бере "last wins", але наш hardening
        // вимикає це і фолтить десеріалізацію.
        const string duplicateJson = """{"jsonrpc":"2.0","jsonrpc":"X","id":"1","result":{"version":"0.14.3"}}""";

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<JsonRpcResponse>(duplicateJson, Opt));
    }

    /// <summary>§4.20: те саме для <see cref="Models.Signal.Groups.ListGroupsResponse"/>.</summary>
    [Fact]
    public void ListGroupsResponse_RoundTrip_PreservesFlatJsonArrayShape()
    {
        const string wireJson =
            """
            [{"id":"GROUP-1","name":"Test","description":null,"isMember":true,"isBlocked":false,
              "messageExpirationTime":0,"members":[],"pendingMembers":[],"requestingMembers":[],
              "admins":[],"banned":[],"permissionAddMember":"EVERY_MEMBER",
              "permissionEditDetails":"EVERY_MEMBER","permissionSendMessage":"EVERY_MEMBER",
              "groupInviteLink":null}]
            """;

        var response = JsonSerializer.Deserialize<Models.Signal.Groups.ListGroupsResponse>(wireJson, Opt)!;

        Assert.Single(response);
        Assert.Equal("GROUP-1", response[0].Id);

        var roundtripped = JsonSerializer.Serialize(response, Opt);
        Assert.StartsWith("[", roundtripped);
        Assert.DoesNotContain("Items", roundtripped);
    }
}
