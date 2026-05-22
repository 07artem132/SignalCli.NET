using SignalCli.Exceptions;
using SignalCli.Models.Signal.Accounts;

namespace SignalCli.Interfaces.Signal;

/// <summary>
/// Сервіс для роботи з обліковими записами Signal.
/// </summary>
/// <remarks>
/// Надає методи для отримання інформації про зареєстровані акаунти
/// та керування їхніми налаштуваннями.
/// </remarks>
public interface ISignalAccounts
{
    /// <summary>
    /// Отримує список зареєстрованих акаунтів Signal.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Список зареєстрованих акаунтів.</returns>
    /// <exception cref="InvalidOperationException">Виникає при помилці отримання списку акаунтів.</exception>
    /// <exception cref="JsonRpcException">Виникає при помилці JSON-RPC запиту.</exception>
    /// <exception cref="OperationCanceledException">Виникає, якщо операцію скасовано.</exception>
    /// <example>
    /// <code>
    /// var accounts = await signalAccounts.ListAccounts(cancellationToken);
    /// foreach (var account in accounts)
    /// {
    ///     Console.WriteLine($"Зареєстрований акаунт: {account.Number}");
    /// }
    /// </code>
    /// </example>
    Task<ListAccountsResponse> ListAccounts(
        CancellationToken cancellationToken = default
    );
    /// <summary>
    /// Надішліть повідомлення із запитом на синхронізацію на основний пристрій (для груп, контактів, ...).
    /// Основний пристрій відповість повідомленням синхронізації з повним списком контактів і груп.
    /// Синхронізація проводиться в фоновому режимі.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Пустий об'єкт.</returns>
    /// <exception cref="InvalidOperationException">Виникає при помилці отримання списку акаунтів.</exception>
    /// <exception cref="JsonRpcException">Виникає при помилці JSON-RPC запиту.</exception>
    /// <exception cref="OperationCanceledException">Виникає, якщо операцію скасовано.</exception>

    Task<SyncAccountsResponse> SyncAccount(CancellationToken cancellationToken = default);

}