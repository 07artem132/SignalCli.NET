using System.Text.Json;
using SignalCli.Models.Signal.Resources;
using SignalCli.Models.Signal.Stickers;
using SignalCli.Serialization;

namespace SignalCli.Tests.Serialization;

/// <summary>
/// signal-cli-api-coverage Wave 4 (sticker-packs + binary-resource-fetch): wire-shape pinning.
/// </summary>
public class StickersAndResourcesSerializationTests
{
    // ---- Stickers ----

    [Fact]
    public void UploadStickerPackResponse_DeserializesFromFlatUrlObject()
    {
        const string json = """{"url":"https://signal.art/addstickers/#pack_id=abc&pack_key=def"}""";
        var resp = JsonSerializer.Deserialize(json, SignalJsonContext.Default.UploadStickerPackResponse)!;
        Assert.Contains("signal.art", resp.Url);
    }

    [Fact]
    public void ListStickerPacksResponse_DeserializesFromFlatArray()
    {
        const string json = """
        [
          {"packId":"abcdef0123456789abcdef0123456789","url":"https://signal.art/addstickers/#pack_id=abcdef0123456789abcdef0123456789&pack_key=ff",
           "installed":true,"title":"My Pack","author":"Author","cover":null,"stickers":[]}
        ]
        """;
        var resp = JsonSerializer.Deserialize(json, SignalJsonContext.Default.ListStickerPacksResponse)!;
        Assert.Single(resp.Items);
        Assert.Equal("My Pack", resp[0].Title);
        Assert.True(resp[0].Installed);
        Assert.Empty(resp[0].Stickers); // pack без fetched manifest = empty []
        Assert.Null(resp[0].Cover);
    }

    [Fact]
    public void JsonStickerPack_TitleAuthor_CanBeEmptyStrings()
    {
        // Quirk: upstream StickerPack ctor default'ить title/author на "" (не null).
        const string json = """{"packId":"a","url":"u","installed":false,"title":"","author":"","cover":null,"stickers":[]}""";
        var pack = JsonSerializer.Deserialize(json, SignalJsonContext.Default.JsonStickerPack)!;
        Assert.Equal("", pack.Title);
        Assert.Equal("", pack.Author);
    }

    [Fact]
    public void AddStickerPackParameters_HasUriListField()
    {
        var p = new AddStickerPackParameters("+1", ["https://signal.art/#1", "https://signal.art/#2"]);
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.AddStickerPackParameters);
        using var doc = JsonDocument.Parse(json);
        var uri = doc.RootElement.GetProperty("uri");
        Assert.Equal(2, uri.GetArrayLength());
    }

    // ---- Resources ----

    [Fact]
    public void GetAvatarParameters_ContactOnly_HasOnlyContactSetOnWire()
    {
        var p = new GetAvatarParameters("+1", Contact: "+2", Profile: null, GroupId: null);
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.GetAvatarParameters);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("+2", doc.RootElement.GetProperty("contact").GetString());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("profile").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("groupId").ValueKind);
    }

    [Fact]
    public void JsonAttachmentData_HasDataField()
    {
        var d = new JsonAttachmentData("dGVzdA==");
        var json = JsonSerializer.Serialize(d, SignalJsonContext.Default.JsonAttachmentData);
        Assert.Contains("\"data\":\"dGVzdA==\"", json);
    }

    [Fact]
    public void GetStickerParameters_HasIntStickerId()
    {
        var p = new GetStickerParameters("+1", "abcdef0123456789abcdef0123456789", 42);
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.GetStickerParameters);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(42, doc.RootElement.GetProperty("stickerId").GetInt32());
        Assert.Equal("abcdef0123456789abcdef0123456789", doc.RootElement.GetProperty("packId").GetString());
    }
}
