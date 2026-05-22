namespace SignalCli.Interfaces.FileSystem;

/// <summary>
/// Вкладення повідомлення з методами для роботи з його вмістом.
/// </summary>
/// <remarks>
/// Дозволяє працювати з двійковими даними вкладення, зберігати їх у тимчасові файли,
/// перетворювати у формат Data URI та керувати життєвим циклом.
/// </remarks>
public interface IAttachmentEntry
{
    /// <summary>
    /// Ім'я файлу вкладення.
    /// </summary>
    string FileName { get; }
    
    /// <summary>
    /// Двійкові дані вкладення.
    /// </summary>
    byte[] Data { get; }
    
    /// <summary>
    /// Шлях до тимчасового файлу вкладення.
    /// </summary>
    /// <value>Шлях до файлу або порожній рядок, якщо файл не збережено.</value>
    /// <remarks>
    /// Встановлюється після успішного виклику <see cref="SaveToTempFile"/>.
    /// </remarks>
    string FilePath { get; }

    /// <summary>
    /// Перетворює вкладення у формат Data URI.
    /// </summary>
    /// <returns>Рядок у форматі "data:[MIME-TYPE];filename=[FILENAME];base64,[BASE64 DATA]".</returns>
    string ToDataUri();
    
    /// <summary>
    /// Зберігає вкладення у тимчасовий файл.
    /// </summary>
    /// <remarks>
    /// Створює унікальну тимчасову директорію для файлу.
    /// Після успішного збереження оновлює властивість <see cref="FilePath"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Файл вже збережено у тимчасовій папці.</exception>
    /// <exception cref="IOException">Виникає при проблемах запису на диск.</exception>
    void SaveToTempFile();
    
    /// <summary>
    /// Видаляє тимчасовий файл вкладення.
    /// </summary>
    /// <remarks>
    /// Видаляє як сам файл, так і тимчасову директорію, в якій він знаходиться.
    /// </remarks>
    /// <exception cref="IOException">Виникає, якщо не вдалося видалити файл або директорію.</exception>
    void DeleteTempFile();
}
