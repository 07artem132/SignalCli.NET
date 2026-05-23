namespace SignalCli.Utilities;

/// <summary>
/// Атомарний лічильник для генерації монотонних ідентифікаторів (наприклад, JSON-RPC id).
/// </summary>
/// <param name="seed">Початкове значення для лічильника.</param>
/// <remarks>
/// A.8: при переповненні int32 повертається до від’ємного діапазону через
/// <see langword="unchecked"/>-каст. Для request-id (рядкове представлення) це нормально:
/// важлива лише унікальність у межах активних запитів, а не монотонність.
/// </remarks>
internal class AtomicCounter(long seed = 0)
{
    private long _seed = seed;

    /// <summary>
    /// Атомарно збільшує лічильник на одиницю та повертає нове значення.
    /// </summary>
    /// <returns>Нове значення лічильника (int32; може wrap-around-итися без винятку).</returns>
    public int Increment() => unchecked((int)Interlocked.Increment(ref _seed));
}
