using System.Text.Json;
using SignalCli.Models.Signal.Message;
using SignalCli.Serialization;

namespace SignalCli.Tests.Serialization;

/// <summary>
/// signal-cli-api-coverage Wave 7a (polls + messaging-power-user): wire-shape pinning.
/// </summary>
public class PollsAndPowerUserSerializationTests
{
    [Fact]
    public void SendPollCreateParameters_NoMulti_IsPolarityInverted()
    {
        // §F21: AllowMultipleVotes=true → noMulti=false on wire.
        var p = new SendPollCreateParameters("+1", null, null, null, false, false, "Q?", NoMulti: false, ["A", "B"]);
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.SendPollCreateParameters);
        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("noMulti").GetBoolean());
        Assert.Equal("Q?", doc.RootElement.GetProperty("question").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("option").GetArrayLength());
    }

    [Fact]
    public void SendPollVoteParameters_HasZeroBasedOptionIndexes()
    {
        // §F22: zero-based int indexes.
        var p = new SendPollVoteParameters("+1", ["+2"], null, null, false, false,
            PollAuthor: null, PollTimestamp: 1717L, OptionIndexes: [0, 2], VoteCount: 1);
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.SendPollVoteParameters);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("option")[0].GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("option")[1].GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("voteCount").GetInt32());
    }

    [Fact]
    public void SendPinMessageParameters_PinDurationMinusOne_IsForever()
    {
        // §F23: -1 = forever sentinel.
        var p = new SendPinMessageParameters("+1", ["+2"], null, null, false, false,
            PinDuration: -1, TargetAuthor: "+3", TargetTimestamp: 100L, Story: false);
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.SendPinMessageParameters);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(-1, doc.RootElement.GetProperty("pinDuration").GetInt32());
    }

    [Fact]
    public void SendMessageRequestResponseParameters_TypeIsLowercase()
    {
        // §F2 wire: lowercase string {accept, delete}.
        var p = new SendMessageRequestResponseParameters("+1", ["+2"], null, null, "accept");
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.SendMessageRequestResponseParameters);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("accept", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void SendPaymentNotificationParameters_RecipientIsSingularString()
    {
        // Single-recipient only — not list.
        var p = new SendPaymentNotificationParameters("+1", "+2", "dGVzdA==", Note: "thanks");
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.SendPaymentNotificationParameters);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("recipient").ValueKind);
        Assert.Equal("+2", doc.RootElement.GetProperty("recipient").GetString());
        Assert.Equal("dGVzdA==", doc.RootElement.GetProperty("receipt").GetString());
    }

    [Fact]
    public void SendAdminDeleteParameters_GroupIdsArePresent_NoRecipient()
    {
        // Group-only — wire has groupId array; жодного recipient.
        var p = new SendAdminDeleteParameters("+1", ["g=="], false, "+2", 100L, false);
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.SendAdminDeleteParameters);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("g==", doc.RootElement.GetProperty("groupId")[0].GetString());
        Assert.False(doc.RootElement.TryGetProperty("recipient", out _));
    }
}
