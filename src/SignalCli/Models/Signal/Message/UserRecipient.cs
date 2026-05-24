using JetBrains.Annotations;
using SignalCli.Interfaces.Signal;

namespace SignalCli.Models.Signal.Message;

/// <summary>
/// Отримувач повідомлення типу "користувач".
/// </summary>
/// <remarks>
/// Реалізація інтерфейсу <see cref="IRecipient"/> для індивідуальних користувачів у Signal.
/// Використовується для надсилання особистих повідомлень.
/// </remarks>
/// <param name="phoneNumberOrUuid">Номер телефону або UUID користувача.</param>
/// <exception cref="ArgumentNullException">Виникає, якщо <paramref name="phoneNumberOrUuid"/> є null.</exception>
/// <exception cref="ArgumentException">Виникає, якщо <paramref name="phoneNumberOrUuid"/> — порожній рядок.</exception>
[PublicAPI]
public class UserRecipient(string phoneNumberOrUuid) : IRecipient
{
    /// <summary>
    /// Визначає, що цей отримувач не є групою.
    /// </summary>
    /// <value>Завжди повертає false.</value>
    public bool IsGroup => false;

    /// <summary>
    /// Номер телефону або UUID користувача.
    /// </summary>
    /// <value>Ідентифікатор користувача для адресації повідомлень.</value>
    /// <remarks>
    /// post-modernize-tuning §4.18 (audit N4): null → <see cref="ArgumentNullException"/>;
    /// empty → <see cref="ArgumentException"/>. Раніше обидва шляхи кидали NRE-замаскований
    /// під ArgumentNullException, що порушувало контракт обох типів. .NET 8+ helper
    /// <see cref="ArgumentException.ThrowIfNullOrEmpty(string?, string?)"/> робить цей розподіл правильно.
    /// </remarks>
    public string Identifier { get; } = ValidateAndReturn(phoneNumberOrUuid);

    private static string ValidateAndReturn(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value, nameof(phoneNumberOrUuid));
        return value;
    }
}