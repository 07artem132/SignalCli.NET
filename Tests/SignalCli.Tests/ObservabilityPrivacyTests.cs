using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reactive.Subjects;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Diagnostics;
using SignalCli.Interfaces.Rpc;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.Rpc;
using SignalCli.Models.Signal;
using SignalCli.Models.Signal.Events;
using SignalCli.Services.Signal;

namespace SignalCli.Tests;

/// <summary>
/// post-modernize-tuning §11.A.6 + §11.B.8 + §11.D.1 (audit N1/N2): privacy invariant
/// guard. Перехоплюємо КОЖНИЙ Activity-tag і КОЖНУ Meter-measurement, прокидаємо
/// synthetic seed-PII через SubscribeAsync і дисетчер нотифікацій, потім перевіряємо,
/// що жодне значення тегу не містить літерально PII.
/// </summary>
/// <remarks>
/// Microsoft *Adding distributed tracing instrumentation — best practices* і
/// *Multi-dimensional metrics* стверджують, що tag values мають низьку кардинальність
/// і не повинні містити PII. Ці тести роблять інваріант enforce'абельним: будь-який
/// майбутній PR, що тихо додасть `account` або `message body` як tag, спричинить
/// фейл одного з literal-substring-asserts'ів.
/// </remarks>
public sealed class ObservabilityPrivacyTests : IDisposable
{
    private const string SeedPhone = "+380999000111";
    private const string SeedBody = "ПРИВІТ-АУДИТ-СЕКРЕТНЕ-ТІЛО-2026";
    private const string SeedPath = "/home/audit/secret-attachment.bin";

    private readonly List<Activity> _capturedActivities = [];
    private readonly List<(string Instrument, object? Value, IReadOnlyList<KeyValuePair<string, object?>> Tags)> _capturedMeasurements = [];
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;

    public ObservabilityPrivacyTests()
    {
        // §11.A.6: ActivityListener — захоплюємо всі Activity з нашого джерела й зберігаємо
        // повний tag-snapshot після Stop'а (StartActivity не має тегів ще на момент створення).
        _activityListener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == "SignalCli.NET",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => _capturedActivities.Add(activity),
        };
        ActivitySource.AddActivityListener(_activityListener);

