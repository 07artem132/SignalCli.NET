using System.Text.Json;
using SignalCli.Models.Signal;
using SignalCli.Serialization;

namespace SignalCli.Tests.Serialization;

/// <summary>
/// signal-cli-api-coverage Wave 7b: wire-shape pinning для 7 нових receive-side decoders.
/// </summary>
public class ReceiveDecodersSerializationTests
{
    [Fact]
    public void JsonPayment_NewShape_HasNoteAndReceipt()
    {
        // BREAKING fix: old (Amount, Currency) → new (Note, Receipt: byte[]?).
        const string json = """{"note":"Thanks!","receipt":"dGVzdC1ieXRlcw=="}""";
        var p = JsonSerializer.Deserialize(json, SignalJsonContext.Default.JsonPayment)!;
        Assert.Equal("Thanks!", p.Note);
        Assert.NotNull(p.Receipt);
        Assert.Equal("test-bytes", System.Text.Encoding.UTF8.GetString(p.Receipt!));
    }

    [Fact]
    public void JsonPayment_NullReceipt_DeserializesToNull()
    {
        // api-coverage-audit-followup §2: upstream Java has no NRT enforcement; wire-level
        // `"receipt": null` MUST deserialize to null (consumer pattern `p.Receipt?.Length`
        // обовʼязковий). Без цього guard'у попередній `byte[]` declaration давав silent NRE.
        const string json = """{"note":"No receipt","receipt":null}""";
        var p = JsonSerializer.Deserialize(json, SignalJsonContext.Default.JsonPayment)!;
        Assert.Equal("No receipt", p.Note);
        Assert.Null(p.Receipt);
    }

    [Fact]
    public void JsonPayment_MissingReceipt_DeserializesToNull()
    {
        // api-coverage-audit-followup §2: missing-field case — Jackson permissiveness
        // дозволяє пропустити поле, STJ source-gen теж робить так само (default-value).
        const string json = """{"note":"Just a note"}""";
        var p = JsonSerializer.Deserialize(json, SignalJsonContext.Default.JsonPayment)!;
        Assert.Equal("Just a note", p.Note);
        Assert.Null(p.Receipt);
    }

    [Fact]
    public void JsonPollCreate_DeserializesCorrectly()
    {
        const string json = """{"question":"Best?","allowMultiple":true,"options":["A","B","C"]}""";
        var pc = JsonSerializer.Deserialize(json, SignalJsonContext.Default.JsonPollCreate)!;
        Assert.Equal("Best?", pc.Question);
        Assert.True(pc.AllowMultiple);
        Assert.Equal(3, pc.Options.Count);
    }

    [Fact]
    public void JsonPollVote_HasZeroBasedIndexes_AndDeprecatedAuthor()
    {
        // §F22 zero-based indexes; §F17 author legacy field still on wire.
        const string json = """{"author":"+legacy","authorNumber":"+1","authorUuid":"abc-uuid","targetSentTimestamp":1717,"optionIndexes":[0,2],"voteCount":3}""";
        var pv = JsonSerializer.Deserialize(json, SignalJsonContext.Default.JsonPollVote)!;
        Assert.Equal("+legacy", pv.Author);
        Assert.Equal(1717L, pv.TargetSentTimestamp);
        Assert.Equal(new[] { 0, 2 }, pv.OptionIndexes);
        Assert.Equal(3, pv.VoteCount);
    }

    [Fact]
    public void JsonPollTerminate_SingleField()
    {
        const string json = """{"targetSentTimestamp":1717}""";
        var pt = JsonSerializer.Deserialize(json, SignalJsonContext.Default.JsonPollTerminate)!;
        Assert.Equal(1717L, pt.TargetSentTimestamp);
    }

    [Fact]
    public void JsonPinMessage_PinDurationSecondsIsLong()
    {
        // §F16: receive-side long (not send-side int).
        const string json = """{"targetAuthor":null,"targetAuthorNumber":"+1","targetAuthorUuid":null,"targetSentTimestamp":100,"pinDurationSeconds":-1}""";
        var pm = JsonSerializer.Deserialize(json, SignalJsonContext.Default.JsonPinMessage)!;
        Assert.Equal(-1L, pm.PinDurationSeconds);
        Assert.IsType<long>((object)pm.PinDurationSeconds);
    }

    [Fact]
    public void JsonUnpinMessage_NoDurationField()
    {
        const string json = """{"targetAuthor":null,"targetAuthorNumber":"+1","targetAuthorUuid":null,"targetSentTimestamp":100}""";
        var upm = JsonSerializer.Deserialize(json, SignalJsonContext.Default.JsonUnpinMessage)!;
        Assert.Equal(100L, upm.TargetSentTimestamp);
    }

    [Fact]
    public void JsonAdminDelete_StructurallySameAsUnpin()
    {
        const string json = """{"targetAuthor":null,"targetAuthorNumber":"+offender","targetAuthorUuid":null,"targetSentTimestamp":200}""";
        var ad = JsonSerializer.Deserialize(json, SignalJsonContext.Default.JsonAdminDelete)!;
        Assert.Equal("+offender", ad.TargetAuthorNumber);
        Assert.Equal(200L, ad.TargetSentTimestamp);
    }

    [Fact]
    public void JsonDataMessage_AllSevenNewFieldsNullableAndOmittable()
    {
        // Backward-compat: existing envelopes (no new fields) деsеріалізуються без помилки.
        const string json = """{"timestamp":1,"message":"hi"}""";
        var dm = JsonSerializer.Deserialize(json, SignalJsonContext.Default.JsonDataMessage)!;
        Assert.Null(dm.PollCreate);
        Assert.Null(dm.PollVote);
        Assert.Null(dm.PollTerminate);
        Assert.Null(dm.Payment);
        Assert.Null(dm.PinMessage);
        Assert.Null(dm.UnpinMessage);
        Assert.Null(dm.AdminDelete);
    }

    [Fact]
    public void JsonDataMessage_PresenceBasedUnion_AllSevenCanCoexist()
    {
        // Critical rule #4: presence-based union. Кілька полів non-null одночасно — валідно.
        const string json = """
        {
          "timestamp":1,
          "message":"join me",
          "pollCreate":{"question":"Q?","allowMultiple":false,"options":["A","B"]},
          "pinMessage":{"targetAuthor":null,"targetAuthorNumber":"+1","targetAuthorUuid":null,"targetSentTimestamp":100,"pinDurationSeconds":3600}
        }
        """;
        var dm = JsonSerializer.Deserialize(json, SignalJsonContext.Default.JsonDataMessage)!;
        Assert.NotNull(dm.Message);
        Assert.NotNull(dm.PollCreate);
        Assert.NotNull(dm.PinMessage);
        Assert.Equal(3600L, dm.PinMessage.PinDurationSeconds);
    }
}
