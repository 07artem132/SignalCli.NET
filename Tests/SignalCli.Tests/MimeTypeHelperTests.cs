using SignalCli.Utilities;

namespace SignalCli.Tests;

public class MimeTypeHelperTests
{
    private static byte[] Hex(string hex) =>
        Enumerable.Range(0, hex.Length / 2).Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16)).ToArray();

    [Theory]
    [InlineData("89504E4700000000", "image/png")]
    [InlineData("FFD8FF00", "image/jpeg")]
    [InlineData("47494638", "image/gif")]
    [InlineData("25504446", "application/pdf")]
    [InlineData("504B0304", "application/zip")]
    [InlineData("0000000066747970", "video/mp4")]   // "ftyp" at offset 4
    public void GetMimeType_DetectsBySignature(string hex, string expected)
    {
        Assert.Equal(expected, MimeTypeHelper.GetMimeType(Hex(hex)));
    }

    [Fact]
    public void GetMimeType_TooShort_ReturnsOctetStream()
    {
        Assert.Equal("application/octet-stream", MimeTypeHelper.GetMimeType([1, 2, 3]));
        Assert.Equal("application/octet-stream", MimeTypeHelper.GetMimeType((byte[])null!));
    }

    [Fact]
    public void GetMimeType_FallsBackToExtension()
    {
        var unknown = new byte[] { 1, 2, 3, 4, 5 };
        Assert.Equal("text/plain", MimeTypeHelper.GetMimeType(unknown, "notes.txt"));
        Assert.Equal("application/json", MimeTypeHelper.GetMimeType(unknown, "data.JSON")); // case-insensitive
        Assert.Equal("application/octet-stream", MimeTypeHelper.GetMimeType(unknown, "file.unknownext"));
    }

    [Fact]
    public void GetMimeType_StreamOverload_DetectsAndRewinds()
    {
        using var ms = new MemoryStream([0x25, 0x50, 0x44, 0x46, 0x2D]); // %PDF-
        var mime = MimeTypeHelper.GetMimeType(ms, "x.bin");
        Assert.Equal("application/pdf", mime);
        Assert.Equal(0, ms.Position); // повернуто на початок
    }

    [Fact]
    public void GetMimeType_NullStream_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MimeTypeHelper.GetMimeType((Stream)null!));
    }
}
