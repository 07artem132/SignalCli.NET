using System.Reactive.Subjects;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SignalCli.Interfaces.Rpc;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models;
using SignalCli.Models.Rpc;
using SignalCli.Models.Signal;
using SignalCli.Models.Signal.Events;
using SignalCli.Models.SignalCli;
using SignalCli.Services.Rpc;
using SignalCli.Services.Signal;

namespace SignalCli.Tests;

/// <summary>
/// edge-case-followup-completion (CHANGELOG [4.0.1] honest gap-fix): закриває
/// останні 3 з 6 deferred edge-case-coverage позицій, які НЕ були покриті у
/// перший прохід 4.0.1: ForceRestart no-op states, NotificationChannelCapacity=1
/// FIFO, SubscribeAsync leader-cancellation propagation до follower.
/// </summary>
public class EdgeCaseFollowupTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // #1 — ForceRestartAsync на станах { Stopping, Stopped, NotStarted, Starting }
    // має бути no-op (log + return), а не throw / state-transition.
    // SignalCliHostedService.cs:222-246 — guard: currentState != Running && != Failed.
    // ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ProcessState.NotStarted)]
    [InlineData(ProcessState.Starting)]
    [InlineData(ProcessState.Stopping)]
    [InlineData(ProcessState.Stopped)]
    public async Task ForceRestartAsync_OnNonRestartableState_IsNoOp(ProcessState targetState)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "lib"));
        File.WriteAllText(Path.Combine(tempDir, "lib", "signal-cli.jar"), "fake");

        try
        {
            var loggerMock = new Mock<ILogger<SignalCli.Services.SignalCli.SignalCliHostedService>>();
            loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

            var procStartCount = 0;
            var runnerMock = new Mock<IProcessRunner>();
            runnerMock.Setup(r => r.StartProcessWithHandle(It.IsAny<ProcessConfig>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    procStartCount++;
                    var pMock = new Mock<IProcess>();
                    pMock.Setup(p => p.Start(It.IsAny<CancellationToken>())).Returns(true);
                    pMock.Setup(p => p.Id).Returns(procStartCount);
                    var streams = new StreamPair(
                        new StreamWriter(new MemoryStream()),
                        new StreamReader(new MemoryStream()),
                        new StreamReader(new MemoryStream()));
                    pMock.Setup(p => p.StandardInput).Returns(streams.StandardInput);
                    pMock.Setup(p => p.StandardOutput).Returns(streams.StandardOutput);
                    pMock.Setup(p => p.StandardError).Returns(streams.StandardError);
                    return (pMock.Object, streams);
                });

            var smLogger = new Mock<ILogger<SignalCli.Services.SignalCli.ProcessStateManager>>();
            smLogger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
            var stateManager = new SignalCli.Services.SignalCli.ProcessStateManager(smLogger.Object);

            var opts = new SignalCliOptions
            {
                AppHome = tempDir,
                LibDirectory = "lib",
                JavaExecutable = "java",
                MaxRestartAttempts = 5,
                RestartDelaySeconds = 0,
                StopTimeoutSeconds = 0,
                HealthCheckIntervalSeconds = 1,
                HealthCheckTimeoutSeconds = 1,
            };

            var service = new SignalCli.Services.SignalCli.SignalCliHostedService(
                loggerMock.Object, runnerMock.Object, stateManager,
                Options.Create(opts), null);

            // Поставити цільовий state напряму через ProcessStateManager, не запускаючи
            // справжній start-cycle. Це валідно бо state machine — single source of truth.
            stateManager.UpdateState(targetState);
            var stateBefore = stateManager.CurrentState;
            var startsBefore = procStartCount;

            // Act — ForceRestartAsync має одразу повернутися без винятку.
            await service.ForceRestartAsync(CancellationToken.None);

            // Assert — стан не змінився, новий процес не стартував.
            Assert.Equal(stateBefore, stateManager.CurrentState);
            Assert.Equal(startsBefore, procStartCount);

            await service.DisposeAsync();
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { /* best-effort */ }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // #2 — NotificationChannelCapacity = 1 (minimum), JsonRpcClient має зберегти
    // FIFO-порядок нотифікацій навіть на найменшій ємності каналу.
    // JsonRpcClient.cs:100 — capacity = Math.Max(1, options?.NotificationChannelCapacity ?? 1024).
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NotificationChannel_CapacityOne_DeliversInFifoOrder()
    {
        var loggerMock = new Mock<ILogger<JsonRpcClient>>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var streamProviderMock = new Mock<IStreamPairProvider>();
        var subject = new Subject<StreamPair?>();
        StreamPair? current = null;
        streamProviderMock.SetupGet(sp => sp.CurrentStreamPair).Returns(() => current);
        streamProviderMock.SetupGet(sp => sp.StreamPairChanged).Returns(subject);

        var options = new SignalCliOptions
        {
            AppHome = Path.GetTempPath(),
            JavaExecutable = string.Empty,
            LibDirectory = string.Empty,
            RequestTimeoutSeconds = 60,
            NotificationChannelCapacity = 1,
        };
        await using var client = new JsonRpcClient(loggerMock.Object, streamProviderMock.Object, options, null);

        // Stream pair потрібна, щоб reader-loop працював; ми не пишемо в неї real-stdin,
        // нотифікації прокидаємо через ProcessMessageAsync напряму (reflection).
        current = new StreamPair(
            new StreamWriter(new MemoryStream()),
            new StreamReader(new MemoryStream()),
            new StreamReader(new MemoryStream()));
        subject.OnNext(current);

        // Збираємо порядок IDs через Subscribe на client.Notifications.
        var received = new List<long>();
        var doneSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        const int total = 50;
        using var sub = client.Notifications.Subscribe(n =>
        {
            var ts = n.Params?.Result?.Envelope?.Timestamp ?? -1;
            received.Add(ts);
            if (received.Count == total) doneSignal.TrySetResult(true);
        });

        var mi = typeof(JsonRpcClient).GetMethod(
            "ProcessMessageAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        // Прокидаємо 50 нотифікацій з монотонно зростаючим timestamp.
        for (var i = 0; i < total; i++)
        {
            var line = "{\"jsonrpc\":\"2.0\",\"method\":\"receive\",\"params\":{\"subscription\":1,\"result\":{\"account\":\"+380000000000\",\"envelope\":{\"source\":\"+380000000000\",\"sourceNumber\":\"+380000000000\",\"sourceUuid\":\"u\",\"sourceName\":\"n\",\"sourceDevice\":1,\"timestamp\":" + i + ",\"serverReceivedTimestamp\":" + i + ",\"serverDeliveredTimestamp\":" + i + "}}}}";
            var task = (Task)mi!.Invoke(client, [line, CancellationToken.None])!;
            await task;
        }

        // Чекаємо доки subscriber отримає всі — fan-out з bounded channel CapacityOne
        // має повністю drain'нути (FullMode=Wait блокує producer'а доки consumer відчитає).
        var completed = await Task.WhenAny(doneSignal.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.Same(doneSignal.Task, completed);

        // Assert — FIFO порядок: timestamps мають бути 0,1,2,...,49.
        Assert.Equal(total, received.Count);
        Assert.Equal(Enumerable.Range(0, total).Select(i => (long)i), received);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // #3 — SubscribeAsync follower receives the same exception коли leader's
    // RPC-call cancel'ується (мають отримати OperationCanceledException через TCS).
    // SignalEventService.cs:242-280 — leader розв'язує `_pendingSubscribes[account]`,
    // на cancellation викидає виняток і викликає `myTcs.TrySetException(ex)`.
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SubscribeAsync_LeaderCancellation_FollowerReceivesSameException()
    {
        var rpcClient = new Mock<IJsonRpcClient>();
        var notifications = new Subject<JsonRpcNotification<SubscriptionEventArgs>>();
        rpcClient.Setup(c => c.Notifications).Returns(notifications);
        var provider = new Mock<IJsonRpcClientProvider>();
        provider.Setup(p => p.Client).Returns(rpcClient.Object);

        // RPC-mock тримає leader-call у await-stage поки не побачить cancellation.
        var rpcGate = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var signalCli = new Mock<ISignalCliClient>();
        signalCli.Setup(c => c.InvokeMethodAsync<SubscribeReceiveParameters, JsonElement>(
                It.IsAny<string>(),
                It.IsAny<SubscribeReceiveParameters>(),
                It.IsAny<JsonTypeInfo<SubscribeReceiveParameters>>(),
                It.IsAny<JsonTypeInfo<JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, SubscribeReceiveParameters, JsonTypeInfo<SubscribeReceiveParameters>, JsonTypeInfo<JsonElement>, CancellationToken, TimeSpan?>(
                async (_, _, _, _, ct, _) =>
                {
                    // Реагуємо на cancellation так, як це робить справжній JsonRpcClient.
                    using var reg = ct.Register(() => rpcGate.TrySetCanceled(ct));
                    return await rpcGate.Task.ConfigureAwait(false);
                });

        var service = new SignalEventService(
            Mock.Of<ILogger<SignalEventService>>(), provider.Object, signalCli.Object);
        await service.StartAsync(CancellationToken.None);

        using var leaderCts = new CancellationTokenSource();
        const string account = "+380999000111";

        // Leader заявляється першим.
        var leaderTask = service.SubscribeAsync(account, leaderCts.Token);

        // Чекаємо ~50ms щоб leader зайняв _pendingSubscribes slot.
        await Task.Delay(50);

        // Follower підходить другим — він застрягне на той самий TCS-placeholder.
        var followerTask = service.SubscribeAsync(account, CancellationToken.None);

        // Cancel leader's token → RPC має cancel'нутися → exception propagates до обох.
        leaderCts.Cancel();

        // Assert обидва кидають OperationCanceledException (або derived TaskCanceledException).
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => leaderTask);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => followerTask);

        await service.StopAsync(CancellationToken.None);
    }
}
