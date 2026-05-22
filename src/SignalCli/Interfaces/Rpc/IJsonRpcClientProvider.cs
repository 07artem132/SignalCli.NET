namespace SignalCli.Interfaces.Rpc;

/// <summary>
/// Постачальник готового екземпляра JSON-RPC клієнта.
/// </summary>
/// <remarks>
/// Надає доступ до попередньо створеного та ініціалізованого клієнта.
/// Зазвичай реалізується як сервіс одиночка (singleton) в DI-контейнері.
/// </remarks>
public interface IJsonRpcClientProvider
{
    /// <summary>
    /// Поточний активний екземпляр JSON-RPC клієнта.
    /// </summary>
    /// <exception cref="InvalidOperationException">Виникає, якщо клієнт ще не ініціалізовано.</exception>
    IJsonRpcClient Client { get; }
}