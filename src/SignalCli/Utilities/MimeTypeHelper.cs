namespace SignalCli.Utilities;

/// <summary>
/// Утилітний клас для визначення MIME-типів файлів на основі їх даних та імен.
/// </summary>
internal static class MimeTypeHelper
{
    // Словник відповідності розширень до MIME-типів (резервний варіант)
    private static readonly Dictionary<string, string> ExtensionMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".txt", "text/plain" },
        { ".html", "text/html" },
        { ".htm", "text/html" },
        { ".css", "text/css" },
        { ".js", "application/javascript" },
        { ".json", "application/json" },
        { ".xml", "application/xml" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png", "image/png" },
        { ".gif", "image/gif" },
        { ".bmp", "image/bmp" },
        { ".webp", "image/webp" },
        { ".pdf", "application/pdf" },
        { ".zip", "application/zip" },
        // Можна додати інші розширення за потреби
    };

    /// <summary>
    /// Визначає MIME-тип на основі даних файлу та, за потреби, його імені.
    /// Аналізує перші байти (сигнатури відомих форматів). Якщо тип не визначено за вмістом,
    /// використовує розширення файлу як резервний варіант.
    /// </summary>
    /// <param name="data">Байтовий масив даних файлу.</param>
    /// <param name="fileName">Опціональне ім'я файлу для визначення MIME-типу за розширенням.</param>
    /// <returns>MIME-тип у вигляді рядка.</returns>
    public static string GetMimeType(byte[] data, string fileName = null)
    {
        if (data == null || data.Length < 4)
            return "application/octet-stream";

        // Перевірка PNG: 89 50 4E 47
        if (data.Length >= 8 &&
            data[0] == 0x89 && data[1] == 0x50 &&
            data[2] == 0x4E && data[3] == 0x47)
        {
            return "image/png";
        }

        // Перевірка JPEG: FF D8 FF
        if (data.Length >= 3 &&
            data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return "image/jpeg";
        }

        // Перевірка GIF: 47 49 46 38
        if (data.Length >= 4 &&
            data[0] == 0x47 && data[1] == 0x49 &&
            data[2] == 0x46 && data[3] == 0x38)
        {
            return "image/gif";
        }

        // Перевірка PDF: 25 50 44 46
        if (data.Length >= 4 &&
            data[0] == 0x25 && data[1] == 0x50 &&
            data[2] == 0x44 && data[3] == 0x46)
        {
            return "application/pdf";
        }

        // Перевірка ZIP: 50 4B 03 04
        if (data.Length >= 4 &&
            data[0] == 0x50 && data[1] == 0x4B &&
            data[2] == 0x03 && data[3] == 0x04)
        {
            return "application/zip";
        }
        
        // Перевірка MP4: наявність "ftyp" на позиції 4
        if (data.Length >= 8 &&
            data[4] == 0x66 && data[5] == 0x74 &&
            data[6] == 0x79 && data[7] == 0x70)
        {
            return "video/mp4";
        }

        // Якщо не вдалося визначити за вмістом, спробуємо за розширенням файлу
        if (!string.IsNullOrEmpty(fileName))
        {
            string ext = Path.GetExtension(fileName);
            if (!string.IsNullOrEmpty(ext) &&
                ExtensionMimeTypes.TryGetValue(ext, out string mime))
            {
                return mime;
            }
        }

        return "application/octet-stream";
    }

    /// <summary>
    /// Визначає MIME-тип, зчитуючи дані з потоку.
    /// Потік повертається у початкову позицію, якщо це можливо.
    /// </summary>
    /// <param name="stream">Потік з даними файлу.</param>
    /// <param name="fileName">Опціональне ім'я файлу для визначення MIME-типу за розширенням.</param>
    /// <returns>MIME-тип у вигляді рядка.</returns>
    public static string GetMimeType(Stream stream, string fileName = null)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        byte[] buffer = new byte[8];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);

        if (stream.CanSeek)
            stream.Position = 0;

        byte[] data = buffer;
        if (bytesRead < buffer.Length)
        {
            data = new byte[bytesRead];
            Array.Copy(buffer, data, bytesRead);
        }
        return GetMimeType(data, fileName);
    }
}