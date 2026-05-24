using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace SignalCli.Diagnostics;

/// <summary>
/// post-modernize-tuning §11 (audit N1/N2): єдиний фасад для distributed-tracing (<see cref="ActivitySource"/>)
/// та metrics (<see cref="Meter"/>) бібліотеки SignalCli.NET.
/// </summary>
/// <remarks>
/// <para>
/// <b>Споживання.</b> Викликачі підписуються на джерела через OpenTelemetry:
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithTracing(t =&gt; t.AddSource("SignalCli.NET"))
///     .WithMetrics(m =&gt; m.AddMeter("SignalCli.NET"));
/// </code>
/// </para>
/// <para>
/// <b>Privacy invariant</b> (CLAUDE.md rule #1): значення тегів у <c>Activity</c> та
/// <c>Meter</c>-інструментах — тільки method-names, status enums, integer IDs, durations,
/// exception type names. <b>Ніколи</b> — тіло повідомлення, номер телефону, шлях до файлу
/// вкладення. Privacy guard tests (<c>ActivityTagPrivacyTests</c>, <c>MeterTagPrivacyTests</c>)
/// перевіряють це через literal-substring asserts на seed-PII.
/// </para>
/// <para>
/// <b>Best practice</b> (Microsoft *Adding distributed tracing instrumentation — best practices*):
/// один <see cref="ActivitySource"/> на бібліотеку, статичне поле, ієрархічна назва, версія.
/// </para>
/// </remarks>
internal static class SignalCliDiagnostics
{
    /// <summary>Assembly-name + assembly-version (наприклад, "SignalCli.NET" + "2.1.0").</summary>
    private const string SourceName = "SignalCli.NET";

    private static readonly string AssemblyVersion =
        typeof(SignalCliDiagnostics).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    /// <summary>
    /// Єдине джерело <see cref="Activity"/>-ів для всієї бібліотеки. Без активного слухача
    /// <c>StartActivity</c> повертає <c>null</c> — нульовий runtime-cost.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(SourceName, AssemblyVersion);

    /// <summary>
    /// Єдиний <see cref="Meter"/> для метрик бібліотеки. Інструменти створюються нижче;
    /// без слухача (<c>MeterListener</c> / OTel exporter) — нульова видима витрата.
    /// </summary>
    public static readonly Meter Meter = new(SourceName, AssemblyVersion);

    // ---- Counters / histograms ----
    // post-modernize-tuning §11.B (audit N2). Bounded tag-cardinality (≤3 individually-specified
    // tags per call — allocation-free per Microsoft *Multi-dimensional metrics*).

    /// <summary>
    /// JSON-RPC requests sent to signal-cli. Tags: `method` (string), `status` ∈
    /// {`ok`, `timeout`, `error`}.
    /// </summary>
    public static readonly Counter<long> RpcRequests = Meter.CreateCounter<long>(
        "signalcli.rpc.requests",
        unit: "{request}",
        description: "JSON-RPC requests sent to signal-cli");

    /// <summary>
    /// JSON-RPC round-trip duration. Tag: `method`. Recorded in milliseconds.
    /// </summary>
    public static readonly Histogram<double> RpcDuration = Meter.CreateHistogram<double>(
        "signalcli.rpc.duration",
        unit: "ms",
        description: "JSON-RPC round-trip time in milliseconds");

    /// <summary>
    /// signal-cli process restarts. Tag: `trigger` ∈ {`force`, `crash`, `health`}.
    /// </summary>
    public static readonly Counter<long> ProcessRestarts = Meter.CreateCounter<long>(
        "signalcli.process.restarts",
        unit: "{restart}",
        description: "signal-cli process restarts by trigger");

    /// <summary>
    /// Channel-overflow drops in <c>SignalEventService</c>. Tag: `event_type` ∈
    /// {`text`, `reaction`, `attachment`, `sticker`, `typing`, `receipt`, `sync`,
    /// `quote`, `edit`, `remote_delete`}. Replaces the private `_droppedCount` field.
    /// </summary>
    public static readonly Counter<long> EventsDropped = Meter.CreateCounter<long>(
        "signalcli.events.dropped",
        unit: "{event}",
        description: "Channel overflows (DropOldest evictions) by event type");

    /// <summary>
    /// post-modernize-tuning §11.B.6 (audit N2): callback-провайдер для
    /// `signalcli.subscriptions.active` ObservableGauge. <c>SignalEventService</c>
    /// інжектить тут реальну функцію при ініціалізації (через
    /// <see cref="SetActiveSubscriptionsProvider"/>); до того — gauge репортує 0.
    /// </summary>
    private static Func<int>? s_activeSubscriptionsProvider;

    /// <summary>
    /// Currently active receive subscriptions (gauge — поточне значення, не cumulative).
    /// Без тегів — кардинальність 1.
    /// </summary>
    public static readonly ObservableGauge<int> ActiveSubscriptions = Meter.CreateObservableGauge(
        "signalcli.subscriptions.active",
        () => s_activeSubscriptionsProvider?.Invoke() ?? 0,
        description: "Currently active receive subscriptions");

    /// <summary>
    /// Реєструє callback для <see cref="ActiveSubscriptions"/>. Викликається
    /// <c>SignalEventService</c>-ом у конструкторі/StartAsync. Не thread-safe —
    /// очікується одна реєстрація на process (синглтон-сервіс через DI).
    /// </summary>
    internal static void SetActiveSubscriptionsProvider(Func<int> provider)
        => s_activeSubscriptionsProvider = provider;

    // ---- Activity helpers ----
    // post-modernize-tuning §11.A (audit N1). Activity names use hierarchical
    // dot-separated naming for consumer-side filtering.

    /// <summary>RPC client activity name prefix; full name = `rpc.{method}`.</summary>
    public const string RpcActivityNamePrefix = "rpc.";

    /// <summary>Process lifecycle activity names (§11.A.3).</summary>
    public const string ProcessStartActivityName = "signalcli.process.start";
    public const string ProcessExitedActivityName = "signalcli.process.exited";
    public const string ForceRestartActivityName = "signalcli.force_restart";

    /// <summary>Health-check ping activity name (§11.A.4).</summary>
    public const string HealthCheckPingActivityName = "signalcli.healthcheck.ping";

    /// <summary>Subscribe/unsubscribe activity names (§11.A.5).</summary>
    public const string SubscribeActivityName = "signalcli.subscribe";
    public const string UnsubscribeActivityName = "signalcli.unsubscribe";
}
