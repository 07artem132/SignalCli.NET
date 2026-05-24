using System.Text;
using SignalCli.Services.FileSystem;

namespace SignalCli.Tests;

public class AttachmentEntryTests
{
    [Fact]
    public void SaveToTempFile_TraversalFileName_StaysInsideTempDirectory()
    {
        var data = Encoding.UTF8.GetBytes("payload");
        // Спроба path traversal у імені файлу
        var entry = new AttachmentEntry(Path.Combine("..", "..", "evil.txt"), data);

        try
        {
            entry.SaveToTempFile();

            var savedDir = Path.GetDirectoryName(entry.FilePath)!;
            var tempRoot = Path.GetFullPath(Path.GetTempPath());

            // Файл має лежати в підкаталозі темпу, а не вище за нього
            Assert.StartsWith(tempRoot, Path.GetFullPath(savedDir));
            Assert.Equal("evil.txt", Path.GetFileName(entry.FilePath));
            Assert.True(File.Exists(entry.FilePath));
        }
        finally
        {
            entry.DeleteTempFile();
        }
    }

    [Fact]
    public void ToDataUri_TraversalFileName_UsesSanitizedName()
    {
        var data = Encoding.UTF8.GetBytes("payload");
        var entry = new AttachmentEntry(Path.Combine("..", "secret", "evil.bin"), data);

        var uri = entry.ToDataUri();

        Assert.Contains("filename=evil.bin;", uri);
        Assert.DoesNotContain("..", uri.Split("base64,")[0]);
    }

    [Fact]
    public void SaveToTempFile_EmptyAfterTraversal_FallsBackToAttachment()
    {
        var data = Encoding.UTF8.GetBytes("x");
        var entry = new AttachmentEntry("../", data);

        try
        {
            entry.SaveToTempFile();
            Assert.Equal("attachment", Path.GetFileName(entry.FilePath));
        }
        finally
        {
            entry.DeleteTempFile();
        }
    }

    // safe-filename-hardening (CHANGELOG [4.0] pending follow-up): edge-cases для
    // SafeFileName поза traversal.

    [Fact]
    public void SaveToTempFile_FileNameWithNulByte_StripsControlChar()
    {
        var data = Encoding.UTF8.GetBytes("p");
        // NUL byte у середині імені — у багатьох ОС це truncate'ить, з GUI може спричинити
        // невидиме розширення. Має бути відфільтрований.
        var entry = new AttachmentEntry("evil\0.txt", data);

        try
        {
            entry.SaveToTempFile();
            var saved = Path.GetFileName(entry.FilePath);
            Assert.DoesNotContain('\0', saved);
            Assert.True(File.Exists(entry.FilePath));
        }
        finally
        {
            entry.DeleteTempFile();
        }
    }

    [Fact]
    public void ToDataUri_FileNameWithRightToLeftOverride_StripsBidiChar()
    {
        var data = Encoding.UTF8.GetBytes("p");
        // U+202E RIGHT-TO-LEFT OVERRIDE — класичний UI-spoofing: "evil‮gpj.exe"
        // у Explorer відображається як "evilexe.jpg" (розширення помінялось візуально).
        var entry = new AttachmentEntry("evil‮gpj.exe", data);

        var uri = entry.ToDataUri();

        Assert.DoesNotContain('‮', uri);
        Assert.Contains("filename=evilgpj.exe;", uri);
    }

    [Theory]
    [InlineData('‪')] // LEFT-TO-RIGHT EMBEDDING
    [InlineData('‫')] // RIGHT-TO-LEFT EMBEDDING
    [InlineData('‬')] // POP DIRECTIONAL FORMATTING
    [InlineData('‭')] // LEFT-TO-RIGHT OVERRIDE
    [InlineData('⁦')] // LEFT-TO-RIGHT ISOLATE
    [InlineData('⁧')] // RIGHT-TO-LEFT ISOLATE
    [InlineData('⁨')] // FIRST STRONG ISOLATE
    [InlineData('⁩')] // POP DIRECTIONAL ISOLATE
    [InlineData('')] // DEL
    public void ToDataUri_FileNameWithBidiOrControlChar_FiltersOut(char dangerousChar)
    {
        var data = Encoding.UTF8.GetBytes("p");
        var entry = new AttachmentEntry($"name{dangerousChar}.ext", data);

        var uri = entry.ToDataUri();

        Assert.DoesNotContain(dangerousChar, uri);
    }

    [Fact]
    public void SaveToTempFile_FileNameOnlyDangerousChars_FallsBackToAttachment()
    {
        var data = Encoding.UTF8.GetBytes("p");
        // Усі символи фільтруються — fallback на "attachment".
        var entry = new AttachmentEntry("\0‮", data);

        try
        {
            entry.SaveToTempFile();
            Assert.Equal("attachment", Path.GetFileName(entry.FilePath));
        }
        finally
        {
            entry.DeleteTempFile();
        }
    }

    [Fact]
    public void SaveToTempFile_CalledTwice_ThrowsInvalidOperation()
    {
        var data = Encoding.UTF8.GetBytes("p");
        var entry = new AttachmentEntry("a.txt", data);

        try
        {
            entry.SaveToTempFile();
            // Re-entry має бути explicit-fail щоб caller'ів не путати ілюзією успіху
            // (FilePath заповнений з першого виклику; другий ніколи б нічого не записав).
            var ex = Assert.Throws<InvalidOperationException>(() => entry.SaveToTempFile());
            Assert.Contains("вже збережено", ex.Message);
        }
        finally
        {
            entry.DeleteTempFile();
        }
    }

    [Fact]
    public void SafeFileName_LongName_FiltersWithoutStackOverflow()
    {
        // Heap-path branch у фільтрації (name.Length > 256).
        var data = Encoding.UTF8.GetBytes("p");
        var longName = new string('a', 300) + "\0" + ".txt";
        var entry = new AttachmentEntry(longName, data);

        var uri = entry.ToDataUri();

        Assert.DoesNotContain('\0', uri);
        Assert.Contains(new string('a', 300) + ".txt", uri);
    }
}
