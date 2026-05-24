## ADDED Requirements

### Requirement: Library exposes a single `ActivitySource` for distributed tracing

The library SHALL expose `static readonly ActivitySource SignalCliDiagnostics.ActivitySource = new("SignalCli.NET", AssemblyVersion)` (Microsoft *Adding distributed tracing instrumentation — best practices*: one source per library, hierarchical name, version-tagged). The source SHALL create activities only when listeners are present — there SHALL be zero overhead when no listener is attached (`ActivitySource.StartActivity` returns null and `activity?.SetTag(...)` no-ops).

#### Scenario: Consumer adds the source to OpenTelemetry

- **GIVEN** a consumer's host that calls `builder.Services.AddOpenTelemetry().WithTracing(t => t.AddSource("SignalCli.NET").AddConsoleExporter())`
- **WHEN** the library executes any instrumented call (`InvokeMethodAsync`, process lifecycle, subscribe, …)
- **THEN** each call produces an `Activity` with a `signalcli.*` name visible in the consumer's tracing pipeline
- **AND** the source name `SignalCli.NET` matches `AddSource(…)` in the consumer code

#### Scenario: No listener attached — zero overhead

- **GIVEN** a consumer without any `ActivityListener` / OTel exporter
- **WHEN** the library executes `InvokeMethodAsync` 1 000 times
- **THEN** allocation traces show zero `Activity` instances allocated by the library
- **AND** the `StartActivity` return value is `null` at every site

### Requirement: Each instrumented operation has a documented activity name and tag set

