## 0. Setup

- [ ] 0.1 Audit findings cross-referenced with current source (every High verified by reading cited lines)
- [ ] 0.2 Branch `claude/post-modernize-tuning` created from current `main`

## 1. RPC back-pressure (capability `rpc-back-pressure`)

- [ ] 1.1 (A3) Add `Config.NotificationChannelCapacity` (default 1024)
- [ ] 1.2 (A3) Create `Channel<JsonRpcNotificationRaw>` in `JsonRpcClient` with `BoundedChannelOptions { SingleReader=true, SingleWriter=true, FullMode=Wait }`
- [ ] 1.3 (A3) Stdout reader loop becomes parse-then-`await WriteAsync` — no `OnNext` inline
- [ ] 1.4 (A3) Channel-consumer `Task` runs the fan-out (`_notificationSubject.OnNext`)
- [ ] 1.5 (A3) `DisposeAsync` completes the channel writer, awaits the consumer, then completes/disposes the subject
- [ ] 1.6 (A3) Test: 1000-message burst with a 50ms/message slow subscriber — reader never blocks; messages arrive in order

## 2. State-machine thread safety (capability `state-machine-thread-safety`)

- [ ] 2.1 (A2) `ProcessStateManager.UpdateState`: snapshot under lock, emit `OnNext` outside lock
- [ ] 2.2 (A2) `_disposed` becomes `Volatile.Write`/`Volatile.Read`-safe; lock-free `Dispose` short-circuit
- [ ] 2.3 (A2) Catch `ObjectDisposedException` from `OnNext` (documented disposal race window)
- [ ] 2.4 (C2) `_disposed` in `SignalCliHostedService`, `JsonRpcClient`, `JsonRpcClientHostedService`, `SignalEventService` switched to `Interlocked.Exchange`-based `int`
- [ ] 2.5 (A2) Test: synchronous Rx subscriber that re-enters `UpdateState` completes within 2s virtual time (deadlock guard)

## 3. Subscription race safety (capability `subscription-race-safety`)

- [ ] 3.1 (A4) `SignalEventService.SubscribeAsync` inserts `account → Pending(-1)` placeholder under `_subscriptionsLock` before sending RPC
- [ ] 3.2 (A4) On RPC exception, rollback the placeholder
- [ ] 3.3 (A4) On RPC success, overwrite placeholder with real `subscriptionId`
- [ ] 3.4 (A4) `UnsubscribeAsync` ignores placeholders (no signal-cli call for a never-completed reservation)
- [ ] 3.5 (A4) `ObjectDisposedException.ThrowIf(_disposed, ...)` added to `SubscribeAsync`/`UnsubscribeAsync` (Audit C6)
- [ ] 3.6 (A4) Test: `Task.WhenAll(10x SubscribeAsync(same account))` → mocked RPC invoked exactly once; 9 callers see `InvalidOperationException`; dictionary holds exactly one entry

## 4. Agent-friendly public API (capability `agent-friendly-api`)

- [ ] 4.1 (D1) `FinishLinkResponse(string Number)` PascalCase + `[property: JsonPropertyName("number")]`
- [ ] 4.2 (D1) `SubscribeReceiveResponse(int Id)` PascalCase + `[property: JsonPropertyName("id")]`
- [ ] 4.3 (D2) `ISignalAccounts.ListAccountsAsync` / `SyncAccountAsync` + impls + tests
- [ ] 4.4 (D2) `ISignalDevices.StartLinkAsync` / `FinishLinkAsync` + impls + tests
- [ ] 4.5 (D2) `ISignalGroups.ListGroupsAsync` + impl + tests
- [ ] 4.6 (D2) `ISignalCliClient.VersionAsync` + impl + tests + `SignalCliHealthMonitor` call site
- [ ] 4.7 (D3) `CancellationToken` removed from `TextMessageOptions` / `AttachmentMessageOptions` / `StickerMessageOptions`; added as last parameter to `SendTextMessageAsync` / `SendAttachmentAsync` / `SendStickerAsync`
- [ ] 4.8 (D5) `SignalCliHostedService` becomes `sealed`
- [ ] 4.9 (D6) `ConfigureAwait(false)` added to the 5 missing public-path `await`s; `.editorconfig` raises `CA2007` from `silent` → `warning`
- [ ] 4.10 (D7) `Config.EnvironmentVariables` becomes `IReadOnlyDictionary<string,string>` with a `WithEnvironment(IDictionary<string,string>)` setter helper
- [ ] 4.11 (D8) `Example/Program.cs` rewritten as `async Task Main`, `await using IHost host = …`, awaited `SendTextMessageAsync`, awaited `host.StopAsync()`
- [ ] 4.12 (D10) `JsonRpcRequest` record body emptied (positional params already generate the properties); `[property: JsonPropertyName("...")]` on each ctor param
- [ ] 4.13 (D11) Compileable `<example>` XML-doc snippets on `ISignalMessage.*Async` (use `[new UserRecipient(...)]`, not `new[] { ... }`)
- [ ] 4.14 (D12) `JetBrains.Annotations` → `PrivateAssets="all"` in `SignalCli.csproj`
- [ ] 4.15 (D9) Builders' `Build()` adds final guard (defensive, post-mutation)
- [ ] 4.16 `Version` 2.0.0 → 3.0.0 in `SignalCli.csproj` (semver-major for the breaks); CHANGELOG entry under "## [3.0.0]"

## 5. High-performance logging (capability `high-performance-logging`)

