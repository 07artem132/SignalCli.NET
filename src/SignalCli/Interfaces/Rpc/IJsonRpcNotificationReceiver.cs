using SignalCli.Models.Rpc;
using SignalCli.Models.Signal.Events;

namespace SignalCli.Interfaces.Rpc;

/// <summary>
/// Приймач нотифікацій від JSON-RPC сервера.
/// </summary>
/// <remarks>
/// Дозволяє підписатись на потік подій, що надходять від сервера без явного запиту.
/// Використовує Reactive Extensions (Rx) для обробки потоку нотифікацій.
/// </remarks>
public interface IJsonRpcNotificationReceiver
{
    /// <summary>
    /// Потік нотифікацій JSON-RPC.
    /// </summary>
    /// <remarks>
    /// Кожна нотифікація містить метод виклику та типізовані параметри.
    /// Для отримання нотифікацій потрібно підписатись на цей потік через метод Subscribe().
    /// </remarks>
    IObservable<JsonRpcNotification<SubscriptionEventArgs>> Notifications { get; }
}