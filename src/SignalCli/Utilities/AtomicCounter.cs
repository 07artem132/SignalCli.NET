namespace SignalCli.Utilities;

/// <summary>
/// Атомарний лічильник, який забезпечує потокобезпечне інкрементування значення.
/// </summary>
/// <param name="seed">Початкове значення для лічильника.</param>
internal class AtomicCounter(long seed = 0)
{
    private long _seed = seed;

    /// <summary>
    /// Атомарно збільшує лічильник на одиницю та повертає нове значення.
    /// Якщо значення перевищує int.MaxValue, відбувається скидання до початкового стану.
    /// </summary>
    /// <returns>Нове значення лічильника як ціле число.</returns>
    public int Increment()
    {
        var next = Interlocked.Increment(ref _seed);
        if (next <= int.MaxValue) return (int)next;
        Interlocked.CompareExchange(ref _seed, 0, next);
        return (int)(next % int.MaxValue);
    }
}