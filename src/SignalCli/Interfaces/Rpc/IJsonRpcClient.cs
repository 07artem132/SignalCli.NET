namespace SignalCli.Interfaces.Rpc;

/// <summary>
/// Клієнт для взаємодії з JSON-RPC сервером.
/// </summary>
/// <remarks>
/// Об'єднує функціональність відправки запитів (<see cref="IJsonRpcSender"/>),
/// отримання нотифікацій (<see cref="IJsonRpcNotificationReceiver"/>)
/// та асинхронного звільнення ресурсів (<see cref="IAsyncDisposable"/>).
/// <para>
/// A.6: <see cref="IDisposable"/> навмисно НЕ успадковано — JsonRpcClient має
/// асинхронний cleanup (зупинка stdout/stderr reader-циклів), а синхронний
/// <c>Dispose()</c>-fallback через <c>GetAwaiter().GetResult()</c> — класичний
/// deadlock-вектор. DI-контейнер коректно викликає <see cref="IAsyncDisposable.DisposeAsync"/>;
/// сторонні споживачі мають користуватись <c>await using</c>.
/// </para>
/// </remarks>
public interface IJsonRpcClient :
    IJsonRpcSender,
    IJsonRpcNotificationReceiver,
    IAsyncDisposable
{
}
