using SignalCli.Interfaces.FileSystem;
using SignalCli.Utilities;

namespace SignalCli.Services.FileSystem;

public class AttachmentEntry(string fileName, byte[] data) : IAttachmentEntry
{
    public string FileName { get; } = fileName;
    
    public byte[] Data { get; } = data;
    
    public string FilePath { get; private set; }

    public string MimeType => MimeTypeHelper.GetMimeType(Data, FileName);

    public string ToDataUri()
    {
        string base64Data = Convert.ToBase64String(Data);
        return $"data:{MimeType};filename={FileName};base64,{base64Data}";
    }

    public void SaveToTempFile()
    {
        if (!string.IsNullOrEmpty(FilePath))
            throw new InvalidOperationException("Файл вже збережено у тимчасовій папці.");

        var tempDirectory = Path.GetTempPath();
        string guidDirectory;
        string targetDirectory;

        do
        {
            guidDirectory = Guid.NewGuid().ToString();
            targetDirectory = Path.Combine(tempDirectory, guidDirectory);
        } while (Directory.Exists(targetDirectory));

        Directory.CreateDirectory(targetDirectory);
        var tempPath = Path.Combine(targetDirectory, FileName);
        File.WriteAllBytes(tempPath, Data);
        FilePath = tempPath;
    }

    public void DeleteTempFile()
    {
        if (!string.IsNullOrEmpty(FilePath) && File.Exists(FilePath))
        {
            try
            {
                var directory = Path.GetDirectoryName(FilePath);
                File.Delete(FilePath);
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory);
                }
                FilePath = null;
            }
            catch (Exception ex)
            {
                throw new IOException($"Не вдалося видалити тимчасовий файл або папку: {FilePath}", ex);
            }
        }
    }
}