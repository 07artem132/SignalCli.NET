using SignalCli.Interfaces.FileSystem;
using SignalCli.Utilities;

namespace SignalCli.Services.FileSystem;

public class AttachmentEntry(string fileName, byte[] data) : IAttachmentEntry
{
    public string FileName { get; } = fileName;
    
    public byte[] Data { get; } = data;
    
    public string FilePath { get; private set; } = string.Empty;

    public string MimeType => MimeTypeHelper.GetMimeType(Data, FileName);

    /// <summary>
    /// Безпечне ім'я файлу без компонентів шляху (захист від path traversal).
    /// Відкидає каталоги та послідовності "../"; за порожнього результату — "attachment".
    /// </summary>
    private string SafeFileName
    {
        get
        {
            var name = Path.GetFileName(FileName);
            return string.IsNullOrWhiteSpace(name) ? "attachment" : name;
        }
    }

    public string ToDataUri()
    {
        string base64Data = Convert.ToBase64String(Data);
        return $"data:{MimeType};filename={SafeFileName};base64,{base64Data}";
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
        // Використовуємо лише безпечне ім'я файлу, щоб "../" чи розділювачі шляху
        // не дозволили записати файл поза згенерованою тимчасовою директорією.
        var tempPath = Path.Combine(targetDirectory, SafeFileName);
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
                FilePath = string.Empty;
            }
            catch (Exception ex)
            {
                throw new IOException($"Не вдалося видалити тимчасовий файл або папку: {FilePath}", ex);
            }
        }
    }
}