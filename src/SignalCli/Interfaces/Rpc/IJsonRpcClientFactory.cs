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
    /// Асинхронно створює новий екземпляр JSON-RPC клієнта.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Новий налаштований екземпляр <see cref="IJsonRpcClient"/>.</returns>
    /// <exception cref="InvalidOperationException">Виникає при помилці створення клієнта.</exception>
    /// <exception cref="OperationCanceledException">Виникає, якщо операцію скасовано.</exception>
    Task<IJsonRpcClient> CreateAsync(CancellationToken cancellationToken = default);
}
