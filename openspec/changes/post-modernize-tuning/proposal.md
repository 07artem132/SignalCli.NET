## Why

After `comprehensive-code-audit` (2025-Q1) and its remediation pass `address-audit-findings-2`, two follow-up audit sessions (recorded in chat, grounded in Microsoft Learn via the docs-MCP) surfaced a second wave of issues that were **out of scope** of the original audit:

1. **Regressions introduced by the remediation itself.**
   - F25 fix moved `BehaviorSubject.OnNext` *inside* `ProcessStateManager._lock` to close a dispose-vs-emit race — but a synchronous Rx subscriber that re-enters now risks reentrancy-deadlock.
   - F4 fix added `IAsyncDisposable` to `JsonRpcClient` while keeping the legacy `IDisposable.Dispose()` implemented as `DisposeAsync().AsTask().GetAwaiter().GetResult()` — a sync-over-async anti-pattern (MS *Common async/await bugs*).
2. **Architectural items not in the original "bugs" scope.**
   - No back-pressure between the stdout reader and the notification fan-out: a slow subscriber can stall the JSON-RPC stream.
   - `SignalEventService.SubscribeAsync` has a TOCTOU window that can leak a subscription on `signal-cli` if two callers race on the same account.
   - Modernization items deliberately deferred: `BackgroundService` instead of hand-rolled `Task.Run` for the health loop; `IHostedLifecycleService` for explicit startup ordering; `SemaphoreSlim` instead of `Nito.AsyncEx` (cuts a non-trim-safe dependency); Options pattern with `ValidateOnStart`.
3. **Public API / DX defects that are awkward for AI agents and humans alike.**
   - Two response records expose lowercase properties (`FinishLinkResponse.number`, `SubscribeReceiveResponse.id`) — violates Microsoft's *Capitalization conventions*.
   - Six public async methods (`ListAccounts`, `StartLink`, `FinishLink`, `ListGroups`, `Version`, `SyncAccount`) miss the mandatory `Async` suffix.
   - `CancellationToken` is stored as a property on options records, polluting record equality.
   - The bundled example does `var response = signalMessage.SendTextMessageAsync(...)` (fire-and-forget) and `host.StopAsync().Wait()` — AI agents that copy-paste it inherit both bugs.
4. **Tests and CI hygiene.**
   - 12 unit tests still rely on wall-clock `Task.Delay(...)` — re-flake risk under CI load (one path already virtualized with `FakeTimeProvider`, the pattern just needs to spread).
   - Several testbases reach into private fields by reflection (`GetPrivateField<T>`) — brittle to refactors.
   - The new race-condition findings have no test coverage.
   - xUnit v2 is locked in, no migration plan to v3 + Microsoft.Testing.Platform.
5. **Hot-path performance & AOT readiness.**
   - 106 `_logger.LogXxx(...)` calls across services — `CA1848` would flag every one of them. The stdout reader and notification dispatcher log on every line/message and box enum/int parameters.
   - `JsonSourceGenerationMode.Metadata` skips fast-path serialization.
   - `<IsAotCompatible>true</IsAotCompatible>` is not set; the source-gen + reflection-fallback resolver and the `Nito.AsyncEx` dependency both block trimming/AOT.
6. **Cloud-development setup.** The project has no documented Claude-Code-on-the-Web onboarding: every cloud session pays the cost of installing the .NET SDK and discovering NuGet credentials from scratch.

## What Changes

Grouped by capability (each is a separate spec under `specs/`):

