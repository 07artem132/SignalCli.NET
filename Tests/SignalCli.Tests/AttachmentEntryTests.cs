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
}
