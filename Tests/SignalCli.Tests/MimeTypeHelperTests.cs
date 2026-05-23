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
    // F16: ISO-BMFF тепер дискримінує major-brand на байтах 8..11.
    // isom/mp42/avc1 → MP4; qt → MOV; heic/M4A/3gpp → відповідно.
    [InlineData("000000006674797069736F6D", "video/mp4")]      // isom
    [InlineData("000000006674797071742020", "video/quicktime")] // "qt  "
    [InlineData("000000006674797068656963", "image/heic")]      // heic
    [InlineData("000000006674797033677034", "video/3gpp")]      // 3gp4
    [InlineData("000000006674797033673261", "video/3gpp2")]     // 3g2a
    public void GetMimeType_DetectsBySignature(string hex, string expected)
    {
        Assert.Equal(expected, MimeTypeHelper.GetMimeType(Hex(hex)));
    }

    [Fact]
    public void GetMimeType_IsoBmff_M4A_ReturnsAudioMp4()
    {
        // "M4A " — пробіл наприкінці значимий (4 символи).
        var bytes = Hex("00000000667479704D344120");
        Assert.Equal("audio/mp4", MimeTypeHelper.GetMimeType(bytes));
    }

    [Fact]
    public void GetMimeType_IsoBmff_UnknownBrand_FallsBackToMp4()
    {
        // Невідомий major brand — fallback на video/mp4 (зворотна сумісність зі signal-cli).
        var bytes = Hex("00000000667479706162636400");
        Assert.Equal("video/mp4", MimeTypeHelper.GetMimeType(bytes));
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
