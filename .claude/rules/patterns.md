---
paths:
  - "src/SignalCli/**"
  - "src/SignalCli.HealthChecks/**"
---

# Established patterns (these are the law — do not regress)

The patterns below are not aspirational. They were rolled out across the codebase in the `agent-friendly-modernization` change (2.1.0) and the test suite enforces them. New code MUST follow these; edits to existing code that touches an affected area MUST keep them. Re-introducing the old shape is a regression.

## Async, cancellation, naming

- **Async suffix:** every `Task`/`ValueTask`-returning method has `Async` in its name. The one historical exception, `ISignalCliClient.Version()`, exists only as `[Obsolete]` shim delegating to `VersionAsync()`.
- **`CancellationToken cancellationToken = default` as the last explicit parameter.** Even when a paired `*Options` record carries a `CancellationToken` field (deprecated), the parameter on the method is the discoverable surface. Link both inside via `CreateLinkedTokenSource` — see `SignalMessage.LinkTokens` for the canonical helper.
- **`TaskCompletionSource<T>.TrySetCanceled(token)` always carries a token.** `JsonRpcClient` keeps `_disposeCts` for `DisposeAsync`-time cancellation and a transient cancelled CTS for stream-pair-change. Never `TrySetCanceled()` without an argument.

## Configuration: `IOptions<SignalCliOptions>` only

- **Configurable knobs go in `SignalCliOptions`**, not in `Config`. `Config` is `[Obsolete]`-shimmed to `Action<Config>?` `AddSignalCli` overload — do not extend it.
- **Properties are `get; set;`** (not `init`-only). Microsoft.Extensions.Options is a stateful pattern: the framework creates the instance via `Activator.CreateInstance` and mutates it through your `Action<TOptions>.Configure`-delegate and `Bind(IConfiguration)`. `init` makes both reflection-based `Bind` and the `Configure`-delegate ergonomically painful — we learned this the hard way and reverted. Immutability is enforced socially (no setter calls after registration), not by the type system.
- **Validation is layered:** `[Required]`/`[Range]` DataAnnotations on properties → `ValidateDataAnnotations()` on the builder → custom `.Validate(o => …, "msg")` for cross-field rules (e.g. `JavaExecutable` XOR `SignalCliExecutable`) → `SignalCliOptionsValidator` (`[OptionsValidator]` source-gen — closes the reflection-free / AOT-safe path). All three are wired up in `ServiceCollectionExtensions.ConfigureOptions`. Don't pick one — add to all of them when relevant.
- **Internal services read `_options.Value` once in the constructor** and cache the snapshot in a `private readonly SignalCliOptions _options`. The `.Value` access is what triggers validation; doing it in the ctor means `OptionsValidationException` surfaces on host start, not on some random method call.
- **Both `AddSignalCli` overloads are idempotent** — guarded by an `IOptions<SignalCliOptions>`-presence check in the service collection. Tests rely on this.

## Logging: `[LoggerMessage]` exclusively

- **No new direct `_logger.LogInformation("template {Arg}", arg)` calls.** Every new log line goes through a `[LoggerMessage]`-decorated `partial` method on a sibling `internal static partial class XxxLog : Logging/XxxLog.cs`. CA1848/CA1873 must stay green.
- **EventId blocks reserved per service** (do not reuse across classes):
    - 100–199 `SignalCliHostedServiceLog`
    - 200–299 `SignalCliHealthMonitorLog`
    - 300–399 `JsonRpcClientLog`
    - 400–499 `JsonRpcClientHostedServiceLog`
    - 500–599 `SignalEventServiceLog`
    - 600–699 `SignalServiceLog`
    - 700–799 `SignalMessageLog`
    - 800–899 `SignalAccountsLog` / `SignalDevicesLog` / `SignalGroupsLog`
    - 900–999 `ProcessRunnerLog` / `ProcessStateManagerLog`
