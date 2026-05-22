using System.Globalization;
using SignalCli.Utilities;

namespace SignalCli.Tests;

public class TextStyleParserTests
{
    [Fact]
    public void Parse_AsciiItalic_ProducesUtf16Range()
    {
        var (text, styles) = new TextStyleParser("*hi*").Parse();

        Assert.Equal("hi", text);
        Assert.Equal(new[] { "0:2:ITALIC" }, styles);
    }

    [Fact]
    public void Parse_Bold_ProducesBoldRange()
    {
        var (text, styles) = new TextStyleParser("**bold**").Parse();

        Assert.Equal("bold", text);
        Assert.Equal(new[] { "0:4:BOLD" }, styles);
    }

    [Fact]
    public void Parse_UnderTurkishCulture_UsesInvariantStyleName()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            var (_, styles) = new TextStyleParser("*x*").Parse();

            // Без ToUpperInvariant у tr-TR вийшло б "İTALİC".
            Assert.Equal(new[] { "0:1:ITALIC" }, styles);
            Assert.DoesNotContain("İ", styles[0]);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Parse_EscapedMarkers_AreLiteralWithNoRanges()
    {
        var (text, styles) = new TextStyleParser(@"\*not italic\*").Parse();

        Assert.Equal("*not italic*", text);
        Assert.Empty(styles);
    }

    [Fact]
    public void Parse_EscapedBackslashBeforeMarker_KeepsBackslashAndStyles()
    {
        var (text, styles) = new TextStyleParser(@"\\*italic*").Parse();

        Assert.Equal(@"\italic", text);
        Assert.Equal(new[] { "1:6:ITALIC" }, styles);
    }
}
