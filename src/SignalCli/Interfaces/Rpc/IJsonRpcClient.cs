namespace SignalCli.Interfaces.Rpc;

/// <summary>
/// Клієнт для взаємодії з JSON-RPC сервером.
/// </summary>
/// <remarks>
/// Об'єднує функціональність відправки запитів (<see cref="IJsonRpcSender"/>), 
/// отримання нотифікацій (<see cref="IJsonRpcNotificationReceiver"/>) 
/// та керування ресурсами (<see cref="IDisposable"/>).
/// Використовується як основний інтерфейс для комунікації з JSON-RPC сервером.
/// </remarks>
public interface IJsonRpcClient :
    IJsonRpcSender,
    IJsonRpcNotificationReceiver,
    IDisposable
{
}