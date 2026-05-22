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
/// <exception cref="ArgumentNullException">Виникає, якщо groupId є null або порожнім.</exception>
[PublicAPI]
public class GroupRecipient(string groupId) : IRecipient
{
    /// <summary>
    /// Визначає, що цей отримувач є групою.
    /// </summary>
    /// <value>Завжди повертає true.</value>
    public bool IsGroup => true;

    private string GroupId { get; } =
        string.IsNullOrEmpty(groupId) ? throw new ArgumentNullException(nameof(groupId)) : groupId;

    /// <summary>
    /// Ідентифікатор групи.
    /// </summary>
    /// <value>Унікальний ідентифікатор групи в Signal.</value>
    public string Identifier => GroupId;
}
