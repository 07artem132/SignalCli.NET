using SignalCli.Models.Signal.Message;

namespace SignalCli.Interfaces.Signal;

/// <summary>
/// Отримувач повідомлення у Signal.
/// </summary>
/// <remarks>
/// Абстракція для представлення отримувачів повідомлень - індивідуальних користувачів або груп.
/// Реалізації: <see cref="UserRecipient"/> та 
/// <see cref="GroupRecipient"/>.
/// </remarks>
public interface IRecipient
{
    /// <summary>
    /// Визначає, чи є отримувач групою.
    /// </summary>
    /// <value>true - для групи, false - для індивідуального користувача.</value>
    bool IsGroup { get; }
    
    /// <summary>
    /// Унікальний ідентифікатор отримувача.
    /// </summary>
    /// <remarks>
    /// Для користувача - номер телефону або UUID.
    /// Для групи - ідентифікатор групи.
    /// </remarks>
    string Identifier { get; }
}

