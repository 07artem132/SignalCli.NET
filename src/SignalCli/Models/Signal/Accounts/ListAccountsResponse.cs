using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Відповідь на запит списку облікових записів Signal.
/// </summary>
/// <remarks>
/// Наслідує List&lt;Account&gt; і містить колекцію зареєстрованих облікових
/// записів у локальному сховищі Signal CLI.
/// </remarks>
// STJ серіалізує типи, похідні від List<T>, як JSON-масив без додаткових атрибутів.
[PublicAPI]
public class ListAccountsResponse : List<Account>;

/// <summary>
/// Інформація про обліковий запис Signal.
/// </summary>
/// <remarks>
/// Містить основні ідентифікаційні дані облікового запису.
/// </remarks>
/// <param name="Number">Номер телефону облікового запису.</param>
[PublicAPI]
public record Account(string Number);