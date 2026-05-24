using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Reactive.Subjects;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SignalCli.Diagnostics;
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
/// observability-counter-assertions (CHANGELOG [4.0] pending follow-up): за CLAUDE.md
/// "Future development guardrails" вимога — privacy-guards perевіряють відсутність PII,
/// але не factual increment counter'ів. Цей файл закриває цю прогалину для трьох
/// найважливіших instrument'ів (rpc.duration, events.dropped, process.restarts) через
/// MeterListener-based capture з тагами та точним count'ом.
/// </summary>
public sealed class ObservabilityCounterTests : IDisposable
{
    private readonly Lock _captureLock = new();
    private readonly List<(string Instrument, double Value, IReadOnlyList<KeyValuePair<string, object?>> Tags)> _measurements = [];
    private readonly MeterListener _meterListener;

    public ObservabilityCounterTests()
    {
        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "SignalCli.NET")
                    listener.EnableMeasurementEvents(instrument);
            },
        };
        _meterListener.SetMeasurementEventCallback<long>((instr, val, tags, _) =>
        {
            var snap = tags.ToArray();
            lock (_captureLock) { _measurements.Add((instr.Name, val, snap)); }
        });
        _meterListener.SetMeasurementEventCallback<double>((instr, val, tags, _) =>
        {
            var snap = tags.ToArray();
            lock (_captureLock) { _measurements.Add((instr.Name, val, snap)); }
        });
        _meterListener.Start();
    }

    private List<(string Instrument, double Value, IReadOnlyList<KeyValuePair<string, object?>> Tags)> Snapshot()
    {
        lock (_captureLock) { return [.. _measurements]; }
    }

    public void Dispose() => _meterListener.Dispose();

    // ─────────────────────────────────────────────────────────────────────────────
    // Counter #1 — signalcli.rpc.duration (Histogram<double>) on happy path
    // Pattern за зразком JsonRpcClientTests.InvokeMethodAsync_WhenResponseArrives.
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RpcDuration_OnSuccessRoundTrip_RecordsPositiveDurationAndOkStatus()
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
            RequestTimeoutSeconds = 60
        };
        var client = new JsonRpcClient(loggerMock.Object, streamProviderMock.Object, options, null);

        var inputWriter = new StreamWriter(new MemoryStream());
        var outputReader = new StreamReader(new MemoryStream());
        var errorReader = new StreamReader(new MemoryStream());
        current = new StreamPair(inputWriter, outputReader, errorReader);
        subject.OnNext(current);

        var invokeTask = client.InvokeMethodAsync(
            "someMethod",
            new TestProbeRequest(),
            TestSerializationContext.Default.TestProbeRequest,
            TestSerializationContext.Default.TestProbeResponse);

        // Inject response synchronously through reflection helper (existing pattern from
        // JsonRpcClientTests.CallProcessMessage).
        var json = @"{ ""id"": ""1"", ""result"": { ""foo"": ""bar"" } }";
        var mi = typeof(JsonRpcClient).GetMethod(
            "ProcessMessageAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var task = (Task)mi!.Invoke(client, [json, CancellationToken.None])!;
        await task;

        await invokeTask;
        await client.DisposeAsync();

        var snap = Snapshot();

        // ⚠ Filter by method-tag — xUnit запускає тести paralleл'но, інші instancesи можуть
        // фігурити в Meter-publish'у. Унікальне ім'я методу + фільтр по tag-значенню гарантує
        // що ми бачимо саме НАШУ вимірність.
        bool MatchesMethod(IReadOnlyList<KeyValuePair<string, object?>> tags) =>
            tags.Any(t => t is { Key: "method", Value: "someMethod" });

        var durations = snap.Where(m => m.Instrument == "signalcli.rpc.duration" && MatchesMethod(m.Tags)).ToList();
        Assert.Single(durations);
        Assert.True(durations[0].Value >= 0, "Histogram value має бути ≥ 0");

        var requests = snap.Where(m => m.Instrument == "signalcli.rpc.requests" && MatchesMethod(m.Tags)).ToList();
        Assert.Single(requests);
        Assert.Equal(1L, requests[0].Value);
        Assert.Contains(requests[0].Tags, t => t is { Key: "status", Value: "ok" });
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Counter #2 — signalcli.events.dropped on channel overflow
    // ChannelCapacity=1024 + DropOldest. Прокидаємо 1025+ нотифікацій підряд через
    // dispatcher, асертимо що EventsDropped інкрементовано на дельту-overflow з
    // правильним event_type тагом.
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EventsDropped_OnChannelOverflow_IncrementsWithCorrectEventType()
    {
        var notifications = new Subject<JsonRpcNotification<SubscriptionEventArgs>>();
        var rpcClient = new Mock<IJsonRpcClient>();
        rpcClient.Setup(c => c.Notifications).Returns(notifications);
        var provider = new Mock<IJsonRpcClientProvider>();
        provider.Setup(p => p.Client).Returns(rpcClient.Object);
        var signalCli = new Mock<ISignalCliClient>();
        signalCli.Setup(c => c.InvokeMethodAsync<SubscribeReceiveParameters, JsonElement>(
                It.IsAny<string>(),
                It.IsAny<SubscribeReceiveParameters>(),
                It.IsAny<JsonTypeInfo<SubscribeReceiveParameters>>(),
                It.IsAny<JsonTypeInfo<JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.SerializeToElement(42));

        var service = new SignalEventService(
            Mock.Of<ILogger<SignalEventService>>(), provider.Object, signalCli.Object);
        await service.StartAsync(CancellationToken.None);
        var subResp = await service.SubscribeAsync("+380999000111");
        var subId = subResp.Id;

        // Прокидаємо 1100 typing-нотифікацій (channelCapacity = 1024). NO consumer на
        // _typingChannel — отже починаючи з 1025-ої кожен наступний дзвонить DropOldest.
        const int total = 1100;
        const int expectedDrops = total - 1024;
        for (var i = 0; i < total; i++)
        {
            var envelope = new JsonMessageEnvelope(
                "+380000000000", "+380000000000", "uuid-x", "src-name", 1, i, i, i,
                null, null, null, null, null, null,
                new JsonTypingMessage("STARTED", i, null));
            notifications.OnNext(new JsonRpcNotification<SubscriptionEventArgs>
            {
                JsonRpc = "2.0",
                Method = "receive",
                Params = new SubscriptionEventArgs(subId, new SignalEventArgs("+380000000000", envelope)),
            });
        }

        var snap = Snapshot();
        var drops = snap
            .Where(m => m.Instrument == "signalcli.events.dropped")
            .Where(m => m.Tags.Any(t => t is { Key: "event_type", Value: "typing" }))
            .Sum(m => m.Value);

        Assert.Equal(expectedDrops, drops);

        await service.StopAsync(CancellationToken.None);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Counter #3 — signalcli.process.restarts on force trigger
    // Використовуємо SignalCliHostedServiceTestsBase-style fixture inline для
    // simulation force-restart-сценарію та асерту counter тікнув з trigger=force.
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessRestarts_OnForceRestart_IncrementsWithForceTrigger()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "lib"));
        File.WriteAllText(Path.Combine(tempDir, "lib", "signal-cli.jar"), "fake jar");

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
                loggerMock.Object,
                runnerMock.Object,
                stateManager,
                Options.Create(opts),
                null);

            await service.StartAsync(CancellationToken.None);
            await service.ForceRestartAsync(CancellationToken.None);
            await service.StopAsync(CancellationToken.None);
            await service.DisposeAsync();

            var snap = Snapshot();
            var forceRestarts = snap
                .Where(m => m.Instrument == "signalcli.process.restarts")
                .Where(m => m.Tags.Any(t => t is { Key: "trigger", Value: "force" }))
                .Sum(m => m.Value);

            Assert.True(forceRestarts >= 1, $"Expected force-trigger restart counter ≥ 1, got {forceRestarts}");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { /* best-effort */ }
        }
    }
}
