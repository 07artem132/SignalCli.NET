using JetBrains.Annotations;
using SignalCli.Interfaces.Signal;

namespace SignalCli.Models.Signal.Message;

/// <summary>
/// Отримувач повідомлення типу "група".
/// </summary>
/// <remarks>
/// Реалізація інтерфейсу <see cref="IRecipient"/> для груп у Signal.
/// Використовується для надсилання повідомлень у групові чати.
/// </remarks>
/// <param name="groupId">Ідентифікатор групи.</param>
/// <exception cref="ArgumentNullException">Виникає, якщо <paramref name="groupId"/> є null.</exception>
/// <exception cref="ArgumentException">Виникає, якщо <paramref name="groupId"/> — порожній рядок.</exception>
[PublicAPI]
public class GroupRecipient(string groupId) : IRecipient
{
    /// <summary>
    /// Визначає, що цей отримувач є групою.
    /// </summary>
    /// <value>Завжди повертає true.</value>
    public bool IsGroup => true;

    // post-modernize-tuning §4.18 (audit N4): null vs empty differentiate correctly.
    private string GroupId { get; } = ValidateAndReturn(groupId);

    private static string ValidateAndReturn(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value, nameof(groupId));
        return value;
    }

    /// <summary>
    /// Ідентифікатор групи.
    /// </summary>
    /// <value>Унікальний ідентифікатор групи в Signal.</value>
    public string Identifier => GroupId;
}