- **`rpc-back-pressure`** — `System.Threading.Channels.Channel<JsonRpcNotificationRaw>` between stdout reader and notification fan-out; **`JsonRpcClient` becomes `IAsyncDisposable` only** (the sync-over-async bridge `DisposeAsync().AsTask().GetAwaiter().GetResult()` is removed); request serialization composes via `Utf8JsonWriter` directly instead of `SerializeToElement` + `Serialize` two-pass.
- **`state-machine-thread-safety`** — `ProcessStateManager.UpdateState` snapshots state under the lock, then `OnNext`-s outside it; `_disposed` flips via `Interlocked` everywhere.
- **`subscription-race-safety`** — `SignalEventService.SubscribeAsync` reservation placeholder; orphan `subscribeReceive` on signal-cli no longer possible.
- **`hosting-modernization`** — `SignalCliHealthMonitor` becomes a `BackgroundService`; startup ordering between `SignalCliHostedService` and `JsonRpcClientHostedService` becomes explicit through `IHostedLifecycleService`; `SignalCliHostedService` gains `IAsyncDisposable`; `ProcessRunner.StartProcessWithHandle` is no longer `Task.FromResult`-wrapped sync work.
- **`options-validation`** — `Config` registered via `AddOptions<Config>().ValidateDataAnnotations().ValidateOnStart()`; `[Range]`/`[Required]` constraints on every numeric/path field; `Config` becomes fully `init`-only (no `set`); new `services.AddSignalCli(IConfiguration)` overload for `appsettings.json` binding.
- **`agent-friendly-api`** — public API breaks in one wave (v3.0.0):
  - `FinishLinkResponse(string Number)` and `SubscribeReceiveResponse(int Id)` PascalCase with `[JsonPropertyName]`.
  - `Async` suffix on `ListAccountsAsync`, `StartLinkAsync`, `FinishLinkAsync`, `ListGroupsAsync`, `VersionAsync`, `SyncAccountAsync`.
  - `CancellationToken` removed from options records.
  - `SignalCliHostedService` sealed; `EnvironmentVariables` becomes `IReadOnlyDictionary`.
  - `JsonRpcRequest` record body emptied.
  - `IRecipient` becomes a sealed type hierarchy (no boolean `IsGroup`).
  - `*EventArgs` records refactored to hold a single `Envelope` reference instead of 10 duplicated string fields.
  - `Example/Program.cs` rewritten as `async Task Main`.
- **`high-performance-logging`** — every `_logger.LogXxx` site in `src/SignalCli/Services/**` migrates to `[LoggerMessage]` source-gen; privacy invariant preserved verbatim.
- **`aot-readiness`** — `<IsAotCompatible>true</IsAotCompatible>`; drop `Nito.AsyncEx` for `SemaphoreSlim`; drop reflection fallback in `SignalJson.Options`; `JsonSourceGenerationMode.Default` (fast-path).
- **`test-virtualization`** — every wall-clock `Task.Delay` → `FakeTimeProvider`; reflection test helpers → `internal TestSeam`; race tests for the three new safety capabilities; `MockBehavior.Strict` by default; xUnit v2 → v3 + Microsoft.Testing.Platform.
- **`code-hygiene`** — `SignalEventService` sealed; unused `_rpcClient` field removed; `AtomicCounter` wrap-around behavior documented or eliminated; `ValidateRecipients` single-pass; bare log-and-rethrow patterns either removed or enriched with context; `Config.BuildClasspath` caches its directory scan.
- **`cloud-development`** — `.claude/hooks/session-start.sh` + `.claude/settings.json` + `docs/cloud-development.md`. **Already drafted in this change.**

## Capabilities

### New Capabilities

- `rpc-back-pressure`: channel-mediated fan-out + `IAsyncDisposable`-only JsonRpcClient + single-pass `Utf8JsonWriter` request composition.
- `state-machine-thread-safety`: `OnNext`-outside-lock + atomic disposed-flag for `ProcessStateManager`.
- `subscription-race-safety`: atomic reservation pattern in `SignalEventService.SubscribeAsync`.
- `hosting-modernization`: `BackgroundService` for the health-monitor loop; `IHostedLifecycleService` for explicit startup ordering; `IAsyncDisposable` on `SignalCliHostedService`; sync `ProcessRunner` (no `Task.FromResult`).
- `options-validation`: `AddOptions<Config>().ValidateDataAnnotations().ValidateOnStart()`; immutable `init`-only `Config`; `IConfiguration` binding overload.
- `agent-friendly-api`: PascalCase, `Async` suffix, CT-as-parameter, sealed surface, sealed `IRecipient` hierarchy, envelope-by-reference `EventArgs`.
- `high-performance-logging`: source-generated `LoggerMessage` for every services-layer log call.
- `aot-readiness`: trim-/AOT-clean library; no reflection fallback in serialization; no `Nito.AsyncEx`.
- `test-virtualization`: virtual-clock unit tests + xUnit v3/MTP + race-condition coverage + `Strict` mock default.
- `code-hygiene`: sealed `SignalEventService`; unused field removed; classpath cache; single-pass enumeration; non-bare exception handling; documented or eliminated `AtomicCounter` wrap.
- `cloud-development`: SessionStart hook + cloud-development.md for Claude Code on the Web.