- **`BeginScope` for subscription-bound work.** `SignalEventService.OnNotificationReceived` wraps the dispatch in `_logger.BeginScope(new Dictionary<string, object> { ["SubscriptionId"] = …, ["Account"] = … })` — downstream logs inherit those structured properties. Follow this for any per-notification / per-account work added later.
- **Privacy still wins.** The root-CLAUDE.md privacy contract is binding: PII (bodies, phones, attachment payloads) never appears in `[LoggerMessage]` templates at `Information+`. The `PrivacyLoggingTests` suite asserts on `EventId`, not text — so renaming a message won't accidentally break privacy verification.
- **Canonical `[LoggerMessage]` template — copy this shape.** EventId allocation: next free integer in the reserved block (sequential, gap-free; current `JsonRpcClientLog` runs 300→332). One method per log-event; `partial` keyword required; `ILogger` first parameter; additional template-`{Name}` parameters typed as concrete types (not `object`). Generated method visibility — `public static` for consumers from outside the file, `internal static` if only the owning service calls it.

  ```csharp
  internal static partial class JsonRpcClientLog
  {
      // EventId-блок 300–399 reserved per Established patterns table.
      // Next free: see end of file; bump by 1 for each new event.
      [LoggerMessage(EventId = 333, Level = LogLevel.Debug,
          Message = "Some event with structured {RequestId} and {Method}")]
      public static partial void SomeEvent(ILogger logger, string requestId, string method);

      // PII rule: anything Information+ must NOT contain {Body}/{Phone}/{FilePath}-shape
      // parameters. Trace+Debug allowed since they're opt-in for diagnostics.
      [LoggerMessage(EventId = 334, Level = LogLevel.Trace,
          Message = "Raw stdin line: {Line}")]
      public static partial void RawStdinLine(ILogger logger, string line);
  }
  ```

  When the event is naturally per-request, `BeginScope` from the calling site is preferred over passing scope-properties as template-args — see `JsonRpcClient.InvokeMethodAsync`'s `BeginScope(new Dictionary { ["RpcMethod"] = method, ["RpcRequestId"] = requestId })` which propagates to every downstream `JsonRpcClientLog.*` call within the scope. Adding a NEW scope-key joins the canonical set `{RpcMethod, RpcRequestId, SubscriptionId, Account}` — pin it in `ObservabilityPrivacyTests.MeterTagValues_AreOnlyKnownEnumLiterals` style if it'd flow to observability tags too.

## DI registration

- **`TryAddSingleton<T>` over `AddSingleton<T>` for our own services.** `TryAdd*` lets a consumer's prior `services.Replace(...)` survive — useful for testing (`FakeTimeProvider` injection) and for consumers swapping our defaults. Reserve plain `Add*` for `IHostedService` registrations: there we DO want every call to register an additional hosted-service descriptor (host iterates over them); deduplication would silently drop our background work.
- **"One-instance-two-roles" idiom for services that ARE hosted services AND must also resolve via a typed/interface accessor.** Register the concrete once via `services.TryAddSingleton<TConcrete>()`, then forward both the hosted-service slot and the interface adapter to that single instance via factory delegates:
  ```csharp
  services.TryAddSingleton<SignalCliHostedService>();
  services.AddHostedService(sp => sp.GetRequiredService<SignalCliHostedService>());
  services.TryAddSingleton<IStreamPairProvider>(sp => sp.GetRequiredService<SignalCliHostedService>());
  ```
  Canonical sites: `SignalCliHostedService` (concrete + hosted + `IStreamPairProvider`), `JsonRpcClientHostedService` (concrete + hosted + `IJsonRpcClientProvider`), `SignalEventService` (concrete + hosted). One DI instance, three resolution paths, zero risk of "two instances, one host-registered, one not".
- **`AddSignalCli` idempotency via private sentinel-type marker.** Both overloads first check `services.Any(d => d.ServiceType == typeof(SignalCliRegistrationMarker))`; if present, short-circuit. The marker type itself is `private sealed class SignalCliRegistrationMarker {}` inside `ServiceCollectionExtensions` — consumers can't accidentally register or check it. **Do NOT replace with `services.Any(d => d.ServiceType == typeof(IOptions<SignalCliOptions>))`** — that's what was tried originally, but `IOptions<T>` registers as open-generic (`typeof(IOptions<>)`), not concrete; the check always returned false and every repeated `AddSignalCli` call duplicated 3 hosted-service descriptors → double-startup. Full failure-mode rationale in `audit-followup-2026/addsignalcli-idempotency-fix` (4.0.0).

