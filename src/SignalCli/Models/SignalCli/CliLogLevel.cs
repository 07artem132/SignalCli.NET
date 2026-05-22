namespace SignalCli.Models.SignalCli;

/// <summary>
/// Рівень логування для Signal CLI.
/// </summary>
/// <remarks>
/// Визначає детальність виведення діагностичної інформації
/// при роботі з Signal CLI.
/// </remarks>
public enum CliLogLevel : sbyte
{
    /// <summary>
    /// Інформаційні повідомлення.
    /// </summary>
    /// <remarks>
    /// Базовий рівень логування, що містить загальну інформацію про роботу.
    /// Відповідає прапорцю -v у Signal CLI.
    /// </remarks>
    Info,
    
    /// <summary>
    /// Повідомлення для відлагодження.
    /// </summary>
    /// <remarks>
    /// Розширений рівень логування, що містить додаткову інформацію для розробників.
    /// Відповідає прапорцю -vv у Signal CLI.
    /// </remarks>
    Debug,
    
    /// <summary>
    /// Детальні повідомлення для діагностики.
    /// </summary>
    /// <remarks>
    /// Максимальний рівень деталізації. Включає всі можливі повідомлення.
    /// Відповідає прапорцю -vvv у Signal CLI.
    /// </remarks>
    Verbose
}