The following operations SHALL produce activities with these exact names and tag schemas. Tag values SHALL be **low-cardinality, non-PII** — method names, status enums, integer ids, durations only. Phone numbers, message bodies, and attachment payloads SHALL NOT appear as tag values (this is CLAUDE.md rule #1 made explicit for the tracing surface).

| Activity name | Site | Required tags | Forbidden as tag values |
|---|---|---|---|
| `signalcli.rpc.{method}` (e.g. `signalcli.rpc.send`) | `JsonRpcClient.InvokeMethodAsync` | `signal.rpc.method` (string), `signal.rpc.request_id` (string) | request params, response body |
| `signalcli.process.start` | `SignalCliHostedService.StartProcessInternalAsyncNoLock` | `signal.process.executable` (basename only) | full file paths |
| `signalcli.process.exited` | `SignalCliHostedService.OnProcessExitedAsync` | `signal.process.exit_code` (int, when available) | — |
| `signalcli.force_restart` | `SignalCliHostedService.ForceRestartAsync` | `signal.restart.attempt` (int) | — |
| `signalcli.healthcheck.ping` | `SignalCliHealthMonitor.PingCliAsync` | `signal.healthcheck.outcome` ∈ {`ok`,`timeout`,`failed`,`no_stream_pair`} | response body |
| `signalcli.subscribe` / `signalcli.unsubscribe` | `SignalEventService` | `signal.subscription.id` (int) | account phone number |

Errors SHALL be reported via `activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name)` — exception **type name only**, never the message (messages are Ukrainian and may carry RPC error text).

#### Scenario: An RPC call faults with TimeoutException

- **GIVEN** a `JsonRpcClient.InvokeMethodAsync("send", …)` call that times out
- **WHEN** the call faults
- **THEN** the activity `signalcli.rpc.send` carries `Status = Error`, `StatusDescription = "TimeoutException"`
- **AND** no part of the original `Send`-request body appears in any tag

#### Scenario: Privacy guard test

- **WHEN** `Tests/SignalCli.Tests/Observability/ActivityTagPrivacyTests.cs` runs an end-to-end synthetic message roundtrip
- **THEN** every captured tag value is one of: a known method name from the signal-cli enum set, a known outcome enum string, an integer id, a duration in milliseconds, an exception type name
- **AND** the test asserts that the test's seed phone (`+380501234567`) and seed message body (`"Привіт-аудит-2026"`) never appear anywhere in tag values

### Requirement: Library exposes a single `Meter` with documented counters and histograms

The library SHALL expose `static readonly Meter SignalCliDiagnostics.Meter = new("SignalCli.NET", AssemblyVersion)` (Microsoft *Metric APIs comparison — `System.Diagnostics.Metrics` is the recommended default for new libraries*). The following instruments SHALL be created, each with units and descriptions:

| Instrument | Type | Tags (low-cardinality, bounded) | Source / replaces |
|---|---|---|---|
| `signalcli.rpc.requests` | `Counter<long>` | `method` (string), `status` ∈ {`ok`,`timeout`,`error`} | new |
| `signalcli.rpc.duration` | `Histogram<double>` (ms) | `method` | new |
| `signalcli.process.restarts` | `Counter<long>` | `trigger` ∈ {`force`,`crash`,`health`} | new |
| `signalcli.events.dropped` | `Counter<long>` | `event_type` ∈ {`text`,`reaction`,`attachment`,`sticker`,`typing`,`receipt`,`sync`,`quote`,`edit`,`remote_delete`} | **replaces** `SignalEventService._droppedCount` |
| `signalcli.subscriptions.active` | `ObservableGauge<int>` | — (no tags) | new |

Every `Counter.Add` / `Histogram.Record` call site SHALL pass **at most 3 individually-specified tags** ([Multi-dimensional metrics — API is allocation-free for `Add`/`Record` with three or fewer tags](https://learn.microsoft.com/dotnet/core/diagnostics/metrics-instrumentation#multi-dimensional-metrics)). No PII appears as a tag value.

#### Scenario: Consumer scrapes Prometheus from the meter

- **GIVEN** a consumer that calls `builder.Services.AddOpenTelemetry().WithMetrics(m => m.AddMeter("SignalCli.NET").AddPrometheusExporter())`
- **WHEN** signal-cli is restarted 3 times during a session
- **THEN** `signalcli_process_restarts_total{trigger="crash"}` reports 3 (or split across triggers if mixed)
- **AND** `signalcli_rpc_requests_total` reflects the cumulative RPC call count tagged by method and status

#### Scenario: Counter replaces the private field

- **WHEN** `SignalEventService` drops events through the bounded channel
- **THEN** the only accounting is `SignalCliDiagnostics.EventsDropped.Add(1, new KeyValuePair<string, object?>("event_type", "text"))`
- **AND** there is no private `_droppedCount` field on `SignalEventService` (eliminated together with the `% 100 == 1` sampler)

#### Scenario: No listener attached — minimal overhead

- **GIVEN** a consumer without any `MeterListener` / OTel exporter
- **WHEN** the library increments `signalcli.rpc.requests` 1 000 times
- **THEN** allocation traces show zero KeyValuePair[] allocations from `Counter.Add` (≤3 tags path is allocation-free)

### Requirement: `IHealthCheck` adapter ships as an optional package

The core library SHALL NOT take a `Microsoft.Extensions.Diagnostics.HealthChecks` dependency. A separate package `SignalCli.NET.HealthChecks` SHALL provide `SignalCliHealthCheck : IHealthCheck` and `IHealthChecksBuilder.AddSignalCliHealthCheck(...)` ([Distribute a health check library](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0#distribute-a-health-check-library)).

The health check SHALL read process state from `ProcessStateManager.CurrentState` and the last ping outcome from a new `internal` accessor on `SignalCliHealthMonitor.LastPingResult: (bool Ok, DateTimeOffset At)?`. `data`-bag SHALL include `state`, `last_ping_at`, `restart_count` for diagnostic surfaces; values SHALL NOT include any account / phone / message content.

#### Scenario: ASP.NET Core consumer wires the health check

- **GIVEN** a consumer adds `services.AddSignalCli(opts => ...).AddSignalCliHealthCheck()` and `app.MapHealthChecks("/healthz")`
- **WHEN** the consumer makes a GET request to `/healthz`
- **THEN** the response reports `Healthy` when `ProcessState.Running` and the last health-monitor ping was OK
- **AND** the response reports `Unhealthy` when `ProcessState ∈ {Failed, Stopped}`
- **AND** the response body's diagnostic payload does NOT include any phone number / message content / file paths

#### Scenario: Core library has no health-checks dependency

- **WHEN** `dotnet list package` runs against `src/SignalCli/SignalCli.csproj`
- **THEN** neither `Microsoft.Extensions.Diagnostics.HealthChecks` nor `.Abstractions` appear
- **AND** the optional package `SignalCli.NET.HealthChecks` is the only place that pulls them in

### Requirement: Observability surface is documented in README and `docs/cloud-development.md`

After this capability lands, both `README.md` and `docs/cloud-development.md` SHALL show a minimum-viable enable snippet for `ActivitySource` ("SignalCli.NET") and `Meter` ("SignalCli.NET") with OpenTelemetry, plus a one-paragraph note on the optional health-checks package.

#### Scenario: New consumer searches "OpenTelemetry" in the repo

- **WHEN** they grep `README.md`
- **THEN** they find a working `AddSource("SignalCli.NET")` + `AddMeter("SignalCli.NET")` example
- **AND** they find a pointer to the optional `SignalCli.NET.HealthChecks` package
