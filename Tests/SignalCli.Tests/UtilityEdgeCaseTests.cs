using SignalCli.Exceptions;
using SignalCli.Models.Rpc;
using SignalCli.Utilities;

namespace SignalCli.Tests;

public class AtomicCounterTests
{
    [Fact]
    public void Increment_IsSequential()
    {
        var c = new AtomicCounter();
        Assert.Equal(1, c.Increment());
        Assert.Equal(2, c.Increment());
        Assert.Equal(3, c.Increment());
    }

    [Fact]
    public void Increment_RespectsSeed()
    {
        var c = new AtomicCounter(10);
        Assert.Equal(11, c.Increment());
    }

    [Fact]
    public void Increment_IsThreadSafe_NoDuplicates()
    {
        var c = new AtomicCounter();
        var results = new System.Collections.Concurrent.ConcurrentBag<int>();
        Parallel.For(0, 1000, _ => results.Add(c.Increment()));
        Assert.Equal(1000, results.Distinct().Count());
    }
}

public class JsonRpcExceptionTests
{
    [Fact]
    public void FromError_PopulatesErrorAndMessage()
    {
        var err = new JsonRpcError { Code = -32602, Message = "Invalid params" };
        var ex = new JsonRpcException(err);
        Assert.Same(err, ex.Error);
        Assert.Equal("Invalid params", ex.Message);
    }

    [Fact]
    public void FromMessage_CreatesInternalError()
    {
        var ex = new JsonRpcException("boom");
        Assert.Equal(-32000, ex.Error.Code);
        Assert.Equal("boom", ex.Error.Message);
    }

    [Fact]
    public void NullError_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new JsonRpcException((JsonRpcError)null!));
    }
}

public class TextStyleParserMarkerTests
{
    [Theory]
    [InlineData("**b**", "b", "0:1:BOLD")]
    [InlineData("`m`", "m", "0:1:MONOSPACE")]
    [InlineData("~s~", "s", "0:1:STRIKETHROUGH")]
    [InlineData("||sp||", "sp", "0:2:SPOILER")]
    public void Parse_EachMarkerType(string input, string expectedText, string expectedRange)
    {
        var (text, styles) = new TextStyleParser(input).Parse();
        Assert.Equal(expectedText, text);
        Assert.Equal(new[] { expectedRange }, styles);
    }

    [Fact]
    public void Parse_UnclosedMarker_StripsMarkerNoRange()
    {
        var (text, styles) = new TextStyleParser("*unclosed").Parse();
        Assert.Equal("unclosed", text);
        Assert.Empty(styles);
    }

    [Fact]
    public void Parse_PlainText_Unchanged()
    {
        var (text, styles) = new TextStyleParser("just plain text").Parse();
        Assert.Equal("just plain text", text);
        Assert.Empty(styles);
    }
}
