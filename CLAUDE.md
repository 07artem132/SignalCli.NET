# CLAUDE.md

Guidance for AI coding agents (Claude Code, Copilot, etc.) working in this repository.

## Project

**SignalCli.NET** — a .NET wrapper around [`signal-cli`](https://github.com/AsamK/signal-cli) (a Java app) that exposes a typed API for the Signal messenger. The library launches and supervises `signal-cli` in JSON-RPC mode over stdin/stdout, correlates requests/responses, and surfaces incoming events through **two parallel surfaces**: `IObservable<T>` (Rx, for fan-out/broadcast) and `IAsyncEnumerable<T>` (Channels, default for `await foreach`).

- Target framework: **net10.0 (LTS)**, language **C# 14**. Package version **3.0.0**.
- Requires **JDK 25+** (signal-cli 0.14.3's `Main` is class-file version 69.0 = Java 25) and **signal-cli 0.14.3** (downloaded by the `SignalCli.Runtime` package at build time). Java is **not** required with the native package (`SignalCli.Runtime.Native`, Linux x64) or the bundled-JRE packages (`SignalCli.Runtime.Jre.win-x64`, `SignalCli.Runtime.Jre.osx-arm64`).

## Build & test

```bash
dotnet build SignalCli.sln                                  # build all
dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj    # run tests (215 tests)
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

## signal-cli protocol behavior we depend on

These are facts about the *upstream* signal-cli Java app that our wrapper relies on. Each is cited
to a specific signal-cli source file at commit `bda4e7f` (after 0.14.3). Re-verify against newer
signal-cli releases when bumping the pinned version in `SignalCli.runtime.csproj`.

- **Graceful shutdown trigger = stdin EOF or SIGTERM/SIGINT.** signal-cli has no `exit` JSON-RPC
  method and does not read literal text on stdin — every stdin line is parsed as JSON
  (`JsonRpcReader.java:59-75`). Our wrapper closes stdin (`StandardInput.Close()`) in
  `StopProcessInternalAsyncNoLock`; signal-cli's reader-loop terminates naturally on EOF, its
  dispatcher's finally-block clears subscriptions (`SignalJsonRpcDispatcherHandler.java:212-214`),
  and the JVM shuts down cleanly. Signal handlers (SIGINT/SIGTERM via `sun.misc.Signal` —
  `Shutdown.java:24-25`) are the second valid trigger but Windows has no POSIX signals, so we
  prefer stdin-close as the cross-platform path. **Critical rule:** never re-add
  `WriteLineAsync("exit")` — literal "exit" produces `-32700 Parse error` and the process keeps
  running. See `signal-cli-protocol-alignment` capability `graceful-shutdown-fix` for history.

- **Stdout = pure JSON-RPC, line-flushed.** signal-cli's `JsonWriterImpl.write` calls
  `writer.flush()` after every JSON line (`JsonWriterImpl.java:30`), so our `ReadLineAsync` loop
  observes each message promptly even though Java's default for non-TTY stdout is block-buffered.
  signal-cli never emits banner/version/log output on stdout — all diagnostics go to stderr via
  SLF4J/Logback. The `UnknownMessage` log line in our `ProcessMessageAsync` should fire
  approximately never in practice; if it does, suspect protocol drift in a newer signal-cli release.

- **Parallel request processing → match by `id`, not by order.** signal-cli's `JsonRpcReader`
  uses `Executors.newVirtualThreadPerTaskExecutor()` to handle requests
  (`JsonRpcReader.java:58`). Response arrival order is non-deterministic — multiple in-flight
  requests are dispatched to virtual threads that complete in execution-time order, not request
  order. Our `JsonRpcClient._pendingRequests : ConcurrentDictionary<string, TaskCompletionSource>`
  keyed by request `id` is mandatory; never refactor to a queue or order-based correlation.

- **`subscribeReceive` is NOT idempotent at the protocol level.** signal-cli returns a fresh ID
  via `AtomicInteger.getAndIncrement()` for every call
  (`SignalJsonRpcDispatcherHandler.java:143`). Our idempotency lives entirely in
  `SignalEventService._pendingSubscribes` (reservation TCS pattern). If our code path ever
  bypasses the reservation, signal-cli delivers duplicate `receive` notifications for each
  subscription ID — and unsubscribing one ID leaves the others active.

- **Jackson `maxStringLength = 20_000_000` PER STRING TOKEN.** signal-cli uses Jackson 2.20.2
  (`gradle/libs.versions.toml:10`) with `StreamReadConstraints` defaults — does NOT override
  `maxStringLength` (Util.java:51-56 creates the ObjectMapper minimally). Our
  `MaxInlineEncodedAttachmentBytes = 12_000_000` (after `attachment-threshold-margin`) keeps the
  base64-encoded attachment string ≤ 16M with 4M of margin for the rest of the `send` request.
  Total-JSON-line length is also checked in `JsonRpcClient.SendRequestAsync` against 20M (a
  separate, looser check) — both are needed because the constraints address different limits
  (per-token vs per-line).

- **Error codes outside JSON-RPC 2.0 standard.** signal-cli emits these in addition to
  `-32600..-32603` and `-32700` (`SignalJsonRpcCommandHandler.java:35-280`):
  - `-1` `UserError` (bad input, invalid number)
  - `-3` `IoError` (file system / network)
  - `-4` `UntrustedIdentity` (key verification failure) — surfaced as `UntrustedIdentityException`
  - `-5` `RateLimit` (server throttle) — surfaced as `RateLimitException`
  - `-6` `CaptchaRejected`
  All errors are sent on **stdout** (same channel as success responses), never stderr. The
  typed surface is `SignalCli.Exceptions.JsonRpcErrorCode` enum + `JsonRpcException.KnownCode`
  property; `RateLimitException` and `UntrustedIdentityException` are the two derived types
  for high-leverage codes.

- **Java 25 requirement.** signal-cli 0.14.0+ requires JDK 25 (`build.gradle.kts:7-8`).
  `signal-cli 0.14.3` (our pinned version in `SignalCli.runtime.csproj`) is the first 0.14.x.
  Bumping signal-cli later than 0.14.x without bumping JDK fails at JVM startup with
  `UnsupportedClassVersionError`. The bundled-JRE packages
  (`SignalCli.Runtime.Jre.{win-x64,osx-arm64}`) pin Temurin 25 SHA-256 in their csproj.

**When bumping `<SignalCliVersion>` in `SignalCli.runtime.csproj`:** re-verify each of the
seven facts above against the new signal-cli source. The PR description SHALL include a one-line
confirmation that these facts were re-verified, even if zero edits resulted. Discrepancies SHALL
be resolved either by adapting the wrapper or by updating this section + the commit citation.

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
- **.NET 10 changed `BackgroundService.ExecuteAsync` to run entirely on a background thread** ([compatibility breaking change](https://learn.microsoft.com/dotnet/core/compatibility/extensions/10.0/backgroundservice-executeasync-task)). The synchronous prefix no longer blocks other services starting. Consequences: (1) do NOT place startup-blocking initialization at the top of `ExecuteAsync` expecting `StartAsync`-semantics — use the constructor or a `StartAsync` override; (2) order-dependent boot work goes through `IHostedLifecycleService.StartingAsync`/`StartedAsync`. `SignalCliHealthMonitor.ExecuteAsync` is already compliant — first statement is `new PeriodicTimer(...)`, immediately followed by `await timer.WaitForNextTickAsync(...)`, no sync prefix.
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

#### AOT readiness (post-`post-modernize-tuning` §6)

- **Source-gen-only JSON in production.** `SignalJson.Options.TypeInfoResolver = SignalJsonContext.Default` (no `DefaultJsonTypeInfoResolver` fallback). Every type that crosses the JSON boundary from `src/SignalCli/**` MUST be registered via `[JsonSerializable(typeof(T))]` in `Serialization/SignalJsonContext.cs`. `JsonContextRegistrationTests` reflectively scans every `*Parameters`/`*Response` DTO in `Models/Signal/*` and asserts each is in the context — adding a new DTO without registration fails this test immediately, NOT at runtime with `NotSupportedException`.
- **`SignalJson.OptionsForTests` is test-only.** Annotated `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` on its property getter; the Lazy-field-initializer carries `[UnconditionalSuppressMessage]` with justification (real access is gated through the property). Tests that need anonymous-type payloads (`new { Hello = "world" }`) use this; production code MUST NOT.
- **Test-local source-gen contexts** for test-only DTOs. `TestSerializationContext` in `Tests/SignalCli.Tests/TestSerializationContext.cs` registers `TestProbeRequest`/`TestProbeResponse` for `JsonRpcClientTests` — separate context, does NOT pollute production `SignalJsonContext`. Pattern: when a new test needs typed JSON probes, extend the test-local context, not the production one.
- **Wrapper-record + custom `JsonConverter` for List-shaped responses.** `ListAccountsResponse`/`ListGroupsResponse` are `record(IReadOnlyList<T> Items) : IReadOnlyList<T>` with `[JsonConverter]` that reads/writes a flat JSON array (delegating to `JsonSerializer.Deserialize<List<T>>(ref reader, JsonTypeInfo<List<T>>)`). Both the wrapper type AND `List<T>` MUST be in source-gen context. See `Models/Signal/Accounts/ListAccountsResponse.cs` for canonical shape.
- **`IHostedLifecycleService` + `IAsyncDisposable`** on `SignalCliHostedService` (post-`hosting-modernization` §8a.2/§8a.3). Phase-methods are no-op `Task.CompletedTask`; `DisposeAsync` drains `_operationLock.WaitAsync` with 2s `TimeProvider`-aware timeout, then runs shared `DisposeCore`. **Critical rule #9 enforced**: `Dispose()` is sync-only with its own implementation (NOT `DisposeAsync().GetAwaiter().GetResult()`).
- **`AddSignalCli(IConfiguration)` overload is NOT AOT-safe** (partial fix shipped). `<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>` is enabled in `SignalCli.csproj` and helps other source-gen-perceivable call-sites, but `OptionsBuilder.Bind<T>(IConfiguration)` itself is framework-annotated `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` у `Microsoft.Extensions.Options.ConfigurationExtensions` — source-gen НЕ перехоплює цей call-site. Both attributes lишилися на overload'і. AOT-targeting consumers MUST use `AddSignalCli(Action<SignalCliOptions>?)`. Full AOT-fix потребує rewrite away from `OptionsBuilder.Bind` (read section into dictionary, then call `services.Configure<T>`) — separate future change.
- **`VerifyReferenceTrimCompatibility` / `VerifyReferenceAotCompatibility` are deliberately NOT enabled** in `SignalCli.csproj`. Both flags warn about transitive dependencies that lack `IsTrimmable`/`IsAotCompatible` metadata (Microsoft *Prepare .NET libraries for trimming*). Our two non-trivial transitive dependencies — `System.Reactive` (not `IsTrimmable`-annotated as of 6.0.1) and `JetBrains.Annotations` (build-time only, PrivateAssets="all") — would flood the build with warnings without aiding correctness. If a future minor of `System.Reactive` ships `IsTrimmable`, opt in.

#### Regression guards (reflection-based defensive tests)

These tests pin CLAUDE.md-declared invariants at build time. Each is small (~50-100 LOC), reflection-based, and runs in the unit test suite. **When you introduce a new "do not regress" rule in CLAUDE.md, prefer adding a matching reflection-based guard over relying on narrative discipline.**

- **`JsonContextRegistrationTests`** (shipped in `post-modernize-tuning` §6.12) — every `*Parameters` / `*Response` DTO in `Models/Signal/*` MUST be registered in `SignalJsonContext`. Otherwise the source-gen-only JSON path throws `NotSupportedException` at runtime.
- **`ObsoleteMessageConsistencyTests`** (shipped in `audit-followup-2026/regression-guards`, v4.0.0) — every `[Obsolete("...; will be removed in N.0")]` message has N strictly greater than the current package major. Drift is the M-1 audit finding made impossible going forward.
- **`EventIdBlockTests`** (shipped in `audit-followup-2026/regression-guards`, v4.0.0) — every `[LoggerMessage(EventId = X)]` lies inside the block reserved for its `*Log.cs` class per the "Logging" table above. A new `[LoggerMessage(EventId = 250)]` on `JsonRpcClientLog` (whose block is 300-399) fails the build.
- **`PublicApiSurfaceTests`** (shipped in `audit-followup-2026/regression-guards`, v4.0.0) — baseline-diff at `Tests/SignalCli.Tests/RegressionGuards/SignalCli.public-api.txt` (1087 lines as of v4.0). Intentional public-API changes update the baseline in the same PR; accidental ones are caught immediately with unified-diff output telling the developer exactly which member to add/remove.

Privacy-guard tests (`PrivacyLoggingTests`, `ObservabilityPrivacyTests` with `MeterTagValues_AreOnlyKnownEnumLiterals`) are part of this family too — they pin Critical rule #1.

#### Mass-edit safety

- **PowerShell file I/O preserves encoding ONLY via `[System.IO.File]`.** `Get-Content -Raw` + `Set-Content -Encoding UTF8` mangles Cyrillic by reading via system codepage (often Windows-1251) and writing UTF-8-BOM. For batch-edits across `.cs` files use:
  ```powershell
  $text  = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
  $bytes = [System.IO.File]::ReadAllBytes($path)
  $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
  # ... mutate $text ...
  $enc = if ($hasBom) { New-Object System.Text.UTF8Encoding($true) } else { New-Object System.Text.UTF8Encoding($false) }
  [System.IO.File]::WriteAllText($path, $text, $enc)
  ```
  Mojibake'd cyrillic mid-batch is fixed by `git checkout` of the affected files + redo with the safe pattern. Symptom: `прибрано — клас` → `РїСЂРёР±СЂР°РЅРѕ вЂ" РєР»Р°СЃ`.
- **GitHub Actions `actions/*` SHAs MUST come from existing workflows in this repo**, not from notes or docs. `grep -rn "actions/" .github/workflows/*.yml | grep -v <new-file>` and copy. Typo-pinning produces fast-fail "Unable to resolve action" — round 16 lost one PR-cycle to a 1-char typo in `actions/checkout` SHA.
- **PowerShell `Get-FileHash` is fragile on `windows-latest` GitHub-runner** (rare `Microsoft.PowerShell.Utility` auto-load race). All download scripts (`src/build/download-jre.ps1`, `src/SignalCli.runtime/download-signal-cli.ps1`) compute SHA-256 directly via `System.Security.Cryptography.SHA256.Create().ComputeHash(stream)` + `BitConverter.ToString().Replace("-","")` — cross-version-safe across WinPS 5.1 and PS 7.x, no module-loading dependency. Don't revert to `Get-FileHash`.

#### Observability

- **Two surfaces only**, both named `"SignalCli.NET"`: `SignalCliDiagnostics.ActivitySource` for tracing (spans `rpc.<method>`, `signalcli.process.start`, `signalcli.healthcheck.ping`, `signalcli.subscribe`), `SignalCliDiagnostics.Meter` for metrics (`signalcli.rpc.requests`, `signalcli.rpc.duration`, `signalcli.process.restarts`, `signalcli.events.dropped`, `signalcli.subscriptions.active`). Adding new instruments goes in `SignalCli/Diagnostics/SignalCliDiagnostics.cs` only — do not spawn a second source.
- **Tag values are low-cardinality and PII-free.** The canonical set of tag keys is exactly `{method, status, trigger, event_type}`. `MeterTagValues_AreOnlyKnownEnumLiterals` in `ObservabilityPrivacyTests` pins this — if you add a new tag key, you MUST extend the test's `knownTagKeys` set and re-justify in the test fixture why the key is PII-free. Adding `account`/`phone`/`recipient`/`body` as a tag value is a privacy invariant violation (CLAUDE.md rule #1 — observability extension); the test catches it via literal-substring asserts on seed PII.
- **HealthChecks adapter is a separate optional package** (`SignalCli.NET.HealthChecks`). Core library NEVER takes a hard dependency on `Microsoft.Extensions.Diagnostics.HealthChecks` — it's generic-host-only and ASP.NET-independent, but consumers without a health-check pipeline shouldn't pay for it. The adapter reads `ProcessStateManager.CurrentState` (public) + `SignalCliHealthMonitor.LastPingResult` (internal, gated via `[InternalsVisibleTo("SignalCli.HealthChecks")]`). Data-bag fields: `state`, `last_ping_ok`, `last_ping_at` — no PII.
- **Listener-fan-out in tests must be thread-safe.** `ActivitySource.AddActivityListener` and `MeterListener` are global registrations; callbacks may arrive from parallel-test threads. Use `Lock` + snapshot pattern (see `ObservabilityPrivacyTests._captureLock`) for any captured-collection access, otherwise `List<T>` throws `Collection was modified` intermittently.

### Backward compatibility convention

When we deprecate API, the rule is **one major version of `[Obsolete]` shim** before removal.

**Already removed in 3.0** (see `CHANGELOG.md [3.0.0]`):
- `*Options.CancellationToken` field + `WithCancellationToken` builder method on `TextMessageOptions`/`AttachmentMessageOptions`/`StickerMessageOptions` (round 9 §4.7).
- `ISignalMessage.{SendText,SendAttachment,SendSticker}MessageAsync` returning `Task<List<SendMessageResponse>>` (now `Task<SendMessageResponse>`, round 9 §4.23-§4.24).
- `InvokeMethodAsync<TResponse, TRequest>` old generic-param order (round 9 §4.27) — no shim possible (C# overload resolution can't disambiguate generic-arity reorders).
- `FinishLinkResponse.number`/`SubscribeReceiveResponse.id` lowercase wire shape — replaced with PascalCase properties + `[JsonPropertyName]`.

**Currently in flight (will be removed in 4.0):**
- `ISignalCliClient.Version()` — DIM shim delegating to `VersionAsync()`. Still present in [`ISignalCliClient.cs:54-57`](src/SignalCli/Interfaces/SignalCli/ISignalCliClient.cs).
- `ServiceCollectionExtensions.AddSignalCli(Action<Config>?)` — legacy overload. Still present in [`ServiceCollectionExtensions.cs:123-139`](src/SignalCli/Extensions/ServiceCollectionExtensions.cs). Integration E2E tests still depend on `Config.CreateDefault()`-auto-resolve of bundled-JRE; tests use `#pragma CS0618 disable` around the call site. Real production consumers should migrate to `AddSignalCli(Action<SignalCliOptions>?)` or `AddSignalCli(IConfiguration)`.
- `SignalCli.Models.Config` itself — `[Obsolete]` class. Stays as long as the `Action<Config>?` overload + the `Config.ToOptions` / `SignalCliOptionsExtensions.ToOptions(Config)` / `ServiceCollectionExtensions.CopyFrom` triplet stay (see "Three-site duplication trap" below).
- `ISignalAccounts.ListAccounts` / `SyncAccount` / `ISignalDevices.StartLink` / `FinishLink` / `ISignalGroups.ListGroups` — Async-suffix-less shim methods, kept as `[Obsolete]` DIMs per round 9 §4.x.

**Doc-sync invariant.** Every `[Obsolete("...; will be removed in N.0")]` attribute message — N MUST be strictly greater than the current package major version. The same applies to Ukrainian XML doc / inline comments announcing "буде видалений у N.0" / "зникне у N.0" / "removed in N.0". Drift here lies to consumers and trains AI agents to disbelieve `[Obsolete]` lifetime claims. The `2026-05-24` audit found 6 sites still saying "3.0" in 3.0.0 source — `audit-followup-2026` capability `obsolete-doc-sync` corrects them and lands `ObsoleteMessageConsistencyTests` so drift becomes a build failure.

**Three-site duplication trap (in flight to 4.0).** Adding a new property to `SignalCliOptions` today requires updating three near-mirror field-copiers — `Config.ToOptions()`, `SignalCliOptionsExtensions.ToOptions(Config)`, and `ServiceCollectionExtensions.CopyFrom`. Collapsing them into one mapper now is throwaway work because all three disappear with `Config` in 4.0. Until then, when you add a property, update all three. There is intentionally no reflective drift-guard for this — the risk is bounded by the 4.0 cleanup horizon.

When adding a new deprecation, mirror this shape: real new API + `[Obsolete("Use Y; will be removed in N.0")]` shim that delegates, plus a `CHANGELOG.md` entry under "Інше". Internal call sites are migrated immediately; external call sites get one major release of grace. **Exception:** when the shim is technically impossible (generic-order, ctor-overload-ambiguity per `JsonRpcException` §4.22, etc.), do the pure removal and document the impossibility in the CHANGELOG migration note.

## Critical rules (do not regress — these are audit findings + post-2.1.0 invariants)

1. **Privacy:** never log message bodies, phone numbers, or attachment payloads above `Trace`. RPC params/results and raw stdin/stdout lines are `Trace`-only. `SignalService` logs the method name only. `[LoggerMessage]` templates at `Information+` MUST NOT reference PII fields. **The same prohibition applies to `Activity` tag values and `Meter` tag values** (observability surface from `post-modernize-tuning` §11 — shipped): only method names, status enums, integer ids, durations, exception type names — never message contents / phones / file paths. Privacy guard tests (`ObservabilityPrivacyTests` — single fixture covering both `ActivityListener` and `MeterListener` capture paths) enforce this with literal-substring asserts on a seed phone, seed message body, and seed file path; `MeterTagValues_AreOnlyKnownEnumLiterals` also pins the canonical tag-key set (`method`, `status`, `trigger`, `event_type`), so any new tag key spawned without test-fixture update fails loudly.
2. **Process arguments:** build the signal-cli command via `ProcessConfig.ArgumentList` (each arg separate). Never go back to a single interpolated `Arguments` string with quoted paths.
3. **Attachments:** sanitize `FileName` with `Path.GetFileName` (see `AttachmentEntry.SafeFileName`) before writing temp files or building data URIs — guard against path traversal.
4. **Event dispatch:** in `SignalEventService`, a `DataMessage` is a *presence-based union*; emit every applicable observable AND its paired async-channel (text + attachment can both fire). Do not reintroduce early `return` between payload checks.
5. **Text styles:** use `ToUpperInvariant()` for style names (locale-independent).
6. **Serialization:** `System.Text.Json` **only** — `Newtonsoft.Json` is removed. Annotate model members with `[JsonPropertyName]` (never `[JsonProperty]`). Register every new serializable root type in the source-generated context `Serialization/SignalJsonContext.cs` — **source-gen-only**: reflection fallback removed in `post-modernize-tuning` §6.4 (raund 14). Every type passed to `JsonSerializer.Serialize`/`Deserialize`/`SerializeToElement` from `src/SignalCli/**` MUST be in `SignalJsonContext`, or you get `NotSupportedException: Metadata for type ... was not provided` on runtime — `JsonContextRegistrationTests` (§6.12) catches the omission. Production code uses `JsonTypeInfo<T>`-based overloads (AOT-safe); test-only `SignalJson.OptionsForTests` carries the reflection-fallback resolver for anonymous-type test payloads (annotated `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`). `JsonRpcRequest.Params` / `JsonRpcResponse.Result` are `JsonElement` (registered in context for typed deserialization downstream).
7. **Download scripts:** `src/SignalCli.runtime/download-signal-cli.*` and `src/build/download-jre.*` verify the archive SHA-256 before extraction. **The canonical version + hash live in the runtime csproj** (`<SignalCliVersion>`/`<SignalCliSha256>` у `SignalCli.runtime.csproj`; `<JreVersion>`/`<JreSha256>` у обох `SignalCli.runtime.jre.*.csproj`); the scripts read those values as arguments. Bumping a pinned version = single-csproj edit, no script edits. The `.ps1` files are deliberately **ASCII-only** (no Cyrillic/emoji) so they parse under Windows PowerShell 5.1 **without** needing a UTF-8 BOM — keep them ASCII. They also invoke the Windows system `tar` (`%SystemRoot%\System32\tar.exe`) explicitly and stage extraction through an ASCII temp dir, because Git's GNU `tar` mis-reads `C:\…` paths and bsdtar fails on non-ASCII target paths.
8. **Bundled-JRE packages** (`SignalCli.Runtime.Jre.win-x64`, `SignalCli.Runtime.Jre.osx-arm64`): bundle a SHA-256-pinned Eclipse Temurin **25** JRE + signal-cli. The JRE and jars are packed as **single `.zip` files** and extracted by the consumer `.targets` via MSBuild's built-in `<Unzip>` — **do not** pack the JRE as individual files: NuGet treats an extension-less `PackagePath` (e.g. the JRE's `lib/modules`) as a *directory* and corrupts the layout, which crashes the JVM at bootstrap. `Config.ResolveBundledJava` auto-discovers `<output>/jre/bin/java[.exe]`, so consumers need no system Java and should **not** set `Config.JavaExecutable`.
9. **No sync-over-async in disposal.** `IJsonRpcClient` is `IAsyncDisposable`-only; never re-introduce a `Dispose()` that does `DisposeAsync().AsTask().GetAwaiter().GetResult()`. DI containers and `await using` are the supported paths.
10. **Fail-fast configuration.** `SignalCliOptions` validation (DataAnnotations + custom rule + `[OptionsValidator]` source-gen) is wired into `AddSignalCli`. Internal services read `_options.Value` in the constructor — that's deliberately where validation fires. Don't bypass it (e.g. don't capture `IOptions<>` and read `.Value` lazily in some method later).
11. **No wall-clock in tests.** Tests in the lifecycle/health-monitor suites must drive time via `FakeTimeProvider`. Re-introducing `await Task.Delay(>10ms)` to a test there is a regression.
12. **Options validation has exactly one path: source-gen `[OptionsValidator]`.** `ServiceCollectionExtensions.ConfigureOptions` registers `SignalCliOptionsValidator` (source-gen, reflection-free, AOT-safe) via `TryAddEnumerable<IValidateOptions<SignalCliOptions>>`. Cross-field rules go in `.Validate(o => …, "msg")` lambdas. **Do not re-add `.ValidateDataAnnotations()`** alongside the `[OptionsValidator]`: it duplicates the same `[Required]`/`[Range]` checks through reflection and is the reason `<IsAotCompatible>true</IsAotCompatible>` still trips IL2026 warnings. `post-modernize-tuning` §8b.8 removes the redundant call; do not bring it back.
13. **Source-gen JSON has no reflection fallback.** Every type passed to `JsonSerializer.Serialize`/`Deserialize`/`SerializeToElement` from `src/SignalCli/**` MUST be registered in `Serialization/SignalJsonContext.cs`. The `JsonContextRegistrationTests` suite (added in `post-modernize-tuning` §6.12) reflectively enumerates `InvokeMethodAsync<TRequest,TResponse>` call sites and asserts each type pair is in the context — if your new RPC method adds a DTO that's not in the context, this test fails loudly instead of producing silent `"{}"` payloads at runtime.
14. **Typed/idempotent state errors.** `SignalEventService.SubscribeAsync` is **idempotent** (post-`subscription-race-safety` §3.7): re-subscribing the same account returns the existing `subscriptionId` instead of throwing a generic `InvalidOperationException` with a locale-dependent Ukrainian message. Argument null/empty checks throw `ArgumentException` (via `ArgumentException.ThrowIfNullOrEmpty`) with the correct `paramName`. When you add new state-error sites elsewhere, mirror this: prefer idempotency over throwing; if you must throw, prefer a derived typed exception (or `ObjectDisposedException`/`ArgumentException` subclasses) over a generic `InvalidOperationException` so callers can pattern-match without inspecting the message text.
15. **AOT-safe JsonSerializer overloads only in production.** `<IsAotCompatible>true</IsAotCompatible>` is enabled on `src/SignalCli/SignalCli.csproj`. Every `JsonSerializer.Serialize`/`Deserialize`/`SerializeToElement` call in `src/SignalCli/**` MUST use the `JsonTypeInfo<T>`-taking overload, NOT the generic `<T>(_, options)` overload (which is reflection-based and trips IL2026/IL3050). `ISignalCliClient.InvokeMethodAsync<TRequest, TResponse>` requires `JsonTypeInfo<TRequest>` + `JsonTypeInfo<TResponse>` as explicit parameters — consumers pass them from `SignalJsonContext.Default.*`. The only production exception is `AddSignalCli(IConfiguration)` (annotated `[RequiresUnreferencedCode]`+`[RequiresDynamicCode]` because `Bind` uses reflection — AOT-targeting consumers must use `AddSignalCli(Action<SignalCliOptions>?)` instead).
16. **Integration E2E tests use legacy `Action<Config>` overload.** `Tests/SignalCli.Tests.Integration/SignalCliE2EVersionTests.cs` calls `services.AddSignalCli((Config cfg) => …)` inside `#pragma warning disable CS0618` because the legacy flow runs `Config.CreateDefault()` first — which auto-resolves the bundled-JRE path on Windows/macOS (`Config.ResolveBundledJava`) AND sets `LibDirectory = "SignalCli/lib"` (default) which satisfies `[Required(AllowEmptyStrings = false)]` on `SignalCliOptions`. The `Action<SignalCliOptions>?` overload skips both, so the test would fail with `OptionsValidationException`. **Do not "modernize" the Integration test off the legacy overload** until either (a) Config-shim is fully removed in 4.0, or (b) auto-resolve logic is migrated into the SignalCliOptions-overload path.
17. **`InternalsVisibleTo` is the seam for source-gen context in tests.** `SignalJsonContext` is `internal` to keep the source-gen layer hidden from consumers (they pass `JsonTypeInfo<T>` from their own contexts if they need custom). Both `Tests/SignalCli.Tests` and `Tests/SignalCli.Tests.Integration` have `InternalsVisibleTo` to access `SignalJsonContext.Default.*` for AOT-safe `InvokeMethodAsync` calls. **Do not make `SignalJsonContext` public** as a workaround for new test access — add the test project to `InternalsVisibleTo` in `src/SignalCli/SignalCli.csproj`.
18. **JSON deserialization hardening — dual-site enforcement.** Both production JSON layers SHALL reject duplicate JSON keys with `JsonException` (never silently follow last-wins semantics): (a) `SignalJson.Options.AllowDuplicateProperties = false` (runtime flag, covers any reflection-based call-site like `OptionsForTests`; lands in `audit-followup-2026 §json-hardening`); (b) `[JsonSourceGenerationOptions(AllowDuplicateProperties = false)]` on `SignalJsonContext` (source-gen attribute, covers every `SignalJsonContext.Default.X` call-site — this is what `JsonRpcClient.ProcessMessageAsync` and the rest of production actually use; lands in `json-hardening-source-gen-attribute`). **Both layers are required because they cover orthogonal code paths** — the runtime flag is dead-flag for source-gen Default fast-path call-sites; the source-gen attribute is the binding contract that propagates into the generated `Utf8JsonReader` loop. Removing either layer silently weakens defense-in-depth. Pinned by `RG05` ×3 facts in `JsonSerializationTests.cs`. We deliberately do NOT enable `JsonSerializerOptions.Strict`-preset because it implies `JsonUnmappedMemberHandling.Disallow`, which is incompatible with signal-cli's habit of adding new envelope fields between versions (forward-compat).

## Future development guardrails (audit categories)

This list captures CLAUDE.md-declared invariants that DO NOT yet have an executable regression-guard test. PRs that touch the relevant code SHOULD add the matching test (rather than waiting for an audit pass to discover the gap). When `audit-followup-2026` is archived, the entries marked `(in audit-followup-2026 §X)` move to "shipped" and stop being a TODO.

**Already shipped — moved out of this catalog (do NOT re-flag):**

- **JSON-RPC standard error codes** (`-32601`, `-32700`, `error.data` payload preservation): `JsonRpcErrorTests` (shipped in `audit-followup-2026` §6.a; T01 `InvokeMethodAsync_WhenBothResultAndErrorPresent_ErrorWins` added in audit v2.1 closes G9).
- **Attachment filename edge cases:** NUL byte, U+202E (RIGHT-TO-LEFT OVERRIDE), bidi controls, `SaveToTempFile` re-entry, exact boundary at `MaxInlineEncodedAttachmentBytes` (= **12 000 000** after `signal-cli-protocol-alignment`) — `AttachmentEntryTests` + `SignalMessageValidationTests.EncodedSize_OverBoundary_UsesTempFile` (shipped in `audit-followup-2026` §6.b).
- **`AtomicCounter` int32 wrap-around:** `UtilityEdgeCaseTests` (shipped in `audit-followup-2026` §6.c).
- **Observability counters fire on real events:** `signalcli.events.dropped`, `signalcli.rpc.duration`, `signalcli.process.restarts{trigger=force|crash|health}` — `ObservabilityCounterTests` (shipped in `audit-followup-2026` §6.d; T04/T05 added in audit v2.1 close `trigger=crash` + `trigger=health` subcases).
- **State-machine no-op paths:** `ForceRestartAsync` skipped in `Stopping`/`Stopped`/`NotStarted` (shipped in `audit-followup-2026` §6.f).
- **Channel-capacity boundary:** `NotificationChannelCapacity = 1` minimum FIFO (shipped in `audit-followup-2026` §6.g).
- **DI registration idempotency:** repeated `AddSignalCli` is no-op (shipped in `audit-followup-2026` §6.h).
- **`EnvironmentVariables` snapshot semantics:** read-only-dict type contract (shipped in `audit-followup-2026` §6.h).
- **`JsonRpcResponse` defensiveness:** when both `result` AND `error` are present, error wins → `JsonRpcException` (shipped in audit v2.1 T01).
- **Subscription leader cancellation propagation:** follower receives same `OperationCanceledException` (shipped in `audit-followup-2026` §6.e).
- **Event-API symmetry (10 paired surfaces):** every `IObservable<T>` has paired `IAsyncEnumerable<T>` — `RegressionGuards/EventApiSymmetryTests` (shipped in audit v2.1 RG06).
- **Version lockstep:** `SignalCli.NET` and `SignalCli.NET.HealthChecks` ship at the same assembly version — `RegressionGuards/VersionLockstepTests` (shipped in audit v2.1 RG07).

**Currently open — still no executable guard:**

_(empty as of audit v2.1 — all previously-declared invariants now have tests; this section will repopulate as new declared-but-untested invariants surface in future PRs.)_

**Rule for new PRs:** when you find an invariant CLAUDE.md declares but no test pins, choose ONE — (a) write the test in your PR, (b) add the gap to this catalog, (c) explicitly justify why testing is impractical (and add an `// CLAUDE.md guardrails: untested invariant` source comment at the relevant site).

## Audit baseline — invariants that MUST NOT regress

Цей список — мінімальна планка якості зафіксована після аудиту v2.1 (2026-05-24).
Будь-який PR що порушує хоча б один з цих пунктів МУСИТЬ бути відхилений або
супроводжуватись явним обґрунтуванням у CHANGELOG.

### Тестова база

- Unit tests: **≥ 287** (поточна планка після `json-hardening-source-gen-attribute` landing).
- E2E tests: **≥ 2** (bundled-JRE, не потребує live Signal account). Друга — `SignalCliE2EParallelRpcCorrelationTests.Process_ParallelVersionCalls_AllResolveToCorrectResponseById` — пінує CLAUDE.md "signal-cli protocol behavior we depend on" §3 (parallel request correlation by `id`) проти реального virtual-thread-dispatcher'а.
- `dotnet build` з `TreatWarningsAsErrors=true` — **обидва** проекти (`src/SignalCli`, `Tests/SignalCli.Tests`); Integration слідує тому ж шляху коли стане доцільним.
- Нуль `xUnit1031` violations (DoNotUseBlockingTaskOperationsInTestMethod). Якщо новий тест вимагає sync-blocking — додай `[SuppressMessage("xUnit", "xUnit1031", Justification="…")]` із поясненням, інакше build впаде.

### Regression guards — ВСІ мають бути зеленими

| Guard | Файл | Що pins |
|-------|------|---------|
| R01 | `JsonContextRegistrationTests.cs` | Кожен `*Parameters`/`*Response` DTO зареєстрований у `SignalJsonContext` |
| R02 | `RegressionGuards/EventIdBlockTests.cs` | `[LoggerMessage(EventId=…)]` лежить у блоці свого `*Log.cs` класу |
| R03 | `RegressionGuards/PublicApiSurfaceTests.cs` | Public API surface не змінюється без оновлення `SignalCli.public-api.txt` baseline |
| R04 | `RegressionGuards/ObsoleteMessageConsistencyTests.cs` | `[Obsolete("...will be removed in N.0")]` посилається на N строго > поточного major |
| RG05 | `JsonSerializationTests.cs` (3 facts: runtime flag + `JsonDocument` proxy + source-gen `SignalJsonContext` path) | Dual-site `AllowDuplicateProperties = false` enforcement on BOTH `SignalJson.Options` AND `[JsonSourceGenerationOptions]` (CLAUDE.md rule #18) |
| RG06 | `RegressionGuards/EventApiSymmetryTests.cs` | Кожен `IObservable<T>` на `ISignalEventService` має парний `IAsyncEnumerable<T>` метод |
| RG07 | `RegressionGuards/VersionLockstepTests.cs` | `SignalCli.NET.HealthChecks` assembly version == `SignalCli.NET` |

### Архітектурні інваріанти

- Жоден `JsonSerializer.Serialize/Deserialize/SerializeToElement` у `src/SignalCli/**` без `JsonTypeInfo<T>` overload (rule #15).
- Жоден `_logger.LogXxx("template", arg)` — тільки `[LoggerMessage]`-generated `partial` методи (CA1848/CA1873 green).
- Жоден `new CancellationTokenSource(TimeSpan)` у класі що ін'єктить `TimeProvider` — тільки overload `(TimeSpan, TimeProvider)` (Established patterns → "TimeProvider consistency").
- Жоден `Task.Delay(>10ms)` у тестах із `SignalCliHealthMonitor/` чи `SignalCliHostedService/Restart*/` — тільки `FakeTimeProvider.Advance` (rule #11).
- `<SignalCliPackageVersion>` живе **ТІЛЬКИ** у `Directory.Build.props` — не хардкодити `<Version>` у `SignalCli.csproj` або `SignalCli.HealthChecks.csproj`.

### Версійна синхронізація

`SignalCli.NET` і `SignalCli.NET.HealthChecks` ЗАВЖДИ мають однакову версію. Адаптер бінарно прив'язаний до main lib через `[InternalsVisibleTo("SignalCli.HealthChecks")]` — divergent versions = `MissingMethodException` на першому health-check-probe в продакшені. Єдине місце де версія визначається: `Directory.Build.props → <SignalCliPackageVersion>`. Enforced: `VersionLockstepTests.MainLibAndHealthChecksAdapter_ShareExactSameAssemblyVersion`.

### Version-CHANGELOG lockstep

**Кожен bump `<SignalCliPackageVersion>` у `Directory.Build.props` МУСИТЬ супроводжуватись відповідною `## [X.Y.Z] — YYYY-MM-DD` секцією у `CHANGELOG.md` — у **тому самому коміті**.** Без винятків. Включно з patch-bumps (4.0.1 → 4.0.2), включно з test-only fix-релізами, включно з doc-sync-патчами. Якщо тобі нема що написати — релізу не повинно бути; не bump'ай version "про запас".

Структура секції CHANGELOG — Keep-a-Changelog шаблон з цього файлу:
- `## [X.Y.Z] — YYYY-MM-DD` header + 1-2 речення опису-релізу (motivation + scope).
- Підсекції з emoji-prefix: `### 🐛 Виправлено`, `### ✨ Додано`, `### 🛡️ Захист від регресій`, `### 🛠 Інше`, `### Pending follow-up`.
- При capability-batches: вкладений `#### Capability \`name\`` — точне ім'я OpenSpec-capability'ї (з `openspec/changes/<change>/proposal.md`).
- Bold leading clause + пояснювальний текст з посиланнями на file:line / spec / MS Learn — щоб майбутній агент (включно зі мною) міг reconstruct *чому*, не лише *що*.

Кожен попередній реліз у `CHANGELOG.md` (4.0.0, 4.0.1, 4.0.2, 3.0.0, 2.1.0…) має таку секцію — це консистентна історична convention. Silent version bumps тренують споживачів ігнорувати CHANGELOG і ламають NuGet release notes (nuget.org підтягує `<PackageReleaseNotes>` саме з CHANGELOG entries — порожня секція = порожні release notes на nuget.org).

Перевірка при PR: `git diff <base>..HEAD -- Directory.Build.props CHANGELOG.md` — якщо одне змінилось без іншого, перевір чому. Якщо CI бачить version bump без CHANGELOG diff'у — review SHOULD flag перед merge. (Reflection-based regression guard для цього неможливий — CHANGELOG.md не доступний з runtime-assembly; це enforce'ується процесом review, не build-failure.)

Rationale-приклад чому це matter: під час audit v2.1 знайшли що "CHANGELOG [4.0.1] навіть стверджував що `JsonRpcResponse` з обома полями покрито — `grep` показав що ні" (CLAUDE.md "How we discovered → Test gap при рефакторингу"). Якщо CHANGELOG бреше, агенти не довіряють йому й роблять зайвий audit-pass. Правда у CHANGELOG = менше audit-роботи.

### CHANGELOG voice template

Кожен `## [X.Y.Z]` entry пишеться для **трьох аудиторій** у такому порядку пріоритету:

1. **Library consumers** (хто залежить від NuGet-пакету) — *що змінилось у МОЄМУ житті? Чи треба мені оновлюватись? Чи це мене зламає?* Це перші, що читають release notes на nuget.org.
2. **Contributors** (хто працює в репо) — *яка була underlying технічна зміна?*
3. **Future maintainers / AI agents** (читають цей список через 6 місяців) — *де file:line якорі для re-verify?*

#### Правила per bullet

- **Починай кожен bullet з bold-claim'у user-facing мовою:**
  - ✅ "Якщо ти використовуєш X — онови Y разом, інакше runtime crash"
  - ✅ "Видалено: ось як мігрувати: `s/old/new/g`"
  - ✅ "Тепер працює без silent Kill — критичний bug з 1.0"
  - ❌ "Capability `xyz-fix-123` (NF-007 / G2)" — internal taxonomy, consumer'и bounce'нуть на першому абзаці
  - ❌ "Refactored `JsonRpcClient.cs:494` per audit v2.1 RG05 implementation" — meta-narrative про audit-процес, не про change

- **Потім 1–2 речення plain-language пояснення** *що / навіщо*. Acronyms expand'нуті на першому use.

- **Потім (опційно) технічна обгортка** в italics або parens для tracer'ів: file:line, method names, OpenSpec capability slug. **Cap: 2–3 lines max.** Walls of code-citations без "why this matters" framing'у — рефактори.

- **Internal IDs (`NF-XXX`, `RG05`, `T01`, `G4`, capability slugs)** — в кінці bullet'у, в italics-parens: `*(NF-003, RG07)*` або `*([openspec-name](openspec/changes/...))*`.

- **Не більше одного `#### Capability \`name\`` subheader'у per `###` секція.** Consumer'и які скан'ять CHANGELOG для impact'у потребують flat readable lists, не nested taxonomy. Якщо у тебе багато дрібних items в одній capability — згрупуй їх під ОДНИМ bold leading bullet'ом, з вкладеним списком plain-language sub-items.

#### Example — bad vs good

❌ **Bad** (audit v2.0/v2.1 default style — те, що я писав initially у [4.0.2] before C-rewrite):

```
#### Capability `healthchecks-version-sync` (NF-003)
- **`SignalCli.NET.HealthChecks` версія більше НЕ хардкодиться — централізована в `Directory.Build.props`.**
  До 4.0.2 main lib був `4.0.1`, а adapter csproj мав хардкод `<Version>3.0.0</Version>` — divergent versions
  = `MissingMethodException` на першому health-check-probe у консумерах (adapter читає internal'и main lib
  через `[InternalsVisibleTo("SignalCli.HealthChecks")]`). Тепер обидва csproj читають
  `$(SignalCliPackageVersion)`...
```

✅ **Good** (those same facts, consumer-first voice):

```
- **Якщо ти використовуєш `SignalCli.NET.HealthChecks` — онови його разом з main package.**
  До 4.0.2 версії розійшлися (4.0.1 vs 3.0.0), і змішування пакетів давало `MissingMethodException`
  на першому health-check probe. Тепер обидва ідуть в lockstep — `4.0.2` для обох. Bug-class виключено:
  новий тест `VersionLockstepTests` ловить розбіжність версій до merge'у. *(NF-003, RG07)*
```

"Good" версія на ~20% коротша, але перші 12 слів кажуть consumer'у все що йому потрібно знати; технічна обгортка приходить як supporting context, не як wall.

#### Коли в одній capability багато дрібних items

Use **single bold leading bullet + nested plain-language sub-items**, НЕ nested `####` capability headers per item:

```
- **Тестова hygiene tightened — 3 невидимих warnings зникли:**
  - Test csproj тепер відмовляється builder'итись при warnings (як main lib давно).
  - Виправлено 3 deadlock-prone `.GetAwaiter().GetResult()` у `SyncDisposeDuringCleanupTests`.
  - `Microsoft.Extensions.*` test packages bumped 9.0.0 → 10.0.0 (no breaking changes per MS Learn).
  *(NF-004, NF-005)*
```

#### Чому це matter

NuGet.org підтягує перші ~200 символів CHANGELOG-секції як `<PackageReleaseNotes>`. Якщо там "Capability `xyz` (NF-007)" замість "Якщо ти використовуєш X — онови Y разом", consumer на nuget.org бачить шум і пропускає release-картку. Версії [4.0.0], [4.0.1], [4.0.2] переписано в цьому стилі задля консистенції; будь-яка майбутня версія дотримується того ж шаблону.

## How we discovered these issues — prevention checklist

Всі знахідки з аудиту v2.0/v2.1 потрапили в кодову базу через один з цих сценаріїв. При кожному PR перевір що ти не повторюєш той самий паттерн:

### Package version drift (→ NF-003, NF-005)

**Що сталося:** `SignalCli.NET.HealthChecks.csproj` мав хардкодовану `<Version>3.0.0</Version>` поки main lib вже був на `4.0.1`. Окремо: `Microsoft.Extensions.TimeProvider.Testing` і `Microsoft.Extensions.Diagnostics.Testing` залишились на `9.0.0` поки решта Microsoft.Extensions.* перейшли на `10.0.0`.

**Перевірка при PR:** якщо змінюєш версію в будь-якому csproj — `grep -rn "<Version>" src/ Tests/` і подивись чи всі релевантні csproj оновлені. Якщо версія має бути спільною — вона МУСИТЬ йти через MSBuild property у `Directory.Build.props`, не хардкодом.

### Silent warnings у test project (→ NF-004)

**Що сталося:** `TreatWarningsAsErrors=true` був тільки в `src/SignalCli.csproj`, не в `Tests/SignalCli.Tests.csproj`. Три xUnit1031 violations тихо жили в CI місяцями, бо CI не fail'ив на test-project warning.

**Перевірка при PR:** `dotnet build` має бути 0 warnings в **обох** проєктах. Додаючи новий test-файл — переконайся що він не вводить аналізатор-warning (зокрема xUnit1031: ніяких `.GetAwaiter().GetResult()` / `.Wait()` / `.Result` на `Task`). Для тестів що навмисно тестують sync-path (як `SyncDisposeDuringCleanupTests.Dispose()`) — лиш sync API залишається sync; `StartAsync`/`StopAsync` обгортаючи з `await`.

### Test gap при рефакторингу (→ NF-001, G4 subcases)

**Що сталося:** Логіка "error wins over result" у `JsonRpcClient.cs:494` і observability trigger subcases (`crash` / `health`) були коректно реалізовані але не мали тестів. CHANGELOG [4.0.1] навіть стверджував що `JsonRpcResponse` з обома полями покрито — `grep` показав що ні. Рефакторинг міг мовчки зламати їх.

**Перевірка при PR:** якщо змінюєш файл де є CLAUDE.md "Future development guardrails" bullet — перевір що для цього bullet існує тест. Якщо ні — додай перед мержем. Якщо CHANGELOG говорить "тест X covered" — `grep` репозиторій на ім'я тесту, не довіряй на слово.

### Doc/code constant drift (→ NF-006)

**Що сталося:** `MaxInlineEncodedAttachmentBytes` змінили з `15_000_000` на `12_000_000` у `signal-cli-protocol-alignment`, але CLAUDE.md "Future development guardrails" bullet залишився із `(= 15 000 000)`. Той же bullet був у "untested" поки тест `EncodedSize_OverBoundary_UsesTempFile` уже існував.

**Перевірка при PR:** якщо змінюєш будь-яку іменовану константу або threshold у `src/SignalCli/**` — `grep CLAUDE.md` на стару назву **І** на стару величину. Аналогічно: якщо додаєш тест на CLAUDE.md-задекларовану invariant — перенеси bullet із "untested" у "shipped" у тому ж PR.

### Missing regression guard для нового патерну (→ NF-002)

**Що сталося:** "кожен `IObservable<T>` має парний `IAsyncEnumerable<T>`" — правило існувало у "Established patterns" розділі, але не було машинно-верифіковане. Новий event kind без парного методу пройшов би code review (компілився б, тести б проходили).

**Правило:** кожен новий "Established patterns" bullet у CLAUDE.md МУСИТЬ мати відповідний regression guard у таблиці "Audit baseline" вище. Якщо додаєш новий патерн — одразу додай guard. Якщо такого guard'а ще не існує і його неможливо швидко скласти — поясни чому в `// CLAUDE.md guardrails: untested invariant` коменті біля сайту pattern'у.

## Planning (OpenSpec)

This repo uses [OpenSpec](https://github.com/Fission-AI/OpenSpec) for change planning under `openspec/changes/`. For non-trivial work, create/extend a change (proposal → design → specs → tasks) and run `npx -y @fission-ai/openspec@latest validate <change> --strict` before implementing.

**Implemented, merged, archived** (historical reference, do not re-open — all in `openspec/changes/archive/2026-05-24-*/`):
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
- `post-modernize-tuning` (**3.0.0**, archived 2026-05-24) — 14 capabilities including AOT readiness (`<IsAotCompatible>true</IsAotCompatible>` with `JsonTypeInfo<T>`-based `InvokeMethodAsync` redesign), observability (`ActivitySource`/`Meter` `"SignalCli.NET"` + optional `SignalCli.NET.HealthChecks` package), RPC back-pressure (bounded notification channel with `FullMode=Wait`), state-machine thread-safety (snapshot-then-emit, no reentrant deadlock), subscription race-safety (reservation TCS pattern; idempotent `SubscribeAsync`), hosting modernization (`IHostedLifecycleService` + `IAsyncDisposable` on `SignalCliHostedService` with 2s drain), options-validation tightening (`IConfiguration`-overload), supply-chain hardening (`actions/*` SHA-pinned, csproj-anchored versions), v3.0 breaking-API wave (PascalCase responses, single `SendMessageResponse` return, generic-param reversal, `*Options.CancellationToken` removed, wrapper records for `ListAccountsResponse`/`ListGroupsResponse`, `JsonRpcException` canonical code -32603). 215 unit tests + Linux runtime-smoke CI workflow.
- `signal-cli-protocol-alignment` (**4.0.0**, archived 2026-05-24) — pinned 7 upstream signal-cli protocol facts (stdin EOF graceful, stdout pure-JSON line-flushed, virtual-thread parallel dispatch, `subscribeReceive` non-idempotent at protocol level, Jackson `maxStringLength = 20M`, custom error codes `-1..-6`, Java 25 requirement) with `file:line @ bda4e7f` citations in new CLAUDE.md "signal-cli protocol behavior we depend on" section. **Critical correctness fix:** `graceful-shutdown-fix` — `SignalCliHostedService.StopProcessInternalAsync` now closes stdin instead of writing `"exit"` literal; previous behavior was silent `Kill(entireProcessTree)` after parse-error timeout, potential SQLite corruption. **Typed RPC errors:** new `JsonRpcErrorCode` enum + `RateLimitException` / `UntrustedIdentityException` derived types. **Attachment threshold** lowered 15M → 12M (Jackson `maxStringLength` 20M cap + 4M margin).
- `audit-followup-2026` (**4.0.0**, archived 2026-05-24) — 9 capabilities: `obsolete-doc-sync` (6 stale `[Obsolete("3.0")]` → `4.0`), `json-hardening` (`AllowDuplicateProperties = false` on runtime options), `configuration-binder-aot` (foundation for AOT-safe `AddSignalCli(IConfiguration)` — completed in 4.0.1), `regression-guards` (`ObsoleteMessageConsistencyTests` + `EventIdBlockTests` + `PublicApiSurfaceTests` reflection-based, 1087-line baseline), `integration-tests-expansion` (`SignalCliE2EGracefulShutdownTests` + 5 skip-gated E2E in 4.0.1), `edge-case-coverage` (12 declared invariants pinned: JSON-RPC error codes, attachment boundary, AtomicCounter wrap, EventsDropped counter, etc.), `addsignalcli-idempotency-fix` (sentinel-type marker replaces broken IOptions<>-presence guard), `badge-url-fix` (absolute raw.githubusercontent.com URLs + NuGet README), `low-priority-polish` (`(SignalCliOptions o)` typed lambda in Example, .NET 10 `BackgroundService.ExecuteAsync` behavior change doc).
- `deprecated-shim-removal` (**4.0.0**, archived 2026-05-24) — BREAKING: deleted `SignalCli.Models.Config` class, `AddSignalCli(Action<Config>?)` overload, `SignalCliOptions.ToConfig()` + 3 mapper helpers, `ISignalCliClient.Version()` DIM, 5 Async-suffix-less DIMs (`ListAccounts`, `SyncAccount`, `StartLink`, `FinishLink`, `ListGroups`). Replacement: `AddSignalCliWithBundledRuntimeDefaults(Action<SignalCliOptions>?)` for bundled-runtime consumers; `s/\.Foo(/\.FooAsync(/g` for Async-suffix migration. Migration table in `CHANGELOG.md [4.0.0]`.
- `json-hardening-source-gen-attribute` (**4.0.2**, archived 2026-05-24, [audit v2.1](https://github.com/07artem132/SignalCli.NET/pull/new/fix/post-audit-remediation) follow-up) — closes the dead-flag prog in CLAUDE.md rule #18: runtime `SignalJson.Options.AllowDuplicateProperties = false` did NOT fire on production path because `SignalJsonContext` source-gen `GenerationMode = Default` fast-path generates its own `Utf8JsonReader` that ignores runtime flag. Fix: `[JsonSourceGenerationOptions(AllowDuplicateProperties = false)]` on the context. Pinned by `RG05` ×3 facts (runtime flag + .NET 10 `JsonDocument` API + source-gen path).
- `e2e-coverage-expansion` (**4.0.2**, archived 2026-05-24, audit v2.1 follow-up) — single new E2E `SignalCliE2EParallelRpcCorrelationTests` that fires 10 concurrent `version`-RPC through bundled JRE against real signal-cli virtual-thread dispatcher (`JsonRpcReader.java:58`). Pins CLAUDE.md "signal-cli protocol behavior" §3 (parallel request correlation by `id`, not order) against the real upstream — unit tests only covered correlation via in-order `Subject<T>` mock. Integration scorecard: 7 → 8.

**Pending changes:** _(none)_ — `openspec/changes/` має лише `archive/` subdirectory; start a new change to add work.

When you start a new material piece of work, create a new `openspec/changes/<change-name>/` directory with `proposal.md` / `design.md` / `tasks.md` / `specs/<capability>/spec.md`, mirror the structure of an archived change (e.g. `archive/2026-05-24-agent-friendly-modernization/`), and run `openspec validate <change> --strict` before implementing.

**Post-merge archive workflow (canonical):**

```bash
# 1. After PR merges to main:
git checkout main && git pull
# 2. Archive (uses today's date as prefix, --skip-specs matches repo pattern —
#    we do NOT maintain a top-level openspec/specs/ tree; spec content lives
#    inside each change directory and moves with it to archive/):
npx -y @fission-ai/openspec@latest archive <change-name> --yes --skip-specs
# 3. Commit the file moves:
git add -A && git commit -m "chore(openspec): archive <change-name> → YYYY-MM-DD"
# 4. Rebase against coverage-bot auto-commit, then push:
git pull --rebase origin main && git push origin main
# 5. Update CLAUDE.md "Implemented, merged, archived" list to add the new entry
#    with archive-path pointer, in a follow-up commit.
```

`--skip-specs` is mandatory in this repo: previous changes never synced delta-specs to `openspec/specs/`, and switching now would create two sources of truth. Spec content is read from `openspec/changes/archive/<date>-<name>/specs/<capability>/spec.md` when referenced.

## Working style (how Claude and the user collaborate on this repo)

These are conventions we landed during the 2.1.0 work. They aren't strict — but they're what worked and what we expect from each other going forward.

- **Plan first, then implement.** Non-trivial work goes through OpenSpec (proposal → design → tasks → spec.md per capability). The plan should be small enough to validate (`openspec validate --strict`) and explicit enough that any subset is independently shippable.
- **One commit per capability/cluster.** When implementing a multi-cluster OpenSpec change, each capability lands as its own commit with a clear message. Cluster A → cluster B → … is easier to review and bisect than a single mega-commit. Final batch (docs, version bump, leftover items) goes in one trailing commit.
- **`dotnet build` + `dotnet test --no-build` after every cluster.** If the test count drops or a new flake appears, stop and diagnose before moving on. The suite is 215/215 stable — drift is the early-warning sign.
- **Don't claim a flaky test is "pre-existing" without a baseline check.** If a test fails under your changes, `git stash`, rebuild + retest at HEAD, compare. We diagnosed real flake (the `ForceRestart*Delay*` family) this way and migrated it to `FakeTimeProvider` rather than living with it.
- **Subagents (`Explore`, etc.) for parallel research, not for write tasks.** Most of the implementation work in 2.1.0 was direct edits in the main agent; subagents are useful for "find me all callsites of X" or "check whether Y exists in the test suite" but not for "implement cluster D for me."
- **Comments and log messages stay in Ukrainian.** Match the codebase's voice when you edit. The CHANGELOG, README, and PR/commit titles can be Ukrainian or English — mirror the surrounding style.
- **Don't create `*.md` documentation files unless asked.** This `CLAUDE.md`, `README.md`, and `CHANGELOG.md` are the only durable docs we maintain. Working notes belong in OpenSpec change documents.
- **Don't add `[Obsolete]` shims for code that has no real external consumer** — just delete and document in `CHANGELOG.md`. Reserve the shim convention for things that we know are in user code (e.g. `Version()`, the `Config`-based registration, the deprecated `*Options.CancellationToken`).
- **Use the `microsoft-docs` MCP for any .NET/Microsoft API question before coding.** Tools: `mcp__microsoft-docs__microsoft_docs_search`, `microsoft_code_sample_search`, `microsoft_docs_fetch`. Examples of past saves: confirmed `AddInMemoryCollection` ships inside `Microsoft.Extensions.Configuration` (no standalone `…Configuration.Memory` package on nuget.org); pinned the AOT-safe `JsonSerializer.SerializeToElement(value, JsonTypeInfo<T>)` / `JsonElement.Deserialize(JsonTypeInfo<T>)` overload signatures before redesigning `InvokeMethodAsync`; confirmed `Microsoft.Extensions.Diagnostics.Testing` is the package id for `FakeLogger<T>`. Use this *before* speculatively adding a `<PackageReference>` or guessing a method name — guessing wastes round-trips on non-existent packages or wrong overloads.
- **Custom CI workflows: prefer static-check over consumer-build-simulation.** `runtime-smoke.yml` `jre-guard-static-check` `grep`s the `.targets` files for the post-extract `<Error Condition>` guard text + an actionable hint — catches the "guard removed" regression class in 3 seconds on ubuntu-latest. The original attempt to simulate consumer-build by deleting `bin/java` after JRE-package build never triggered the guard (it lives on consumer's `TargetDir`, not the runtime-package's own build). When a CI check needs a real consumer, look for an existing one (e.g. `Tests/SignalCli.Tests.Integration` for native delivery) instead of bolting on a synthetic consumer project.
- **PR webhook auto-handling: skip purely informational bot comments.** `github-actions[bot]` posts coverage badges (`marocchino/sticky-pull-request-comment`) and `Test Results 0/0` after every CI run; these are NOT review comments and require NO action. Address only real CI failures and human-author comments. CI-failure response loop: read `gh api repos/<owner>/<repo>/actions/jobs/<id>/logs` (individual job log, more reliable than `gh run view`), find root cause, fix, push. Most failures during this PR cluster batched into single fix-commits per CI-cycle.
- **`git pull --rebase` before every push to `main`.** Automated coverage-badge bot (`stefanzweifel/git-auto-commit-action`) commits to main after every successful CI run with `[skip ci]` — your local main lags within minutes of any merge. Force-pushes are forbidden (CLAUDE.md "Git" section); rebase is the only safe path.
- **Verify-then-tick is the bookkeeping rule for OpenSpec tasks.** Don't mass-`sed 's/\[ \]/[x]/'` without first confirming each unchecked task is actually shipped. Round-16 audit found `agent-friendly-modernization` with 55 unchecked tasks but CLAUDE.md confirmed "shipped as 2.1.0" — safe to bulk-tick. Generalize: cross-reference CLAUDE.md "Implemented and merged" before sweeping ticks; if status ambiguous, leave for explicit review.
- **Cross-check CLAUDE.md "Implemented, merged, archived" against live source before claiming a deprecation is "already removed".** The 2026-05-24 audit found CLAUDE.md said `Version()` and `AddSignalCli(Action<Config>?)` were "Already removed in 3.0" while both still existed in source. The check is one `grep` (`grep -rn '\[Obsolete' src/`) — do it before editing the "Backward compatibility convention" section. Drift here trains agents to disbelieve the doc; the `ObsoleteMessageConsistencyTests` regression-guard (in `audit-followup-2026`) automates this.
- **When the audit lists a HIGH / MEDIUM finding, file an OpenSpec change before fixing — even tiny doc-sync fixes.** The regression-guard test that prevents recurrence is the durable artifact, not the fix itself. The fix without the guard is one less file showing the right text; the fix with the guard is a permanent invariant. `audit-followup-2026` shape: each capability has its own spec; one commit per capability; final test-count post-merge documented in the proposal.
- **Validate the agent instructions periodically.** Microsoft's *Custom instructions for AI agents* guide recommends: "Test your custom instructions by asking the AI to write a representative task; if the AI still produces the wrong pattern, add a more explicit rule." Apply this to CLAUDE.md — when you notice an agent (Claude, Copilot, etc.) repeatedly violating a rule we thought was written, the rule is too implicit. Make it explicit, with a code-anchored example, in the relevant "Established patterns" subsection.
- **GitHub Copilot reads `.github/copilot-instructions.md`, not `CLAUDE.md`.** Our repo currently has only `CLAUDE.md`. If a contributor uses Copilot/Cursor/Windsurf on this repo and needs the same patterns to apply, a tiny `.github/copilot-instructions.md` containing a single line ("This project's authoritative agent guidance lives in `CLAUDE.md`. Read it first.") is sufficient — multiplying the patterns into multiple files would create a drift-vector. Not added today; mentioned as the cheap escape valve if Copilot users complain.
- **The `awesome-copilot` `CSharpExpert.agent.md` is a complementary reference**, not a replacement for CLAUDE.md. CLAUDE.md describes *this* project's invariants; `CSharpExpert` describes general modern-C# conventions. Both can apply; do not duplicate.

## Git

Work on a feature branch; do not push or commit unless asked. When commits are requested, prefer one commit per OpenSpec capability (see "Working style" above). Never amend already-pushed commits without explicit approval.