        // §11.B.8: MeterListener — захоплюємо всі long/double-measurements з нашого Meter'а.
        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "SignalCli.NET")
                    listener.EnableMeasurementEvents(instrument);
            },
        };
        _meterListener.SetMeasurementEventCallback<long>(OnLongMeasurement);
        _meterListener.SetMeasurementEventCallback<double>(OnDoubleMeasurement);
        _meterListener.Start();
    }

    private void OnLongMeasurement(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        => _capturedMeasurements.Add((instrument.Name, value, tags.ToArray()));

    private void OnDoubleMeasurement(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        => _capturedMeasurements.Add((instrument.Name, value, tags.ToArray()));

    private static SignalEventService CreateEventService(out Subject<JsonRpcNotification<SubscriptionEventArgs>> notifications, int subId = 7)
    {
        notifications = new Subject<JsonRpcNotification<SubscriptionEventArgs>>();
        var rpcClient = new Mock<IJsonRpcClient>();
        rpcClient.Setup(c => c.Notifications).Returns(notifications);
        var provider = new Mock<IJsonRpcClientProvider>();
        provider.Setup(p => p.Client).Returns(rpcClient.Object);
        var signalCli = new Mock<ISignalCliClient>();
        signalCli.Setup(c => c.InvokeMethodAsync<JsonElement, SubscribeReceiveParameters>(
                It.IsAny<string>(), It.IsAny<SubscribeReceiveParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.SerializeToElement(subId));
        return new SignalEventService(Mock.Of<ILogger<SignalEventService>>(), provider.Object, signalCli.Object);
    }

    [Fact]
    public async Task ActivityTags_OnSubscribeAsync_DoNotContainSeedPii()
    {
        var service = CreateEventService(out _);
        await service.StartAsync(CancellationToken.None);

        await service.SubscribeAsync(SeedPhone);

        // Activity для subscribe-операції має зʼявитися (ми зареєстрували listener).
        Assert.Contains(_capturedActivities, a => a.OperationName == SignalCliDiagnostics.SubscribeActivityName);

        // Перевіряємо КОЖЕН тег КОЖНОЇ activity на присутність seed-PII.
        AssertNoPiiInActivityTags();
    }

    [Fact]
    public async Task ActivityTags_OnDispatch_DoNotContainSeedPiiFromEnvelope()
    {
        var service = CreateEventService(out var notifications);
        await service.StartAsync(CancellationToken.None);
        await service.SubscribeAsync(SeedPhone);

        // Прокидаємо нотифікацію з seed-тілом і seed-source через диспетчер.
        var envelope = new JsonMessageEnvelope(
            SeedPhone, SeedPhone, "uuid-x", SeedPhone, 1, 1, 1, 1,
            // JsonDataMessage(Timestamp, Message, ...) — Timestamp перший.
            new JsonDataMessage(1, SeedBody, null, null, null, null, null, null, null, null, null, null, null, null, null, null),
            null, null, null, null, null, null);
        notifications.OnNext(new JsonRpcNotification<SubscriptionEventArgs>
        {
            JsonRpc = "2.0",
            Method = "receive",
            Params = new SubscriptionEventArgs(7, new SignalEventArgs(SeedPhone, envelope)),
        });

        // Перевіряємо КОЖЕН тег — навіть якщо щось додаткове було покладено між dispatch'ами.
        AssertNoPiiInActivityTags();
    }

    [Fact]
    public async Task MeterTags_OnSubscribe_DoNotContainSeedPii()
    {
        var service = CreateEventService(out _);
        await service.StartAsync(CancellationToken.None);

        await service.SubscribeAsync(SeedPhone);

        // У SubscribeAsync ми не пишемо власних counter'ів, але диспетчер може дропнути
        // події у канал → counter. Незалежно від того — перевіряємо все що захопили.
        AssertNoPiiInMeterTags();
    }

    [Fact]
    public void MeterTagValues_AreOnlyKnownEnumLiterals()
    {
        // Викликаємо counter напряму через відомі тегування — переконуємося, що
        // canonical-style теги проходять без issue.
        SignalCliDiagnostics.RpcRequests.Add(1,
            new KeyValuePair<string, object?>("method", "send"),
            new KeyValuePair<string, object?>("status", "ok"));
        SignalCliDiagnostics.ProcessRestarts.Add(1, new KeyValuePair<string, object?>("trigger", "crash"));
        SignalCliDiagnostics.EventsDropped.Add(1, new KeyValuePair<string, object?>("event_type", "text"));

        // Усі вказані теги — низько-кардинальні enum-значення; жоден не містить seed-PII.
        AssertNoPiiInMeterTags();

        // І додатково перевіряємо, що теги дійсно з low-cardinality enum-набору.
        var knownTagKeys = new HashSet<string> { "method", "status", "trigger", "event_type" };
        foreach (var (_, _, tags) in _capturedMeasurements)
        {
            foreach (var tag in tags)
            {
                Assert.Contains(tag.Key, knownTagKeys);
                Assert.NotNull(tag.Value);
            }
        }
    }

    private void AssertNoPiiInActivityTags()
    {
        foreach (var activity in _capturedActivities)
        {
            foreach (var tag in activity.TagObjects)
            {
                var value = tag.Value?.ToString() ?? "";
                Assert.DoesNotContain(SeedPhone, value);
                Assert.DoesNotContain(SeedBody, value);
                Assert.DoesNotContain(SeedPath, value);
            }
            // Status-description теж під суворим scrutiny — там може бути message exception'а.
            if (activity.StatusDescription is not null)
            {
                Assert.DoesNotContain(SeedPhone, activity.StatusDescription);
                Assert.DoesNotContain(SeedBody, activity.StatusDescription);
                Assert.DoesNotContain(SeedPath, activity.StatusDescription);
            }
        }
    }

    private void AssertNoPiiInMeterTags()
    {
        foreach (var (_, _, tags) in _capturedMeasurements)
        {
            foreach (var tag in tags)
            {
                var key = tag.Key;
                var value = tag.Value?.ToString() ?? "";
                Assert.DoesNotContain(SeedPhone, key);
                Assert.DoesNotContain(SeedPhone, value);
                Assert.DoesNotContain(SeedBody, key);
                Assert.DoesNotContain(SeedBody, value);
                Assert.DoesNotContain(SeedPath, key);
                Assert.DoesNotContain(SeedPath, value);
            }
        }
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        _meterListener.Dispose();
    }
}
