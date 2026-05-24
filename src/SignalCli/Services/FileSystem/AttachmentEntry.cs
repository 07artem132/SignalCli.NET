using System.Buffers;
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

    // safe-filename-hardening (CHANGELOG [4.0] pending follow-up): додатково до
    // Path.GetFileName-traversal-захисту фільтруємо control-чартки (NUL та інші <0x20,
    // плюс U+007F DEL) і bidi-override / formatting-символи (U+202A..U+202E, U+2066..U+2069),
    // які attacker може використати щоб маскувати справжнє розширення файлу у UI
    // (наприклад, "evil<RLO>gpj.exe" відображається у Explorer як "evilexe.jpg"). Також
    // відкидаємо chars невалідні для імен файлів на Windows (з Path.GetInvalidFileNameChars()).
    private static readonly SearchValues<char> InvalidFileNameChars =
        SearchValues.Create(BuildInvalidCharSet());

    private static char[] BuildInvalidCharSet()
    {
        var set = new HashSet<char>(Path.GetInvalidFileNameChars());
        for (var c = (char)0; c < ' '; c++) set.Add(c);
        set.Add((char)0x7F); // DEL
        // Bidi-override / formatting marks: LRE, RLE, PDF, LRO, RLO, LRI, RLI, FSI, PDI.
        set.Add((char)0x202A);
        set.Add((char)0x202B);
        set.Add((char)0x202C);
        set.Add((char)0x202D);
        set.Add((char)0x202E);
        set.Add((char)0x2066);
        set.Add((char)0x2067);
        set.Add((char)0x2068);
        set.Add((char)0x2069);
        return [.. set];
    }

    /// <summary>
    /// Безпечне ім'я файлу без компонентів шляху (захист від path traversal) і без
    /// control- / bidi-override-символів (захист від UI-spoofing). За порожнього
    /// результату повертає <c>"attachment"</c>.
    /// </summary>
    private string SafeFileName
    {
        get
        {
            var name = Path.GetFileName(FileName);
            if (string.IsNullOrWhiteSpace(name))
                return "attachment";

            // Швидкий шлях: якщо немає жодного небезпечного символу, повертаємо name as-is.
            if (!name.AsSpan().ContainsAny(InvalidFileNameChars))
                return name;

            // Інакше — фільтруємо посимвольно.
            Span<char> buffer = name.Length <= 256 ? stackalloc char[name.Length] : new char[name.Length];
            var write = 0;
            foreach (var ch in name)
            {
                if (!InvalidFileNameChars.Contains(ch))
                    buffer[write++] = ch;
            }

            if (write == 0) return "attachment";
            var filtered = new string(buffer[..write]);
            return string.IsNullOrWhiteSpace(filtered) ? "attachment" : filtered;
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
