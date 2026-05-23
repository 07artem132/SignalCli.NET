# CLAUDE.md

Guidance for AI coding agents (Claude Code, Copilot, etc.) working in this repository.

## Project

**SignalCli.NET** — a .NET wrapper around [`signal-cli`](https://github.com/AsamK/signal-cli) (a Java app) that exposes a typed API for the Signal messenger. The library launches and supervises `signal-cli` in JSON-RPC mode over stdin/stdout, correlates requests/responses, and surfaces incoming events through **two parallel surfaces**: `IObservable<T>` (Rx, for fan-out/broadcast) and `IAsyncEnumerable<T>` (Channels, default for `await foreach`).

- Target framework: **net10.0 (LTS)**, language **C# 14**. Package version **2.1.0**.
- Requires **JDK 25+** (signal-cli 0.14.3's `Main` is class-file version 69.0 = Java 25) and **signal-cli 0.14.3** (downloaded by the `SignalCli.Runtime` package at build time). Java is **not** required with the native package (`SignalCli.Runtime.Native`, Linux x64) or the bundled-JRE packages (`SignalCli.Runtime.Jre.win-x64`, `SignalCli.Runtime.Jre.osx-arm64`).

## Build & test

```bash
dotnet build SignalCli.sln                                  # build all
dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj    # run tests (180 tests)
dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj --collect:"XPlat Code Coverage"  # coverage
```

- The `SignalCli.runtime` project downloads signal-cli on first build (network required); subsequent builds are skipped via an MSBuild `Exists` gate. The `SignalCli.runtime.native` and `SignalCli.runtime.jre.*` projects similarly download their payloads (native binary / Temurin JRE), so a clean `dotnet build SignalCli.sln` pulls several hundred MB once. To iterate quickly on the library, build/test `src/SignalCli` + `Tests/SignalCli.Tests` directly.
- Prefer running tests after every meaningful change; the hosted-service/health-monitor suites are the safety net for process-lifecycle changes.
- Test suite is **wall-clock-independent**: `SignalCliHealthMonitor/` and `SignalCliHostedService/Restart*/` tests use `FakeTimeProvider` exclusively (never `Task.Delay(>10ms)`). If you add a test that depends on real time, you have introduced flake — use `FakeTimeProvider.Advance(...)` instead.

### Restoring packages in a sandboxed env (Claude Code on the web)

The repo's `NuGet.Config` has a `<packageSourceMapping>` that points `SignalCli.*` packages at a GitHub-hosted feed which requires auth. In a fresh remote-execution container, that feed is unreachable. Use this restore flag instead of plain `dotnet restore`:

```bash
dotnet restore <project> --source https://api.nuget.org/v3/index.json -p:NuGetAudit=false
```

`--source` overrides the GitHub feed; `-p:NuGetAudit=false` skips the (also unreachable) vulnerability scanner. Once restored, `dotnet build/test --no-restore` works normally. The `dotnet` SDK itself is `apt`-installable from `packages.microsoft.com` (which is allowlisted in our container policy).

## Cloud development

For Claude Code on the Web sessions, see [`docs/cloud-development.md`](docs/cloud-development.md). A `SessionStart` hook (`.claude/hooks/session-start.sh`) installs `dotnet-sdk-10.0` and pre-warms NuGet for `Tests/SignalCli.Tests` — runs only when `CLAUDE_CODE_REMOTE=true`, so local workflows are untouched.

## Architecture (key types)

- `SignalCliHostedService` — launches/stops/restarts signal-cli; implements `IStreamPairProvider`. Takes `IOptions<SignalCliOptions>` + optional `TimeProvider`.
- `ProcessStateManager` — process state machine (`ProcessState` enum + `ProcessStateInfo`); single source of truth.
- `SignalCliHealthMonitor` — `BackgroundService` using `PeriodicTimer(interval, TimeProvider)`; pings `version`; force-restarts on failure.
- `JsonRpcClient` / `JsonRpcClientHostedService` — JSON-RPC transport; request/response correlation via `id` + `TaskCompletionSource`; notifications via `IObservable`. `JsonRpcClient` is **`IAsyncDisposable`-only**.
- `SignalEventService` — fans `receive` notifications to both `IObservable<T>` (`TextMessages`/`Attachments`/…) and `IAsyncEnumerable<T>` (`TextMessagesAsync(ct)`/`AttachmentsAsync(ct)`/…) for each of 10 event kinds.
- `SignalMessage` / `SignalService` / `SignalAccounts` / `SignalDevices` / `SignalGroups` — the Signal API surface. **None implement `IDisposable`** (stateless facades).
- `SignalCliOptions` + `SignalCliOptionsValidator` (source-gen `[OptionsValidator]`) — typed configuration with `[Required]`/`[Range]` DataAnnotations validated on host start. Legacy `Config` exists as `[Obsolete]` shim.
- `Logging/*Log.cs` — one `internal static partial class` per service with `[LoggerMessage]`-generated methods (~109 of them). EventId blocks are reserved per service; see "Logging" rule below.
- DI composition root: `Extensions/ServiceCollectionExtensions.cs` (`AddSignalCli(Action<SignalCliOptions>?)` is the modern overload; `AddSignalCli(Action<Config>?)` is the legacy shim; `AddSignalEvents()` is separate).

Patterns in use: Dependency Injection, Options pattern (`IOptions<TOptions>` + source-gen validation), Hosted Services / BackgroundService, Factory, Adapter/Wrapper (`IProcess`), Builder (`*Options.Builder`), Provider, Observer/Rx + async streams via `Channel<T>`, Facade, State Machine, Watchdog, Source-generated logging.

## Conventions (match the existing code)

- Modern C#: file-scoped namespaces, primary constructors, records for DTOs, `required`/collection expressions where natural, `Func<>`/`Action<>` over custom delegates.
- `var` only when the type is obvious; explicit type in `foreach`.
- `string`/`int` keywords, not `String`/`Int32`.
- `_camelCase` private fields, PascalCase public, `I`-prefixed interfaces.
- Always `.ConfigureAwait(false)` in library code.
- **Exceptions:** throw and catch *specific* types. A broad `catch (Exception)` is allowed **only** at long-running boundaries (the stdout reader loop, the health-monitor loop, the notification dispatcher) where one bad item must not kill the loop — and such catches must log and continue. Do not swallow exceptions silently elsewhere.
- **Comments and log messages are written in Ukrainian** in this codebase — match that when editing existing files.
- Keep XML doc comments on public members.

### Established patterns (these are the law — do not regress)

The patterns below are not aspirational. They were rolled out across the codebase in the `agent-friendly-modernization` change (2.1.0) and the test suite enforces them. New code MUST follow these; edits to existing code that touches an affected area MUST keep them. Re-introducing the old shape is a regression.

#### Async, cancellation, naming

- **Async suffix:** every `Task`/`ValueTask`-returning method has `Async` in its name. The one historical exception, `ISignalCliClient.Version()`, exists only as `[Obsolete]` shim delegating to `VersionAsync()`.
- **`CancellationToken cancellationToken = default` as the last explicit parameter.** Even when a paired `*Options` record carries a `CancellationToken` field (deprecated), the parameter on the method is the discoverable surface. Link both inside via `CreateLinkedTokenSource` — see `SignalMessage.LinkTokens` for the canonical helper.
- **`TaskCompletionSource<T>.TrySetCanceled(token)` always carries a token.** `JsonRpcClient` keeps `_disposeCts` for `DisposeAsync`-time cancellation and a transient cancelled CTS for stream-pair-change. Never `TrySetCanceled()` without an argument.

#### Configuration: `IOptions<SignalCliOptions>` only

- **Configurable knobs go in `SignalCliOptions`**, not in `Config`. `Config` is `[Obsolete]`-shimmed to `Action<Config>?` `AddSignalCli` overload — do not extend it.
- **Properties are `get; set;`** (not `init`-only). Microsoft.Extensions.Options is a stateful pattern: the framework creates the instance via `Activator.CreateInstance` and mutates it through your `Action<TOptions>.Configure`-delegate and `Bind(IConfiguration)`. `init` makes both reflection-based `Bind` and the `Configure`-delegate ergonomically painful — we learned this the hard way and reverted. Immutability is enforced socially (no setter calls after registration), not by the type system.
- **Validation is layered:** `[Required]`/`[Range]` DataAnnotations on properties → `ValidateDataAnnotations()` on the builder → custom `.Validate(o => …, "msg")` for cross-field rules (e.g. `JavaExecutable` XOR `SignalCliExecutable`) → `SignalCliOptionsValidator` (`[OptionsValidator]` source-gen — closes the reflection-free / AOT-safe path). All three are wired up in `ServiceCollectionExtensions.ConfigureOptions`. Don't pick one — add to all of them when relevant.
- **Internal services read `_options.Value` once in the constructor** and cache the snapshot in a `private readonly SignalCliOptions _options`. The `.Value` access is what triggers validation; doing it in the ctor means `OptionsValidationException` surfaces on host start, not on some random method call.
- **Both `AddSignalCli` overloads are idempotent** — guarded by an `IOptions<SignalCliOptions>`-presence check in the service collection. Tests rely on this.

#### Logging: `[LoggerMessage]` exclusively

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
- **Privacy still wins.** Critical rule #1 is the contract: PII (bodies, phones, attachment payloads) never appears in `[LoggerMessage]` templates at `Information+`. The `PrivacyLoggingTests` suite asserts on `EventId`, not text — so renaming a message won't accidentally break privacy verification.

#### Background loops + time

- **Periodic workers are `BackgroundService` + `PeriodicTimer(interval, TimeProvider)`.** No raw `Task.Run` + `while (!ct.IsCancellationRequested) { await Task.Delay(...); }` patterns. `SignalCliHealthMonitor` is the canonical reference.
- **`TimeProvider` consistency inside a class:** if a class accepts a `TimeProvider`, then *every* wait it performs goes through it: `Task.Delay(_, _, TimeProvider, ct)`, `new CancellationTokenSource(timeout, TimeProvider)` (the .NET 8+ overload — [`What is TimeProvider?` — *Use with .NET*](https://learn.microsoft.com/dotnet/standard/datetime/timeprovider-overview#use-with-net) explicitly lists this constructor as TimeProvider-aware), `TimeProvider.CreateTimer(...)`, `new PeriodicTimer(interval, TimeProvider)`. No mixing real and virtual clocks in one class. `SignalCliHostedService.ScheduleRestartWindowReset` uses `_timeProvider.CreateTimer(...)`, not `Task.Run(() => Task.Delay(...))`. `SignalCliHealthMonitor.PingCliAsync` uses `new CancellationTokenSource(timeout, _timeProvider)` — this is the canonical site; both `JsonRpcClient.InvokeMethodAsync` and `SignalCliHostedService.StopProcessInternalAsyncNoLock` will follow the same pattern after `post-modernize-tuning` §1.7 / §8a.7. **Do not introduce a new `new CancellationTokenSource(TimeSpan)` (parameterless-of-TimeProvider) inside a class that already injects a `TimeProvider`** — pass `_timeProvider` to the overload.
- **Tests under `SignalCliHealthMonitor/` and `SignalCliHostedService/Restart*/` must not call `Task.Delay(>10ms)`.** Use `FakeTimeProvider.Advance(...)`. If you find yourself wanting to wait for real time in those suites, you are reaching for the wrong tool.

#### Event streams: two surfaces

- Each event kind in `SignalEventService` has **both** an `IObservable<T>` (Rx, fan-out / broadcast) and an `IAsyncEnumerable<T>` (Channels, default for `await foreach`, single-consumer with back-pressure). When adding a new event kind, add both — see how `TextMessages` + `TextMessagesAsync` are paired.
- The async surface uses `Channel.CreateBounded<T>(new BoundedChannelOptions(1024) { FullMode = DropOldest, SingleReader = false, SingleWriter = true })`. Drop-oldest is logged at `Debug` with a counter — don't change to `Wait` without a doc-update justifying the back-pressure mode.
- **Single-consumer is documented in XMLDoc** on the `*Async` methods. If a caller needs fan-out, they take the `IObservable<T>` — say so explicitly.

#### Disposal

- **`IAsyncDisposable`-only for classes with async cleanup.** `IJsonRpcClient` derives from `IAsyncDisposable` only — never both `IDisposable` and `IAsyncDisposable`. No `Dispose()` that wraps `DisposeAsync().AsTask().GetAwaiter().GetResult()` — that's a deadlock vector. DI containers correctly call `DisposeAsync`; external callers use `await using`.
- **Stateless façades have no `IDisposable` at all.** `SignalAccounts`, `SignalDevices`, `SignalGroups`, `SignalService`, `SignalMessage` — none implement `IDisposable`. If you find yourself adding an empty `Dispose()` to a service, stop: either you have real resources (add real cleanup) or you don't (don't implement the interface).

#### Other established patterns

- **`System.Threading.Lock` over `lock (someObject)`.** C# 13 / .NET 9+. We use it in `ProcessStateManager`, `SignalEventService`, `JsonRpcClient` (`_readerLock`). Don't lock on `this` or on the collection you're guarding.
- **`[CallerArgumentExpression]` for `Validate*` helpers** — `paramName` is derived from the caller's expression, not hardcoded. `SignalMessage.ValidateRecipients` is the canonical example.
- **Strong typing over magic strings:** `TextStyleMode` enum, not `string? mode = "styled"`. For protocol values that must compare case-insensitively, use `StringComparison.OrdinalIgnoreCase`; reserve `ToUpperInvariant()` for values crossing the process boundary (critical rule #5).
- **`unchecked Interlocked.Increment` for monotonic ID counters.** `AtomicCounter` is one line: `unchecked((int)Interlocked.Increment(ref _seed))`. Don't try to "reset" — int32 wraparound is fine for request IDs (uniqueness in active set is what matters).

#### Observability

- **Two surfaces only**, both named `"SignalCli.NET"`: `SignalCliDiagnostics.ActivitySource` for tracing (spans `rpc.<method>`, `signalcli.process.start`, `signalcli.healthcheck.ping`, `signalcli.subscribe`), `SignalCliDiagnostics.Meter` for metrics (`signalcli.rpc.requests`, `signalcli.rpc.duration`, `signalcli.process.restarts`, `signalcli.events.dropped`, `signalcli.subscriptions.active`). Adding new instruments goes in `SignalCli/Diagnostics/SignalCliDiagnostics.cs` only — do not spawn a second source.
- **Tag values are low-cardinality and PII-free.** The canonical set of tag keys is exactly `{method, status, trigger, event_type}`. `MeterTagValues_AreOnlyKnownEnumLiterals` in `ObservabilityPrivacyTests` pins this — if you add a new tag key, you MUST extend the test's `knownTagKeys` set and re-justify in the test fixture why the key is PII-free. Adding `account`/`phone`/`recipient`/`body` as a tag value is a privacy invariant violation (CLAUDE.md rule #1 — observability extension); the test catches it via literal-substring asserts on seed PII.
- **HealthChecks adapter is a separate optional package** (`SignalCli.NET.HealthChecks`). Core library NEVER takes a hard dependency on `Microsoft.Extensions.Diagnostics.HealthChecks` — it's generic-host-only and ASP.NET-independent, but consumers without a health-check pipeline shouldn't pay for it. The adapter reads `ProcessStateManager.CurrentState` (public) + `SignalCliHealthMonitor.LastPingResult` (internal, gated via `[InternalsVisibleTo("SignalCli.HealthChecks")]`). Data-bag fields: `state`, `last_ping_ok`, `last_ping_at` — no PII.
- **Listener-fan-out in tests must be thread-safe.** `ActivitySource.AddActivityListener` and `MeterListener` are global registrations; callbacks may arrive from parallel-test threads. Use `Lock` + snapshot pattern (see `ObservabilityPrivacyTests._captureLock`) for any captured-collection access, otherwise `List<T>` throws `Collection was modified` intermittently.

### Backward compatibility convention

When we deprecate API, the rule is **one major version of `[Obsolete]` shim** before removal. Currently in flight (will be removed in **3.0**):

- `ISignalCliClient.Version()` → use `VersionAsync()`.
- `AddSignalCli(Action<Config>?)` → use `AddSignalCli(Action<SignalCliOptions>?)`. `Config` itself is `[Obsolete]`-shimmed.
- `*Options.CancellationToken` properties and `WithCancellationToken` builder methods on `TextMessageOptions`/`AttachmentMessageOptions`/`StickerMessageOptions` → pass `CancellationToken` directly to `Send*Async(options, ct)`.

When adding a new deprecation, mirror this shape: real new API + `[Obsolete("Use Y; will be removed in 3.0")]` shim that delegates, plus a `CHANGELOG.md` entry under "Інше". Internal call sites are migrated immediately; external call sites get one major release of grace.

## Critical rules (do not regress — these are audit findings + post-2.1.0 invariants)

1. **Privacy:** never log message bodies, phone numbers, or attachment payloads above `Trace`. RPC params/results and raw stdin/stdout lines are `Trace`-only. `SignalService` logs the method name only. `[LoggerMessage]` templates at `Information+` MUST NOT reference PII fields. **The same prohibition applies to `Activity` tag values and `Meter` tag values** (observability surface from `post-modernize-tuning` §11 — shipped): only method names, status enums, integer ids, durations, exception type names — never message contents / phones / file paths. Privacy guard tests (`ObservabilityPrivacyTests` — single fixture covering both `ActivityListener` and `MeterListener` capture paths) enforce this with literal-substring asserts on a seed phone, seed message body, and seed file path; `MeterTagValues_AreOnlyKnownEnumLiterals` also pins the canonical tag-key set (`method`, `status`, `trigger`, `event_type`), so any new tag key spawned without test-fixture update fails loudly.
2. **Process arguments:** build the signal-cli command via `ProcessConfig.ArgumentList` (each arg separate). Never go back to a single interpolated `Arguments` string with quoted paths.
3. **Attachments:** sanitize `FileName` with `Path.GetFileName` (see `AttachmentEntry.SafeFileName`) before writing temp files or building data URIs — guard against path traversal.
4. **Event dispatch:** in `SignalEventService`, a `DataMessage` is a *presence-based union*; emit every applicable observable AND its paired async-channel (text + attachment can both fire). Do not reintroduce early `return` between payload checks.
5. **Text styles:** use `ToUpperInvariant()` for style names (locale-independent).
6. **Serialization:** `System.Text.Json` **only** — `Newtonsoft.Json` is removed. Annotate model members with `[JsonPropertyName]` (never `[JsonProperty]`). Register every new serializable root type in the source-generated context `Serialization/SignalJsonContext.cs`, and serialize/deserialize via the shared options in `Serialization/SignalJson.cs` (which combines the source-gen resolver with a reflection fallback). `JsonRpcRequest.Params` / `JsonRpcResponse.Result` are `JsonElement`.
7. **Download scripts:** `src/SignalCli.runtime/download-signal-cli.*` and `src/build/download-jre.*` verify the archive SHA-256 before extraction. **The canonical version + hash live in the runtime csproj** (`<SignalCliVersion>`/`<SignalCliSha256>` у `SignalCli.runtime.csproj`; `<JreVersion>`/`<JreSha256>` у обох `SignalCli.runtime.jre.*.csproj`); the scripts read those values as arguments. Bumping a pinned version = single-csproj edit, no script edits. The `.ps1` files are deliberately **ASCII-only** (no Cyrillic/emoji) so they parse under Windows PowerShell 5.1 **without** needing a UTF-8 BOM — keep them ASCII. They also invoke the Windows system `tar` (`%SystemRoot%\System32\tar.exe`) explicitly and stage extraction through an ASCII temp dir, because Git's GNU `tar` mis-reads `C:\…` paths and bsdtar fails on non-ASCII target paths.
8. **Bundled-JRE packages** (`SignalCli.Runtime.Jre.win-x64`, `SignalCli.Runtime.Jre.osx-arm64`): bundle a SHA-256-pinned Eclipse Temurin **25** JRE + signal-cli. The JRE and jars are packed as **single `.zip` files** and extracted by the consumer `.targets` via MSBuild's built-in `<Unzip>` — **do not** pack the JRE as individual files: NuGet treats an extension-less `PackagePath` (e.g. the JRE's `lib/modules`) as a *directory* and corrupts the layout, which crashes the JVM at bootstrap. `Config.ResolveBundledJava` auto-discovers `<output>/jre/bin/java[.exe]`, so consumers need no system Java and should **not** set `Config.JavaExecutable`.
9. **No sync-over-async in disposal.** `IJsonRpcClient` is `IAsyncDisposable`-only; never re-introduce a `Dispose()` that does `DisposeAsync().AsTask().GetAwaiter().GetResult()`. DI containers and `await using` are the supported paths.
10. **Fail-fast configuration.** `SignalCliOptions` validation (DataAnnotations + custom rule + `[OptionsValidator]` source-gen) is wired into `AddSignalCli`. Internal services read `_options.Value` in the constructor — that's deliberately where validation fires. Don't bypass it (e.g. don't capture `IOptions<>` and read `.Value` lazily in some method later).
11. **No wall-clock in tests.** Tests in the lifecycle/health-monitor suites must drive time via `FakeTimeProvider`. Re-introducing `await Task.Delay(>10ms)` to a test there is a regression.
12. **Options validation has exactly one path: source-gen `[OptionsValidator]`.** `ServiceCollectionExtensions.ConfigureOptions` registers `SignalCliOptionsValidator` (source-gen, reflection-free, AOT-safe) via `TryAddEnumerable<IValidateOptions<SignalCliOptions>>`. Cross-field rules go in `.Validate(o => …, "msg")` lambdas. **Do not re-add `.ValidateDataAnnotations()`** alongside the `[OptionsValidator]`: it duplicates the same `[Required]`/`[Range]` checks through reflection and is the reason `<IsAotCompatible>true</IsAotCompatible>` still trips IL2026 warnings. `post-modernize-tuning` §8b.8 removes the redundant call; do not bring it back.
13. **Source-gen JSON has no reflection fallback.** Every type passed to `JsonSerializer.Serialize`/`Deserialize`/`SerializeToElement` from `src/SignalCli/**` MUST be registered in `Serialization/SignalJsonContext.cs`. The `JsonContextRegistrationTests` suite (added in `post-modernize-tuning` §6.12) reflectively enumerates `InvokeMethodAsync<TRequest,TResponse>` call sites and asserts each type pair is in the context — if your new RPC method adds a DTO that's not in the context, this test fails loudly instead of producing silent `"{}"` payloads at runtime.
14. **Typed/idempotent state errors.** `SignalEventService.SubscribeAsync` is **idempotent** (post-`subscription-race-safety` §3.7): re-subscribing the same account returns the existing `subscriptionId` instead of throwing a generic `InvalidOperationException` with a locale-dependent Ukrainian message. Argument null/empty checks throw `ArgumentException` (via `ArgumentException.ThrowIfNullOrEmpty`) with the correct `paramName`. When you add new state-error sites elsewhere, mirror this: prefer idempotency over throwing; if you must throw, prefer a derived typed exception (or `ObjectDisposedException`/`ArgumentException` subclasses) over a generic `InvalidOperationException` so callers can pattern-match without inspecting the message text.

## Planning (OpenSpec)

This repo uses [OpenSpec](https://github.com/Fission-AI/OpenSpec) for change planning under `openspec/changes/`. For non-trivial work, create/extend a change (proposal → design → specs → tasks) and run `npx -y @fission-ai/openspec@latest validate <change> --strict` before implementing.

**Implemented and merged** (historical reference, do not re-open):
- `address-audit-findings` — privacy/security/correctness audit round 1.
- `modernize-architecture` — `net9.0` → `net10.0`, `Newtonsoft.Json` → `System.Text.Json` (+ source-gen `JsonSerializerContext`), single-source-of-truth process state via `ProcessStateManager`.
- `agent-ready-conventions` — `.editorconfig`, analyzers (`AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`), narrowed broad `catch`-es, this `CLAUDE.md`.
- `address-audit-findings-2` — audit round 2: bounded RPC timeout (`Config.RequestTimeoutSeconds`), windowed restart budget, idempotent `AddSignalCli`, `IAsyncDisposable` on `JsonRpcClient`, integration tests + bundled-JRE E2E.
- `comprehensive-code-audit` — the audit document itself (`AUDIT-FINDINGS.md`); fixes live in the two `address-audit-findings*` changes.
- `agent-friendly-modernization` (**2.1.0**) — five capabilities shipped together:
  - `agent-friendly-api`: `VersionAsync`; explicit `CancellationToken` on `Send*Async`; `IJsonRpcClient` → `IAsyncDisposable`-only; sync `IJsonRpcClientFactory.Create()`; `TextStyleMode` enum; `TrySetCanceled(token)`; `[CallerArgumentExpression]`; `IDisposable` dropped from stateless façades; `AtomicCounter` simplified.
  - `background-monitor`: `SignalCliHealthMonitor` is now a `BackgroundService` with `PeriodicTimer(interval, TimeProvider)`; `SignalCliHostedService.ScheduleRestartWindowReset` uses `TimeProvider.CreateTimer`.
  - `source-generated-logging`: all ~109 `ILogger` callsites moved to `[LoggerMessage]` `partial` methods in `Logging/*Log.cs`; EventId blocks reserved per service; `BeginScope` for subscription-bound work.
  - `options-pattern`: `SignalCliOptions` + `IOptions<>` with `ValidateDataAnnotations` + custom `.Validate(...)` + `[OptionsValidator]` source-gen; legacy `Config` is `[Obsolete]` shim. **All internal services take `IOptions<SignalCliOptions>` now.**
  - `async-stream-events`: each event kind on `ISignalEventService` has a paired `IAsyncEnumerable<T>` method (`TextMessagesAsync(ct)`, …) on top of bounded `Channel<T>` (1024, DropOldest, single-reader).

**Pending changes:**
- `post-modernize-tuning` — broad follow-up wave covering RPC back-pressure, state-machine thread safety, subscription race safety, hosting modernization (`IHostedLifecycleService`, `IAsyncDisposable` on `SignalCliHostedService`), options validation tightening, AOT readiness (`<IsAotCompatible>true</IsAotCompatible>` + reflection-free JSON), high-perf logging extensions, test virtualization (xUnit v3 + MTP, more `FakeTimeProvider`), supply-chain hardening, **observability** (`ActivitySource` + `Meter` + optional `SignalCli.NET.HealthChecks` package — from the 2026-05-23 agent-friendly audit), and the v3.0 breaking-API wave (`agent-friendly-api`). Cloud-development sub-capability is already drafted and merged. See `openspec/changes/post-modernize-tuning/{proposal,tasks}.md`.

When you start a new material piece of work outside this scope, create a new `openspec/changes/<change-name>/` directory with `proposal.md` / `design.md` / `tasks.md` / `specs/<capability>/spec.md`, mirror the structure of `agent-friendly-modernization`, and run `openspec validate <change> --strict` before implementing.

## Working style (how Claude and the user collaborate on this repo)

These are conventions we landed during the 2.1.0 work. They aren't strict — but they're what worked and what we expect from each other going forward.

- **Plan first, then implement.** Non-trivial work goes through OpenSpec (proposal → design → tasks → spec.md per capability). The plan should be small enough to validate (`openspec validate --strict`) and explicit enough that any subset is independently shippable.
- **One commit per capability/cluster.** When implementing a multi-cluster OpenSpec change, each capability lands as its own commit with a clear message. Cluster A → cluster B → … is easier to review and bisect than a single mega-commit. Final batch (docs, version bump, leftover items) goes in one trailing commit.
- **`dotnet build` + `dotnet test --no-build` after every cluster.** If the test count drops or a new flake appears, stop and diagnose before moving on. The suite is 180/180 stable — drift is the early-warning sign.
- **Don't claim a flaky test is "pre-existing" without a baseline check.** If a test fails under your changes, `git stash`, rebuild + retest at HEAD, compare. We diagnosed real flake (the `ForceRestart*Delay*` family) this way and migrated it to `FakeTimeProvider` rather than living with it.
- **Subagents (`Explore`, etc.) for parallel research, not for write tasks.** Most of the implementation work in 2.1.0 was direct edits in the main agent; subagents are useful for "find me all callsites of X" or "check whether Y exists in the test suite" but not for "implement cluster D for me."
- **Comments and log messages stay in Ukrainian.** Match the codebase's voice when you edit. The CHANGELOG, README, and PR/commit titles can be Ukrainian or English — mirror the surrounding style.
- **Don't create `*.md` documentation files unless asked.** This `CLAUDE.md`, `README.md`, and `CHANGELOG.md` are the only durable docs we maintain. Working notes belong in OpenSpec change documents.
- **Don't add `[Obsolete]` shims for code that has no real external consumer** — just delete and document in `CHANGELOG.md`. Reserve the shim convention for things that we know are in user code (e.g. `Version()`, the `Config`-based registration, the deprecated `*Options.CancellationToken`).

## Git

Work on a feature branch; do not push or commit unless asked. When commits are requested, prefer one commit per OpenSpec capability (see "Working style" above). Never amend already-pushed commits without explicit approval.
