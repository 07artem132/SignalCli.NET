using SignalCli.Exceptions;
using SignalCli.Models.Signal.Groups;

namespace SignalCli.Interfaces.Signal;

/// <summary>
/// Сервіс для роботи з групами Signal.
/// </summary>
/// <remarks>
/// Надає методи для отримання списку груп та керування ними.
/// </remarks>
public interface ISignalGroups
{
    /// <summary>
    /// Отримує список груп для вказаного акаунту.
    /// </summary>
    /// <param name="account">Ідентифікатор акаунту (номер телефону).</param>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Список груп з детальною інформацією.</returns>
    /// <exception cref="ArgumentNullException">Виникає, якщо account дорівнює null або порожній.</exception>
    /// <exception cref="InvalidOperationException">Виникає при помилці отримання списку груп.</exception>
    /// <exception cref="JsonRpcException">Виникає при помилці JSON-RPC запиту.</exception>
    /// <exception cref="OperationCanceledException">Виникає, якщо операцію скасовано.</exception>
    /// <example>
    /// <code>
    /// var groups = await signalGroups.ListGroups("+380501234567", cancellationToken);
    /// foreach (var group in groups)
    /// {
    ///     Console.WriteLine($"Група: {group.Name}, Учасників: {group.Members.Count}");
    /// }
    /// </code>
    /// </example>
    Task<ListGroupsResponse> ListGroups(
        string account,
        CancellationToken cancellationToken = default
    );
}