using JetBrains.Annotations;
using Newtonsoft.Json;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Відповідь на запит списку облікових записів Signal.
/// </summary>
/// <remarks>
/// Наслідує List&lt;Account&gt; і містить колекцію зареєстрованих облікових
/// записів у локальному сховищі Signal CLI.
/// </remarks>
[PublicAPI,JsonArray]
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