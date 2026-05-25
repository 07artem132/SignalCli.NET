using System.Text.Json;
using SignalCli.Models.Signal.Accounts;
using SignalCli.Serialization;

namespace SignalCli.Tests.Serialization;

/// <summary>
/// signal-cli-api-coverage Wave 6 (account-lifecycle): wire-shape pinning.
/// Focus на §F3 (numberSharing as bool) + §F4 (voice as bool).
/// </summary>
public class AccountLifecycleSerializationTests
{
    [Fact]
    public void UpdateAccountParameters_NumberSharing_IsBool_NotEnum()
    {
        // §F3: numberSharing wire — bool?, не enum string. Upstream argparse type(Boolean.class).
        var p = new UpdateAccountParameters(
            Account: "+1",
            DeviceName: null,
            UnrestrictedUnidentifiedSender: null,
            DiscoverableByNumber: null,
            NumberSharing: true,
            Username: null,
            DeleteUsername: false);
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.UpdateAccountParameters);
        using var doc = JsonDocument.Parse(json);
        var ns = doc.RootElement.GetProperty("numberSharing");
        Assert.Equal(JsonValueKind.True, ns.ValueKind); // bool, не string "EVERYBODY"
    }

    [Fact]
    public void UpdateAccountResponse_BothFields_AreNullable()
    {
        // Response — обидва поля nullable бо attribute-only updates повертають {}.
        const string emptyJson = "{}";
        var resp = JsonSerializer.Deserialize(emptyJson, SignalJsonContext.Default.UpdateAccountResponse)!;
        Assert.Null(resp.Username);
        Assert.Null(resp.UsernameLink);
    }

    [Fact]
    public void UpdateAccountResponse_UsernameSet_DeserializesCorrectly()
    {
        const string json = """{"username":"alice.42","usernameLink":"https://signal.me/#alice.42-token"}""";
        var resp = JsonSerializer.Deserialize(json, SignalJsonContext.Default.UpdateAccountResponse)!;
        Assert.Equal("alice.42", resp.Username);
        Assert.Contains("signal.me", resp.UsernameLink);
    }

    [Fact]
    public void StartChangeNumberParameters_Voice_IsBool()
    {
        // §F4: voice — bool на wire, default false. Argparse `-v`/`--voice`.
        var p = new StartChangeNumberParameters("+1", "+2", Voice: true, Captcha: null);
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.StartChangeNumberParameters);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.True, doc.RootElement.GetProperty("voice").ValueKind);
    }

    [Fact]
    public void StartChangeNumberParameters_HasNewNumberAsNumberField()
    {
        // Wire-field — "number" (NEW), не "newNumber". OLD номер у envelope (account).
        var p = new StartChangeNumberParameters("+oldNumber", "+newNumber", Voice: false, Captcha: null);
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.StartChangeNumberParameters);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("+oldNumber", doc.RootElement.GetProperty("account").GetString());
        Assert.Equal("+newNumber", doc.RootElement.GetProperty("number").GetString());
    }

    [Fact]
    public void UnregisterParameters_DeleteAccountIsBool()
    {
        var p = new UnregisterParameters("+1", DeleteAccount: true);
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.UnregisterParameters);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("deleteAccount").GetBoolean());
    }

    [Fact]
    public void DeleteLocalAccountDataParameters_HasIgnoreRegisteredField()
    {
        var p = new DeleteLocalAccountDataParameters("+1", IgnoreRegistered: true);
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.DeleteLocalAccountDataParameters);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("ignoreRegistered").GetBoolean());
    }

    [Fact]
    public void UpdateConfigurationParameters_AllFieldsNullableBool()
    {
        // 4 nullable bool — null = "не міняти".
        var p = new UpdateConfigurationParameters("+1",
            ReadReceipts: true, UnidentifiedDeliveryIndicators: null,
            TypingIndicators: false, LinkPreviews: null);
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.UpdateConfigurationParameters);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("readReceipts").GetBoolean());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("unidentifiedDeliveryIndicators").ValueKind);
        Assert.False(doc.RootElement.GetProperty("typingIndicators").GetBoolean());
    }

    [Fact]
    public void SetPinParameters_HasPinField()
    {
        var p = new SetPinParameters("+1", "1234");
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.SetPinParameters);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("1234", doc.RootElement.GetProperty("pin").GetString());
    }

    [Fact]
    public void RemovePinParameters_HasOnlyAccountField()
    {
        var p = new RemovePinParameters("+1");
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.RemovePinParameters);
        using var doc = JsonDocument.Parse(json);
        // Лише account; no other fields.
        var props = doc.RootElement.EnumerateObject().Select(x => x.Name).ToArray();
        Assert.Single(props);
        Assert.Equal("account", props[0]);
    }
}