## Background loops + time

- **Periodic workers are `BackgroundService` + `PeriodicTimer(interval, TimeProvider)`.** No raw `Task.Run` + `while (!ct.IsCancellationRequested) { await Task.Delay(...); }` patterns. `SignalCliHealthMonitor` is the canonical reference.
- **.NET 10 changed `BackgroundService.ExecuteAsync` to run entirely on a background thread** ([compatibility breaking change](https://learn.microsoft.com/dotnet/core/compatibility/extensions/10.0/backgroundservice-executeasync-task)). The synchronous prefix no longer blocks other services starting. Consequences: (1) do NOT place startup-blocking initialization at the top of `ExecuteAsync` expecting `StartAsync`-semantics — use the constructor or a `StartAsync` override; (2) order-dependent boot work goes through `IHostedLifecycleService.StartingAsync`/`StartedAsync`. `SignalCliHealthMonitor.ExecuteAsync` is already compliant — first statement is `new PeriodicTimer(...)`, immediately followed by `await timer.WaitForNextTickAsync(...)`, no sync prefix.
- **`TimeProvider` consistency inside a class:** if a class accepts a `TimeProvider`, then *every* wait it performs goes through it: `Task.Delay(_, _, TimeProvider, ct)`, `new CancellationTokenSource(timeout, TimeProvider)` (the .NET 8+ overload — [`What is TimeProvider?` — *Use with .NET*](https://learn.microsoft.com/dotnet/standard/datetime/timeprovider-overview#use-with-net) explicitly lists this constructor as TimeProvider-aware), `TimeProvider.CreateTimer(...)`, `new PeriodicTimer(interval, TimeProvider)`. No mixing real and virtual clocks in one class. `SignalCliHostedService.ScheduleRestartWindowReset` uses `_timeProvider.CreateTimer(...)`, not `Task.Run(() => Task.Delay(...))`. `SignalCliHealthMonitor.PingCliAsync` uses `new CancellationTokenSource(timeout, _timeProvider)` — this is the canonical site; both `JsonRpcClient.InvokeMethodAsync` and `SignalCliHostedService.StopProcessInternalAsyncNoLock` will follow the same pattern after `post-modernize-tuning` §1.7 / §8a.7. **Do not introduce a new `new CancellationTokenSource(TimeSpan)` (parameterless-of-TimeProvider) inside a class that already injects a `TimeProvider`** — pass `_timeProvider` to the overload.
- **Tests under `SignalCliHealthMonitor/` and `SignalCliHostedService/Restart*/` must not call `Task.Delay(>10ms)`.** Use `FakeTimeProvider.Advance(...)`. If you find yourself wanting to wait for real time in those suites, you are reaching for the wrong tool. See `.claude/rules/testing.md` § FakeTimeProvider and the root-CLAUDE.md "No wall-clock in tests" rule.

## Event streams: two surfaces

- Each event kind in `SignalEventService` has **both** an `IObservable<T>` (Rx, fan-out / broadcast) and an `IAsyncEnumerable<T>` (Channels, default for `await foreach`, single-consumer with back-pressure). When adding a new event kind, add both — see how `TextMessages` + `TextMessagesAsync` are paired.
- The async surface uses `Channel.CreateBounded<T>(new BoundedChannelOptions(1024) { FullMode = DropOldest, SingleReader = false, SingleWriter = true })`. Drop-oldest is logged at `Debug` with a counter — don't change to `Wait` without a doc-update justifying the back-pressure mode.
- **Single-consumer is documented in XMLDoc** on the `*Async` methods. If a caller needs fan-out, they take the `IObservable<T>` — say so explicitly.

## Disposal

- **`IAsyncDisposable`-only for classes with async cleanup.** `IJsonRpcClient` derives from `IAsyncDisposable` only — never both `IDisposable` and `IAsyncDisposable`. No `Dispose()` that wraps `DisposeAsync().AsTask().GetAwaiter().GetResult()` — that's a deadlock vector. DI containers correctly call `DisposeAsync`; external callers use `await using`.
- **Stateless façades have no `IDisposable` at all.** `SignalAccounts`, `SignalDevices`, `SignalGroups`, `SignalService`, `SignalMessage` — none implement `IDisposable`. If you find yourself adding an empty `Dispose()` to a service, stop: either you have real resources (add real cleanup) or you don't (don't implement the interface).

## Other established patterns

- **`System.Threading.Lock` over `lock (someObject)`.** C# 13 / .NET 9+. We use it in `ProcessStateManager`, `SignalEventService`, `JsonRpcClient` (`_readerLock`). Don't lock on `this` or on the collection you're guarding.
- **`[CallerArgumentExpression]` for `Validate*` helpers** — `paramName` is derived from the caller's expression, not hardcoded. `SignalMessage.ValidateRecipients` is the canonical example.
- **Strong typing over magic strings:** `TextStyleMode` enum, not `string? mode = "styled"`. For protocol values that must compare case-insensitively, use `StringComparison.OrdinalIgnoreCase`; reserve `ToUpperInvariant()` for values crossing the process boundary (per the root-CLAUDE.md "Text styles" rule — locale-independent).
- **`unchecked Interlocked.Increment` for monotonic ID counters.** `AtomicCounter` is one line: `unchecked((int)Interlocked.Increment(ref _seed))`. Don't try to "reset" — int32 wraparound is fine for request IDs (uniqueness in active set is what matters).
- **Derive a typed exception only for "consumer-actionable, high-frequency" RPC error codes.** Current derived types: `RateLimitException` (signal-cli code `-5`, consumers retry with backoff) and `UntrustedIdentityException` (`-4`, consumers verify safety-number then resend). Other signal-cli codes (`-1` UserError, `-3` IoError, `-6` CaptchaRejected) stay base `JsonRpcException` because consumers typically just log + surface — no actionable typed-catch is expected. Heuristic when adding a new derived type: "Would `catch (XxxException)` lead to materially different consumer code than `catch (JsonRpcException) when (ex.KnownCode == JsonRpcErrorCode.Xxx)`?" If yes, derive. If no, don't — the base + `KnownCode` enum is sufficient and avoids exception-hierarchy bloat.

## AOT readiness (post-`post-modernize-tuning` §6)

- **Source-gen-only JSON in production.** `SignalJson.Options.TypeInfoResolver = SignalJsonContext.Default` (no `DefaultJsonTypeInfoResolver` fallback). Every type that crosses the JSON boundary from `src/SignalCli/**` MUST be registered via `[JsonSerializable(typeof(T))]` in `Serialization/SignalJsonContext.cs`. `JsonContextRegistrationTests` reflectively scans every `*Parameters`/`*Response` DTO in `Models/Signal/*` and asserts each is in the context — adding a new DTO without registration fails this test immediately, NOT at runtime with `NotSupportedException`.
- **`SignalJson.OptionsForTests` is test-only.** Annotated `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` on its property getter; the Lazy-field-initializer carries `[UnconditionalSuppressMessage]` with justification (real access is gated through the property). Tests that need anonymous-type payloads (`new { Hello = "world" }`) use this; production code MUST NOT.
- **Test-local source-gen contexts** for test-only DTOs. `TestSerializationContext` in `Tests/SignalCli.Tests/TestSerializationContext.cs` registers `TestProbeRequest`/`TestProbeResponse` for `JsonRpcClientTests` — separate context, does NOT pollute production `SignalJsonContext`. Pattern: when a new test needs typed JSON probes, extend the test-local context, not the production one.
- **Wrapper-record + custom `JsonConverter` for List-shaped responses.** `ListAccountsResponse`/`ListGroupsResponse` are `record(IReadOnlyList<T> Items) : IReadOnlyList<T>` with `[JsonConverter]` that reads/writes a flat JSON array (delegating to `JsonSerializer.Deserialize<List<T>>(ref reader, JsonTypeInfo<List<T>>)`). Both the wrapper type AND `List<T>` MUST be in source-gen context. See `Models/Signal/Accounts/ListAccountsResponse.cs` for canonical shape.
- **`IHostedLifecycleService` + `IAsyncDisposable`** on `SignalCliHostedService` (post-`hosting-modernization` §8a.2/§8a.3). Phase-methods are no-op `Task.CompletedTask`; `DisposeAsync` drains `_operationLock.WaitAsync` with 2s `TimeProvider`-aware timeout, then runs shared `DisposeCore`. **Root-CLAUDE.md "no sync-over-async in disposal" rule enforced**: `Dispose()` is sync-only with its own implementation (NOT `DisposeAsync().GetAwaiter().GetResult()`).
- **`AddSignalCli(IConfiguration)` overload IS AOT-safe (4.0.1+).** `<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>` was missing from `SignalCli.csproj` until 4.0.1 (`configuration-binder-aot-completion` capability); now present and source-gen intercepts `OptionsBuilderConfigurationExtensions.Bind` per [MS Learn](https://learn.microsoft.com/dotnet/core/extensions/configuration-generator). `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` attributes were removed from the overload. AOT-targeting consumers can use either `AddSignalCli(IConfiguration)` or `AddSignalCli(Action<SignalCliOptions>?)` — both warning-free. The original "partial fix" caveat from 3.0.0 is RESOLVED; if you find this paragraph still saying "NOT AOT-safe" anywhere, it's stale — verify against `src/SignalCli/Extensions/ServiceCollectionExtensions.cs`.
- **`VerifyReferenceTrimCompatibility` / `VerifyReferenceAotCompatibility` are deliberately NOT enabled** in `SignalCli.csproj`. Both flags warn about transitive dependencies that lack `IsTrimmable`/`IsAotCompatible` metadata (Microsoft *Prepare .NET libraries for trimming*). Our two non-trivial transitive dependencies — `System.Reactive` (not `IsTrimmable`-annotated as of 6.0.1) and `JetBrains.Annotations` (build-time only, PrivateAssets="all") — would flood the build with warnings without aiding correctness. If a future minor of `System.Reactive` ships `IsTrimmable`, opt in.

## Observability

- **Two surfaces only**, both named `"SignalCli.NET"`: `SignalCliDiagnostics.ActivitySource` for tracing (spans `rpc.<method>`, `signalcli.process.start`, `signalcli.healthcheck.ping`, `signalcli.subscribe`), `SignalCliDiagnostics.Meter` for metrics (`signalcli.rpc.requests`, `signalcli.rpc.duration`, `signalcli.process.restarts`, `signalcli.events.dropped`, `signalcli.subscriptions.active`). Adding new instruments goes in `SignalCli/Diagnostics/SignalCliDiagnostics.cs` only — do not spawn a second source.
- **Tag values are low-cardinality and PII-free.** The canonical set of tag keys is exactly `{method, status, trigger, event_type}`. `MeterTagValues_AreOnlyKnownEnumLiterals` in `ObservabilityPrivacyTests` pins this — if you add a new tag key, you MUST extend the test's `knownTagKeys` set and re-justify in the test fixture why the key is PII-free. Adding `account`/`phone`/`recipient`/`body` as a tag value is a root-CLAUDE.md privacy-invariant violation (observability extension); the test catches it via literal-substring asserts on seed PII.
- **HealthChecks adapter is a separate optional package** (`SignalCli.NET.HealthChecks`). Core library NEVER takes a hard dependency on `Microsoft.Extensions.Diagnostics.HealthChecks` — it's generic-host-only and ASP.NET-independent, but consumers without a health-check pipeline shouldn't pay for it. The adapter reads `ProcessStateManager.CurrentState` (public) + `SignalCliHealthMonitor.LastPingResult` (internal, gated via `[InternalsVisibleTo("SignalCli.HealthChecks")]`). Data-bag fields: `state`, `last_ping_ok`, `last_ping_at` — no PII.
- **Listener-fan-out in tests must be thread-safe.** `ActivitySource.AddActivityListener` and `MeterListener` are global registrations; callbacks may arrive from parallel-test threads. Use `Lock` + snapshot pattern (see `ObservabilityPrivacyTests._captureLock`) for any captured-collection access, otherwise `List<T>` throws `Collection was modified` intermittently.
