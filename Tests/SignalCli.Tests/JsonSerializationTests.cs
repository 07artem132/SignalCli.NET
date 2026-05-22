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
    private static readonly JsonSerializerOptions Opt = SignalJson.Options;

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
}
