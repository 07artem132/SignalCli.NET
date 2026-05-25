using System.Text.Json;
using SignalCli.Models.Signal.Accounts;
using SignalCli.Serialization;

namespace SignalCli.Tests.Serialization;

/// <summary>
/// signal-cli-api-coverage Wave 8 (utility-rpc): wire-shape pinning.
/// </summary>
public class UtilityRpcSerializationTests
{
    [Fact]
    public void GetUserStatusResponse_DeserializesFlatArray()
    {
        const string json = """
        [
          {"recipient":"+380501234567","number":"+380501234567","uuid":"abc-uuid","isRegistered":true},
          {"recipient":"alice.42","username":"alice.42","uuid":"xyz","isRegistered":true},
          {"recipient":"+1invalid","uuid":null,"isRegistered":false}
        ]
        """;
        var resp = JsonSerializer.Deserialize(json, SignalJsonContext.Default.GetUserStatusResponse)!;
        Assert.Equal(3, resp.Count);
        // §F5 mix: phone row + username row + unregistered row.
        Assert.Equal("+380501234567", resp[0].Number);
        Assert.Null(resp[0].Username);
        Assert.True(resp[0].IsRegistered);
        Assert.Equal("alice.42", resp[1].Username);
        Assert.Null(resp[1].Number);
        Assert.False(resp[2].IsRegistered);
        Assert.Null(resp[2].Uuid);
    }

    [Fact]
    public void GetUserStatusResponse_EmptyArray_Works()
    {
        // §F5: empty input → empty array (not error).
        const string json = "[]";
        var resp = JsonSerializer.Deserialize(json, SignalJsonContext.Default.GetUserStatusResponse)!;
        Assert.Empty(resp);
    }

    [Fact]
    public void GetUserStatusParameters_HasBothListFields()
    {
        var p = new GetUserStatusParameters("+1", ["+2"], ["alice"]);
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.GetUserStatusParameters);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("+2", doc.RootElement.GetProperty("recipient")[0].GetString());
        Assert.Equal("alice", doc.RootElement.GetProperty("username")[0].GetString());
    }

    [Fact]
    public void SubmitRateLimitChallengeParameters_HasAllThreeFields()
    {
        var p = new SubmitRateLimitChallengeParameters("+1", "challenge-token", "captcha-token");
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.SubmitRateLimitChallengeParameters);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("+1", doc.RootElement.GetProperty("account").GetString());
        Assert.Equal("challenge-token", doc.RootElement.GetProperty("challenge").GetString());
        Assert.Equal("captcha-token", doc.RootElement.GetProperty("captcha").GetString());
    }

    [Fact]
    public void SendContactsParameters_OnlyAccountField()
    {
        var p = new SendContactsParameters("+1");
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.SendContactsParameters);
        using var doc = JsonDocument.Parse(json);
        var props = doc.RootElement.EnumerateObject().Select(x => x.Name).ToArray();
        Assert.Single(props);
        Assert.Equal("account", props[0]);
    }
}
