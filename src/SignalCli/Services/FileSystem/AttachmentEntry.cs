using SignalCli.Interfaces.FileSystem;
using SignalCli.Utilities;

namespace SignalCli.Services.FileSystem;

/// <summary>
/// Конкретна реалізація <see cref="IAttachmentEntry"/> для байтів у пам'яті +
/// допоміжна логіка для збереження в темп-файл / data-URI / визначення MIME-типу.
/// </summary>
/// <param name="fileName">Ім'я файлу (для розширення, MIME-резерву та <see cref="SafeFileName"/>).</param>
/// <param name="data">Сирі байти вкладення.</param>
public class AttachmentEntry(string fileName, byte[] data) : IAttachmentEntry
{
    /// <summary>Оригінальне ім'я файлу (як передано конструктору).</summary>
    public string FileName { get; } = fileName;

    /// <summary>Сирі байти вкладення.</summary>
    public byte[] Data { get; } = data;

    /// <summary>Шлях до тимчасового файлу (заповнюється <see cref="SaveToTempFile"/>).</summary>
    public string FilePath { get; private set; } = string.Empty;

    /// <summary>Визначений MIME-тип вкладення (за сигнатурою або розширенням).</summary>
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

    /// <summary>
    /// Будує data-URI з base64-кодованими байтами (інлайн-варіант передачі вкладення).
    /// </summary>
    public string ToDataUri()
    {
        string base64Data = Convert.ToBase64String(Data);
        return $"data:{MimeType};filename={SafeFileName};base64,{base64Data}";
    }

    /// <summary>
    /// Зберігає вкладення в окрему тимчасову директорію (Path.GetTempPath() + GUID)
    /// під безпечним ім'ям файлу. <see cref="FilePath"/> після успішного збереження
    /// містить повний шлях.
    /// </summary>
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

    /// <summary>
    /// Видаляє тимчасовий файл (якщо його було створено через <see cref="SaveToTempFile"/>)
    /// разом із його окремою директорією; обнуляє <see cref="FilePath"/>.
    /// </summary>
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