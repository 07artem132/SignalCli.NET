namespace SignalCli.Interfaces.Rpc;

/// <summary>
/// Фабрика для створення екземплярів JSON-RPC клієнтів.
/// </summary>
/// <remarks>
/// Відповідає за ініціалізацію та налаштування нових екземплярів <see cref="IJsonRpcClient"/>.
/// </remarks>
public interface IJsonRpcClientFactory
{
    /// <summary>
    /// Синхронно створює новий екземпляр JSON-RPC клієнта.
    /// </summary>
    /// <returns>Новий налаштований екземпляр <see cref="IJsonRpcClient"/>.</returns>
    /// <remarks>
    /// A.7: метод синхронний — усередині немає async-операцій (фабрика лише викликає
    /// конструктор). Це уникає фальшивого <c>Async</c>-суфіксу.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Виникає при помилці створення клієнта.</exception>
    IJsonRpcClient Create();
}
