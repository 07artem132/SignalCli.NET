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
/// <exception cref="ArgumentNullException">Виникає, якщо phoneNumberOrUuid є null або порожнім.</exception>
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
    public string Identifier { get; } = !string.IsNullOrEmpty(phoneNumberOrUuid)
        ? phoneNumberOrUuid
        : throw new ArgumentNullException(nameof(phoneNumberOrUuid));
}