### Modified Capabilities

<!--
Existing capabilities (rpc-robustness, process-restart-supervision, event-dispatch,
logging-privacy, cross-platform-startup, attachment-handling, process-argument-safety,
text-style-parsing, runtime-acquisition, code-quality-gates, agent-guidance,
exception-handling, net10-upgrade, json-serialization, process-state-unification)
are not weakened. Where this change touches their files, all original requirements
remain satisfied — see `tasks.md` for the regression-test mapping.
-->

## Impact

- **Code:**
  - `src/SignalCli/SignalCli.csproj` (IsAotCompatible, removed Nito.AsyncEx ref).
  - `src/SignalCli/Services/Rpc/{JsonRpcClient,JsonRpcClientHostedService}.cs` (sync `Dispose` removed; Channel-based fan-out).
  - `src/SignalCli/Services/SignalCli/{SignalCliHostedService,SignalCliHealthMonitor,ProcessStateManager}.cs` (sealed, snapshot-then-emit, `BackgroundService` for the health loop, `SemaphoreSlim`).
  - `src/SignalCli/Services/Signal/{SignalEventService,SignalService,SignalAccounts,SignalGroups,SignalDevices,SignalMessage}.cs` (race-safety, `LoggerMessage` source-gen, `Async` suffix).
  - `src/SignalCli/Models/Signal/Devices/FinishLinkResponse.cs`, `src/SignalCli/Models/Signal/Events/SubscribeReceiveResponse.cs` (PascalCase records).
  - `src/SignalCli/Models/Signal/Message/{Text,Attachment,Sticker}MessageOptions.cs` (CT removed from records).
  - `src/SignalCli/Models/Config.cs` (sealed, readonly env dict).
  - `src/SignalCli/Models/Rpc/JsonRpcRequest.cs` (drop dup properties).
  - `src/SignalCli/Serialization/SignalJson.cs` + `SignalJsonContext.cs` (source-gen-only resolver, fast-path mode).
  - `Example/SignalCli.Example/Program.cs` (async Main, no fire-and-forget).
- **Tests:**
  - xUnit 2.9 → 3.x; `xunit.runner.visualstudio` → `xunit.v3.runner.visualstudio` or move to `xunit.v3` + Microsoft.Testing.Platform.
  - All `Task.Delay(...)` calls in `Tests/SignalCli.Tests/**` migrated to `FakeTimeProvider`.
  - New race tests under `Tests/SignalCli.Tests/SignalEventService/SubscribeRaceTests.cs`, `Tests/SignalCli.Tests/SignalCliHostedService/StateManagerReentrancyTests.cs`.
  - `GetPrivateField`/`SetPrivateField` removed from `SignalCliHostedServiceTestsBase`; internal `TestSeam` properties exposed via `[InternalsVisibleTo("SignalCli.Tests")]`.
- **Docs:**
  - `docs/cloud-development.md` (new; drafted in this change).
  - `CLAUDE.md` — adds a "Cloud development" section linking to it.
- **Infra:**
  - `.claude/hooks/session-start.sh`, `.claude/settings.json` (drafted in this change).
- **Breaking changes** (public API):
  - `FinishLinkResponse.number` → `FinishLinkResponse.Number`.
  - `SubscribeReceiveResponse.id` → `SubscribeReceiveResponse.Id`.
  - Six methods get `Async` suffix.
  - `CancellationToken` becomes a method parameter, not an options field. Existing call sites that set it on the builder need migration.
- **Sequencing**: each capability ships independently. Recommended order:
  1. `cloud-development` (already drafted — merge so future contributors get a working web sandbox).
  2. `state-machine-thread-safety` + `subscription-race-safety` + `rpc-back-pressure` (correctness, internal).
  3. `high-performance-logging` (mechanical, large diff, low risk).
  4. `test-virtualization` (de-flake CI before the public API break).
  5. `aot-readiness` (depends on serialization changes).
  6. `agent-friendly-api` last — it's the only breaking surface; bumps `Version` to `3.0.0`.