- [ ] 5.1 (P1) Add `static partial class Log` for each service (one file per service) under `Services/**/Log.<Service>.cs`
- [ ] 5.2 (P1) Migrate `JsonRpcClient` log calls (8 sites) — events 3xxx
- [ ] 5.3 (P1) Migrate `SignalCliHostedService` log calls (~20 sites) — events 4xxx
- [ ] 5.4 (P1) Migrate `SignalCliHealthMonitor` log calls — events 5xxx
- [ ] 5.5 (P1) Migrate `ProcessStateManager` log calls — events 6xxx
- [ ] 5.6 (P1) Migrate `SignalEventService` log calls — events 7xxx
- [ ] 5.7 (P1) Migrate `SignalService`/`SignalMessage`/`SignalAccounts`/`SignalDevices`/`SignalGroups` log calls — events 2xxx
- [ ] 5.8 (P2) Wrap `string.Join(", ", response)` (`SignalAccounts.cs:43`) and analogous expensive args in `if (_logger.IsEnabled(LogLevel.Trace))` guards (or migrate to `LoggerMessage` which generates the guard)
- [ ] 5.9 `.editorconfig`: enable `CA1848`, `CA1873` analyzers at `warning` severity
- [ ] 5.10 Regression test: `PrivacyLoggingTests` still passes verbatim — no PII appears above `Trace`

## 6. AOT readiness (capability `aot-readiness`)

- [ ] 6.1 (B2) `JsonRpcClient._sendLock`: `Nito.AsyncEx.AsyncLock` → `SemaphoreSlim(1, 1)`
- [ ] 6.2 (B2) `SignalCliHostedService._operationLock`: `AsyncLock` → `SemaphoreSlim(1, 1)`
- [ ] 6.3 (B2) Drop `Nito.AsyncEx` PackageReference from `SignalCli.csproj`
- [ ] 6.4 (P6) Drop `DefaultJsonTypeInfoResolver` from `SignalJson.Options.TypeInfoResolver` — leave only `SignalJsonContext.Default`
- [ ] 6.5 (P6) Audit all `JsonSerializer.Serialize`/`Deserialize`/`SerializeToElement` call sites for `TRequest`/`TResponse` types not in `SignalJsonContext`; add any missing
- [ ] 6.6 (B6) `[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]` (or `Serialization` per-type) for fast-path emission
- [ ] 6.7 (P6) `<IsAotCompatible>true</IsAotCompatible>` in `SignalCli.csproj`
- [ ] 6.8 (P6) Resolve any IL2026/IL2104 warnings surfaced by the AOT analyzer (annotate with `RequiresUnreferencedCode` only as last resort)
- [ ] 6.9 (P6) `dotnet publish -c Release /p:PublishAot=true` from a probe app succeeds (smoke test only, not added to CI yet)

## 7. Test virtualization (capability `test-virtualization`)

- [ ] 7.1 (T2) Replace 12 `Task.Delay` waits in unit tests with `FakeTimeProvider.Advance` — list:
  - `JsonRpcClientTests.cs:232, 288`
  - `SignalCliHostedServiceRestartTests.cs:92, 125, 221`
  - `SignalCliHostedServiceAuditTests.cs:41, 65, 75`
  - `SignalCliHostedServiceStateTests.cs:204`
  - `SignalCliHealthMonitorEdgeCaseTests.cs:65`
  - `SignalCliHealthMonitorLoopTests.cs:71`
- [ ] 7.2 (T3) Add `internal` TestSeam properties (gated by `[InternalsVisibleTo("SignalCli.Tests")]`) — replace `GetPrivateField<IProcess>(svc, "_currentProcess")` with `svc.TestSeam.CurrentProcess`
- [ ] 7.3 (T3) Delete `GetPrivateField`/`SetPrivateField` from `SignalCliHostedServiceTestsBase`
- [ ] 7.4 (T4) Floating-point asserts use `Assert.Equal(expected, actual, precision: 3)`
- [ ] 7.5 (T5) New `Tests/SignalCli.Tests/SignalCliHostedService/StateManagerReentrancyTests.cs` (matches §2.5)
- [ ] 7.6 (T5) New `Tests/SignalCli.Tests/SignalEventService/SubscribeRaceTests.cs` (matches §3.6)
- [ ] 7.7 (T5) New `Tests/SignalCli.Tests/Rpc/BackPressureTests.cs` (matches §1.6)
- [ ] 7.8 (T1) Migrate to `xunit.v3` + `Microsoft.Testing.Platform.MSBuild`; update `xunit 2.9.2`/`xunit.runner.visualstudio 2.8.2` PackageReferences
- [ ] 7.9 (T8) `IAsyncLifetime` on hosted-service test bases instead of `IDisposable`
- [ ] 7.10 (T10) Consolidate facade passthrough tests with `[Theory]` + `[InlineData]` where the shape is uniform

## 8. Cloud development (capability `cloud-development`) — already in this change

- [x] 8.1 `.claude/hooks/session-start.sh` — installs `dotnet-sdk-10.0`, restores tests, sanity-builds the library; skips runtime packages
- [x] 8.2 `.claude/settings.json` — registers the SessionStart hook
- [x] 8.3 `docs/cloud-development.md` — workflow, command crib, network policy, async/sync trade-off
- [x] 8.4 `CLAUDE.md` — link to `docs/cloud-development.md` from a new "Cloud development" section
- [x] 8.5 Hook validated end-to-end in remote env (SDK install, restore, build, sample test pass)

## 9. Synthesis & validation

- [x] 9.1 `openspec validate post-modernize-tuning --strict` passes
- [ ] 9.2 `dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj` — 152 (+3 new race tests) pass
- [ ] 9.3 `dotnet build SignalCli.sln /p:TreatWarningsAsErrors=true` clean
- [ ] 9.4 `dotnet build src/SignalCli/SignalCli.csproj /p:PublishAot=true` from a probe app emits zero `IL*` warnings
- [ ] 9.5 CHANGELOG entry under "## [3.0.0]" lists every breaking change from §4 with the migration mapping
