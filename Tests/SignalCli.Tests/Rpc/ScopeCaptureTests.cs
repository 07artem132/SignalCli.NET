using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models;
using SignalCli.Models.SignalCli;
using SignalCli.Services.Rpc;

namespace SignalCli.Tests.Rpc;

/// <summary>
/// post-modernize-tuning §5.12 (audit N13): <see cref="JsonRpcClient.InvokeMethodAsync"/> має
/// відкривати structured scope із <c>RpcMethod</c> + <c>RpcRequestId</c>; кожен вкладений
/// log-запис (включно з усіма <c>JsonRpcClientLog.*</c>-генерованими методами) має
/// успадковувати ці scope-properties.
/// </summary>
public sealed class ScopeCaptureTests
{
    [Fact]
    public async Task InvokeMethodAsync_OpensScope_WithRpcMethod_AndRpcRequestId()
    {
        // Arrange — `FakeLogger<T>` capture'ить кожен log-entry разом із активними scope'ами.
        var fakeLogger = new FakeLogger<JsonRpcClient>();
        var streamProviderMock = new Mock<IStreamPairProvider>();
        var streamPairSubject = new Subject<StreamPair?>();
        var inputWriter = new StreamWriter(new MemoryStream());
        var outputReader = new StreamReader(new MemoryStream());
        var errorReader = new StreamReader(new MemoryStream());
        var pair = new StreamPair(inputWriter, outputReader, errorReader);
        streamProviderMock.Setup(p => p.CurrentStreamPair).Returns(pair);
        streamProviderMock.Setup(p => p.StreamPairChanged).Returns(streamPairSubject);

        var config = new Config
        {
            AppHome = Path.GetTempPath(),
            JavaExecutable = string.Empty,
            LibDirectory = string.Empty,
            // 2s — достатньо щоб SendRequestAsync завершився, але швидкий timeout щоб тест не висів.
            RequestTimeoutSeconds = 2,
        };
        await using var client = new JsonRpcClient(
            fakeLogger,
            streamProviderMock.Object,
            config.ToOptions());

        // Act — запит без відповіді, дочекаємось timeout'у (швидкий — 2с).
        var invokeTask = client.InvokeMethodAsync(
            "scopeProbe",
            new TestProbeRequest(),
            TestSerializationContext.Default.TestProbeRequest,
            TestSerializationContext.Default.TestProbeResponse);
        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await invokeTask.WaitAsync(TimeSpan.FromSeconds(10)));

        // Assert — серед log-записів має бути хоч один із scope-property RpcMethod="scopeProbe".
        var entries = fakeLogger.Collector.GetSnapshot();
        Assert.NotEmpty(entries);

        // Scope може приїхати у двох shape'ах залежно від ML-pipeline:
        // (а) IReadOnlyList<KeyValuePair<string,object?>> — стандартна структура з MEL;
        // (б) Dictionary<string,object> — як ми передали в BeginScope напряму.
        static IEnumerable<KeyValuePair<string, object?>> EnumerateKvps(object? scope) => scope switch
        {
            IReadOnlyList<KeyValuePair<string, object?>> list => list,
            IEnumerable<KeyValuePair<string, object>> dict => dict.Select(
                kvp => new KeyValuePair<string, object?>(kvp.Key, kvp.Value)),
            _ => [],
        };

        var allScopeKvps = entries
            .SelectMany(rec => rec.Scopes)
            .SelectMany(EnumerateKvps)
            .ToList();

        var methods = allScopeKvps
            .Where(kvp => kvp.Key == "RpcMethod")
            .Select(kvp => kvp.Value?.ToString())
            .ToList();
        Assert.NotEmpty(methods);
        Assert.Contains("scopeProbe", methods);

        var ids = allScopeKvps
            .Where(kvp => kvp.Key == "RpcRequestId")
            .Select(kvp => kvp.Value?.ToString())
            .ToList();
        Assert.NotEmpty(ids);
        Assert.All(ids, id => Assert.False(string.IsNullOrEmpty(id)));
    }
}
