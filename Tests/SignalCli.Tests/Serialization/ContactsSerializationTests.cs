using System.Text.Json;
using SignalCli.Models.Signal.Contacts;
using SignalCli.Serialization;

namespace SignalCli.Tests.Serialization;

/// <summary>
/// signal-cli-api-coverage Wave 3 (contacts-identity): wire-shape pinning. Парсимо через
/// <see cref="JsonDocument"/> щоб уникнути sensitivity до STJ-encoder defaults.
/// </summary>
public class ContactsSerializationTests
{
    private static JsonDocument SerializeAndParse<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        var json = JsonSerializer.Serialize(value, typeInfo);
        return JsonDocument.Parse(json);
    }

    // ---- ListContactsResponse: flat array wrapper-record ----

    [Fact]
    public void ListContactsResponse_DeserializesFromFlatArray()
    {
        const string json = """
        [
          {"number":"+380501234567","uuid":"abc-uuid","username":null,"name":"Test User",
           "givenName":"Test","familyName":"User","nickName":null,"nickGivenName":null,
           "nickFamilyName":null,"note":null,"color":null,"isArchived":false,"isBlocked":false,
           "isHidden":false,"messageExpirationTime":0,"profileSharing":true,"unregistered":false,
           "profile":null}
        ]
        """;
        var resp = JsonSerializer.Deserialize(json, SignalJsonContext.Default.ListContactsResponse)!;
        Assert.Single(resp.Items);
        Assert.Equal("+380501234567", resp[0].Number);
        Assert.Equal("Test User", resp[0].Name);
        // §internal absent (request internal=false) — field має бути null.
        Assert.Null(resp[0].Internal);
    }

    [Fact]
    public void JsonContact_With_Internal_Deserializes_AllInternalFields()
    {
        // §request internal=true → internal sub-object present.
        const string json = """
        {"number":"+1","uuid":"u","username":null,"name":"x","givenName":null,"familyName":null,
         "nickName":null,"nickGivenName":null,"nickFamilyName":null,"note":null,"color":null,
         "isArchived":false,"isBlocked":false,"isHidden":false,"messageExpirationTime":0,
         "profileSharing":false,"unregistered":false,"profile":null,
         "internal":{"capabilities":["storage","storageServiceEncryptionV2Capability"],
                     "unidentifiedAccessMode":"ENABLED","sharesPhoneNumber":true,
                     "discoverableByPhonenumber":false}}
        """;
        var c = JsonSerializer.Deserialize(json, SignalJsonContext.Default.JsonContact)!;
        Assert.NotNull(c.Internal);
        Assert.Equal(2, c.Internal.Capabilities!.Count);
        // Capabilities — Java enum identifiers у lowercase/mixedCase, не UPPER_SNAKE (per research).
        Assert.Contains("storage", c.Internal.Capabilities);
        Assert.Equal("ENABLED", c.Internal.UnidentifiedAccessMode);
        Assert.True(c.Internal.SharesPhoneNumber);
        // Upstream typo: "discoverableByPhonenumber" (lowercase "n").
        Assert.False(c.Internal.DiscoverableByPhonenumber);
    }

    // ---- ListIdentitiesResponse + TrustLevel UPPER_SNAKE wire ----

    [Fact]
    public void ListIdentitiesResponse_DeserializesUpperSnakeTrustLevel()
    {
        const string json = """
        [
          {"number":"+1","uuid":null,"fingerprint":"deadbeef","safetyNumber":"123 456",
           "scannableSafetyNumber":null,"trustLevel":"TRUSTED_VERIFIED","addedTimestamp":1717181920000}
        ]
        """;
        var resp = JsonSerializer.Deserialize(json, SignalJsonContext.Default.ListIdentitiesResponse)!;
        Assert.Single(resp.Items);
        // Wire — UPPER_SNAKE; ми приймаємо як string (не enum) у DTO.
        Assert.Equal("TRUSTED_VERIFIED", resp[0].TrustLevel);
        Assert.Equal("deadbeef", resp[0].Fingerprint);
    }

    // ---- TrustParameters: XOR enforced on wire через 2 окремих API ----

    [Fact]
    public void TrustParameters_AllKnownKeys_HasCorrectFields()
    {
        var p = new TrustParameters("+1", "+2", TrustAllKnownKeys: true, VerifiedSafetyNumber: null);
        using var doc = SerializeAndParse(p, SignalJsonContext.Default.TrustParameters);
        Assert.True(doc.RootElement.GetProperty("trustAllKnownKeys").GetBoolean());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("verifiedSafetyNumber").ValueKind);
    }

    [Fact]
    public void TrustParameters_Verified_HasCorrectFields()
    {
        var p = new TrustParameters("+1", "+2", TrustAllKnownKeys: false, VerifiedSafetyNumber: "12345 67890");
        using var doc = SerializeAndParse(p, SignalJsonContext.Default.TrustParameters);
        Assert.False(doc.RootElement.GetProperty("trustAllKnownKeys").GetBoolean());
        Assert.Equal("12345 67890", doc.RootElement.GetProperty("verifiedSafetyNumber").GetString());
    }

    // ---- RemoveContactParameters: hide/forget XOR ----

    [Fact]
    public void RemoveContactParameters_HasBothBooleansOnWire()
    {
        var p = new RemoveContactParameters("+1", "+2", Hide: true, Forget: false);
        using var doc = SerializeAndParse(p, SignalJsonContext.Default.RemoveContactParameters);
        Assert.True(doc.RootElement.GetProperty("hide").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("forget").GetBoolean());
    }

    // ---- UpdateProfileParameters ----

    [Fact]
    public void UpdateProfileParameters_RemoveAvatar_HasBoolean()
    {
        var p = new UpdateProfileParameters(
            Account: "+1",
            GivenName: null, FamilyName: null, About: null, AboutEmoji: null,
            MobileCoinAddress: null, Avatar: null, RemoveAvatar: true);
        using var doc = SerializeAndParse(p, SignalJsonContext.Default.UpdateProfileParameters);
        Assert.True(doc.RootElement.GetProperty("removeAvatar").GetBoolean());
    }

    // ---- Block/Unblock: однакова shape, різні типи ----

    [Fact]
    public void BlockParameters_HasRecipientAndGroupIdFields()
    {
        var p = new BlockParameters("+1", ["+2"], ["g=="]);
        using var doc = SerializeAndParse(p, SignalJsonContext.Default.BlockParameters);
        Assert.Equal("+2", doc.RootElement.GetProperty("recipient")[0].GetString());
        Assert.Equal("g==", doc.RootElement.GetProperty("groupId")[0].GetString());
    }

    [Fact]
    public void UnblockParameters_HasSameShapeAsBlock()
    {
        var p = new UnblockParameters("+1", ["+2"], null);
        using var doc = SerializeAndParse(p, SignalJsonContext.Default.UnblockParameters);
        Assert.Equal("+2", doc.RootElement.GetProperty("recipient")[0].GetString());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("groupId").ValueKind);
    }
}
