# Design — audit-followup-2026

## Method

This change has **six** capabilities, each independently shippable. They are bundled because the regression-guard tests (§4 below) are what *prevent* the doc-sync (§1) from drifting again, and the new JSON-hardening flag (§2) wants a round-trip test landing in the same commit.

No new dependencies are introduced. No public API breaks. No behavioral change is visible to a consumer whose responses are well-formed signal-cli output.

## Subsystem-by-subsystem design

### 1. `obsolete-doc-sync`

**Problem.** Six call sites declare *"will be removed in 3.0"* in source that has already shipped as 3.0.0. `CLAUDE.md` itself says under "Backward compatibility convention" that `AddSignalCli(Action<Config>?)`, `Config`, and `ISignalCliClient.Version()` are *"in flight, will be removed in 4.0"* — yet the code attribute strings still say 3.0. Additionally the `CLAUDE.md` "Implemented, merged, archived" section lists `Version()` and the `Action<Config>?`-shim as *"Already removed in 3.0"*, which contradicts the live source.

**Affected sites (confirmed by grep):**

| File | Current text | Action |
|---|---|---|
| `src/SignalCli/Models/Config.cs:17` | `[Obsolete("...; will be removed in 3.0.", error: false)]` | rewrite version to `4.0` |
| `src/SignalCli/Interfaces/SignalCli/ISignalCliClient.cs:54` | `[Obsolete("Use VersionAsync; will be removed in 3.0")]` | rewrite version to `4.0` |
| `src/SignalCli/Extensions/ServiceCollectionExtensions.cs:122` | `[Obsolete("Use AddSignalCli(Action<SignalCliOptions>?) ...; Will be removed in 3.0.")]` | rewrite version to `4.0` |
| `src/SignalCli/Models/SignalCliOptions.cs:24` (XML doc) | `Config; він буде видалений у 3.0` | rewrite to `4.0` |
| `src/SignalCli/Models/SignalCliOptions.cs:114-115` (comment) | `audit N7: internal compat-shim — зникне у 3.0` | rewrite to `4.0` |
| `src/SignalCli/Models/SignalCliOptionsExtensions.cs:13,15` (comments) | `зникне у 3.0` / `removed in 3.0` | rewrite to `4.0` |

**`CLAUDE.md` reconciliation.** The "Backward compatibility convention" paragraph already lists `AddSignalCli(Action<Config>?)`, `Config` (compat-shim), and the `InvokeMethodAsync` generic-param reversal as 4.0-targeted — that block stays. The "Already removed in 3.0" mention of `Version()` and `AddSignalCli(Action<Config>?)`-shim arg/property removal-shims is **wrong** and gets corrected (the `Version()` DIM is still in `ISignalCliClient.cs`; the `Action<Config>?` overload is still in `ServiceCollectionExtensions.cs`).

**Why one commit.** This is a doc-only sweep across 6 source sites + one `CLAUDE.md` paragraph. Mass-edit safety per `CLAUDE.md`: use the `[System.IO.File]` UTF-8 BOM-aware pattern for the `.cs` files (most are mojibake-prone Cyrillic).

### 2. `json-hardening`

**Problem.** `SignalJson.Options` does not opt into `AllowDuplicateProperties = false`, a new flag in .NET 10. signal-cli's Jackson serializer has no built-in dedup-on-write contract: a malformed signal-cli response with duplicate keys silently keeps "last wins" behavior, which could mask protocol drift.

**Approach.** Single-line addition to `src/SignalCli/Serialization/SignalJson.cs`:

```csharp
public static readonly JsonSerializerOptions Options = new()
{
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    AllowDuplicateProperties = false, // .NET 10: reject dup keys (defensive — signal-cli should not produce them)
    TypeInfoResolver = SignalJsonContext.Default,
};
```

Same for `OptionsForTests` (so test-only anonymous-type payloads also benefit).

**Risk.** If signal-cli does in fact send duplicate keys, this fails the deserialization. We accept that risk explicitly — duplicate keys are a protocol violation; failing loudly is correct.

**Test.** `JsonSerializationTests.DuplicateProperty_FailsDeserialization` — feeds `{"jsonrpc":"2.0","jsonrpc":"X","id":"1"}` and asserts `JsonException`.

### 3. `configuration-binder-aot`

**Problem.** `AddSignalCli(IConfiguration)` ([ServiceCollectionExtensions.cs:88-101](../../../src/SignalCli/Extensions/ServiceCollectionExtensions.cs)) carries `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` because `OptionsBuilder<T>.Bind(IConfiguration)` uses reflection. Since .NET 8, `Microsoft.Extensions.Configuration.Binder` ships a Roslyn source-generator that emits AOT-safe binding when `<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>` is set.

**Approach.**

1. Add `<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>` to `src/SignalCli/SignalCli.csproj`.
2. Remove `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` from `AddSignalCli(IConfiguration)` and `ConfigureOptionsFromConfiguration`.
3. Build with `dotnet build -p:TreatWarningsAsErrors=true` and verify no IL2026/IL3050 from the binding site.
4. The XMLDoc note "AOT-warning: для AOT-deploy'у користуйтеся `AddSignalCli(Action<SignalCliOptions>?)`" is removed — the source-gen makes both overloads AOT-safe.

**Risk.** The source-generator currently doesn't support `IReadOnlyDictionary<string,string>` binding for `SignalCliOptions.EnvironmentVariables` from an `IConfiguration` section directly. Verify at build: if generator can't bind a property, it logs at build-time. If `EnvironmentVariables` can't be bound, document that it must be set via `Action<SignalCliOptions>` (not via `appsettings.json`) and keep the test for `IConfiguration` binding limited to scalar properties (`AppHome`, `JavaExecutable`, etc.) — same as today's `OptionsValidationTests.AddSignalCli_FromConfiguration_BindsAppsettingsValues`.

### 4. `regression-guards`

Three new defensive tests under `Tests/SignalCli.Tests/RegressionGuards/`. All read-only, all reflection-based — same flavor as the existing `JsonContextRegistrationTests`.

#### 4.a `ObsoleteMessageConsistencyTests.cs`

```
[Fact] AllObsoleteAttributes_HaveValidRemovalVersion()
```

- Enumerate every `MemberInfo` on `typeof(ISignalCliClient).Assembly` reachable via `GetTypes()/.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)`.
- For each `[ObsoleteAttribute]`, regex `"will be removed in (\d+)\.0"` on `Message`.
- Read current assembly major from `Assembly.GetName().Version!.Major`.
- Fail if `removalMajor <= currentMajor` with a list of offending members.

This catches the M-1 class of drift the moment it reappears — including the Ukrainian-language XML-doc variant via `XmlDocReader.SelectNodes("//remarks[contains(., 'видалений у')]")`. (Actually XML docs aren't in the assembly. So the test is `[Obsolete]`-attribute-only; we accept that as the highest-value pin and skip XML docs — the regex would otherwise need to read `SignalCli.xml` and parse it. Document this scope limit in the test's XML doc.)

#### 4.b `EventIdBlockTests.cs`

```
[Theory]
[InlineData(typeof(SignalCliHostedServiceLog), 100, 199)]
[InlineData(typeof(SignalCliHealthMonitorLog), 200, 299)]
[InlineData(typeof(JsonRpcClientLog), 300, 399)]
[InlineData(typeof(JsonRpcClientHostedServiceLog), 400, 499)]
[InlineData(typeof(SignalEventServiceLog), 500, 599)]
[InlineData(typeof(SignalServiceLog), 600, 699)]
[InlineData(typeof(SignalMessageLog), 700, 799)]
[InlineData(typeof(SignalAccountsLog), 800, 899)]
[InlineData(typeof(SignalDevicesLog), 800, 899)]
[InlineData(typeof(SignalGroupsLog), 800, 899)]
[InlineData(typeof(ProcessRunnerLog), 900, 999)]
[InlineData(typeof(ProcessStateManagerLog), 900, 999)]
public void EventIds_InReservedBlock(Type logClass, int lo, int hi)
```

- Reflect every `partial` method on `logClass`.
- Walk `MethodInfo.GetCustomAttribute<LoggerMessageAttribute>()`.
- Assert `lo <= EventId <= hi`.

Catches `CLAUDE.md` rule for EventId blocks the moment a new `[LoggerMessage(EventId = 250)]` lands inside `JsonRpcClientLog` (correct block is 300-399).

#### 4.c `PublicApiSurfaceTests.cs`

```
[Fact] PublicSurface_MatchesBaseline()
```

- For every `public` type, member, parameter, constraint in `SignalCli.dll`, emit a stable canonical-form line (e.g. `M:SignalCli.Services.Signal.SignalMessage.SendTextMessageAsync(SignalCli.Models.Signal.Message.TextMessageOptions, System.Threading.CancellationToken)`).
- Sort lines lexicographically.
- Compare the snapshot to `Tests/SignalCli.Tests/RegressionGuards/SignalCli.public-api.txt`.
- On mismatch, fail with a unified diff so the agent sees the exact added/removed line.

**Acceptance / regen.** When intentional public-surface changes are made, the developer regenerates the baseline (`dotnet test --filter PublicSurface --logger "console;verbosity=detailed" -- … RegenerateBaseline=true` controlled via an env var, or simply by editing the .txt file by hand from the failing test output). The baseline ships in the test project; PR review treats edits to it as design decisions.

This is the canonical .NET-library pattern from `dotnet/runtime` (the [`Microsoft.DotNet.ApiCompat` toolchain](https://learn.microsoft.com/dotnet/fundamentals/apicompat/overview) achieves the same; we implement a minimal reflection-based version because pulling the whole ApiCompat toolchain is overkill for a 200-file library).

### 5. `integration-tests-expansion`

`Tests/SignalCli.Tests.Integration/` grows from 1 to 6 tests. All share the existing `TryBuildHost` skip-gate (returns the same `skipReason` when the bundled JRE / native binary is not present on this platform).

#### 5.a `SignalCliE2EProcessLifecycleTests.Process_StartStopRestart_TransitionsObservedCorrectly`

```csharp
var states = new List<ProcessState>();
state.StateChanged.Subscribe(info => states.Add(info.State));
await host.StartAsync();    // expect Starting, Running
await hostedService.StopAsync(CancellationToken.None);  // Stopping, Stopped
await host.StartAsync();    // Starting, Running again? Or new host needed?
```

(In practice: a new host instance per `Start/Stop`, but `ProcessStateManager` lives as long as the DI container — so we observe the full cycle on the same `StateChanged` stream.)

#### 5.b `SignalCliE2ERestartTests.Process_KilledExternally_AutoRestartReclaimsProcess`

- Configure `MaxRestartAttempts = 2`, `RestartDelaySeconds = 1`, `RestartWindowSeconds = 60`.
- Start, wait for `Running`.
- Capture `currentProcess.Id`; call `Process.GetProcessById(id).Kill()`.
- Wait up to 10s for `ProcessRestarts` counter (via MeterListener) to tick with `trigger=crash`.
- Wait for `state == Running` again with a *different* `currentProcess.Id`.

#### 5.c `SignalCliE2EHealthMonitorTests.HealthMonitor_OverInterval_LastPingResultUpdates`

- Configure `HealthCheckIntervalSeconds = 2`, start, sleep wall-clock 5s.
- Reflect `SignalCliHealthMonitor.LastPingResult` (already exposed via `internal` + `InternalsVisibleTo`).
- Assert `LastPingResult.Ok == true` and `(DateTimeOffset.UtcNow - LastPingResult.At).TotalSeconds < 5`.

This is the only E2E test that intentionally uses wall-clock time — it is testing the *real* cadence with a *real* `signal-cli`. Document this exception in the test's XML doc; do not extend the `<10ms` cap rule to integration tests (that rule applies to unit tests under `SignalCliHealthMonitor/` and `SignalCliHostedService/Restart*/`).

#### 5.d `SignalCliE2EDisposeTests.DisposeAsync_MidFlight_LeavesNoOrphanProcess`

- Start, capture `currentProcess.Id`.
- Start a `VersionAsync` call but do not await it.
- `await host.DisposeAsync()` (within 3s).
- Assert `Process.GetProcessById(id)` throws `ArgumentException` (process no longer exists) within 5 wall-clock seconds, OR if found, `HasExited == true`.

#### 5.e `SignalCliE2EConfigurationTests.Configuration_FromIConfiguration_StartsRealCli`

- Build an in-memory `IConfiguration` section with `SignalCli:AppHome`, `SignalCli:LibDirectory` (Win/macOS) or `SignalCli:SignalCliExecutable` (Linux), `SignalCli:JavaExecutable`.
- `services.AddSignalCli(configuration.GetSection("SignalCli"))`.
- Build host, start, call `version`, stop.
- Same as `Version_RealSignalCli_ReturnsNonEmpty` but through the `IConfiguration` overload.

This validates the `configuration-binder-aot` capability end-to-end with a real process — the unit test for the same overload (`OptionsValidationTests.AddSignalCli_FromConfiguration_BindsAppsettingsValues`) only validates the binding, not that the bound options actually start a process.

### 6. `edge-case-coverage`

Twelve unit tests, organized by area. Each is small (≤40 lines including arrange/act/assert). Full list in `tasks.md` §6. The high-leverage ones:

- **JSON-RPC error contract** — `JsonRpcErrorTests`:
  - `Error_Minus32601_DeserializesAsMethodNotFound`
  - `Error_Minus32700_DeserializesAsParseError`
  - `Error_WithDataField_PreservesPayload` (today `JsonRpcError.Data` exists; no test asserts it deserializes)
- **Attachment boundary** — `AttachmentEntryTests`:
  - `EncodedSize_ExactlyAtBoundary_UsesTempFile` (`raw.Length * 4 / 3 == MaxInlineEncodedAttachmentBytes` → temp-file path)
  - `Filename_WithNulByte_IsSanitized`
  - `Filename_WithUnicodeRtl_IsSanitized`
  - `SaveToTempFile_CalledTwice_IsIdempotent` or documented as not-supported
- **AtomicCounter wrap** — `UtilityEdgeCaseTests`:
  - `AtomicCounter_AtInt32MaxValue_WrapsToInt32MinValue_WithoutThrowing`
- **Observability counters firing** — `ObservabilityCounterTests`:
  - `EventsDropped_OnChannelOverflow_IncrementsByExactlyOne`
  - `RpcDuration_HappyPath_RecordsPositiveValue`
  - `ProcessRestarts_OnForceRestart_IncrementsWithTriggerForce`
- **Subscription cancellation propagation** — `SignalEventServiceDispatchTests`:
  - `SubscribeAsync_LeaderCancelled_FollowersReceiveSameCancellation`
- **State-machine no-op paths**:
  - `ForceRestartAsync_WhenStopping_IsNoOp`
- **Channel cap minimum** — `BackPressureTests`:
  - `Capacity1_DeliversAllMessagesInFifoOrder`
- **DI registration idempotency** — `OptionsValidationTests`:
  - `AddSignalCli_CalledTwice_SecondCallIsNoOp`
- **EnvironmentVariables snapshot semantics** — `OptionsValidationTests`:
  - `EnvironmentVariables_PostStartMutationOfExternalMap_DoesNotLeakToProcess`
- **Response shape defensiveness** — `JsonRpcClientTests`:
  - `Response_WithBothResultAndError_PrefersError`

## Verification

After implementation, the following commands must all succeed:

```bash
dotnet build SignalCli.sln -p:TreatWarningsAsErrors=true       # no IL2026/IL3050 anywhere
dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj       # 215+ tests, all green
dotnet test Tests/SignalCli.Tests.Integration/SignalCli.Tests.Integration.csproj  # 6 E2E tests, runtime-gated skip on platforms without bundled runtime
```

On a platform with a bundled runtime (Win/macOS/Linux), all 6 Integration tests should *run*, not skip. The CI workflow `runtime-smoke.yml` already runs Integration tests on Linux native and verifies a real signal-cli starts.

## Why not split into multiple changes?

Each of `obsolete-doc-sync`, `json-hardening`, `configuration-binder-aot` could individually be its own micro-change. But:

- They land in the **same files** (`SignalCli.csproj`, `SignalJson.cs`, `ServiceCollectionExtensions.cs`) within tight diffs — three commits across the same files generate three rebase fronts.
- The regression-guard test in §4 *exists* to catch the §1 class of drift. Landing them together is what closes the loop.
- The integration-test expansion is a natural bundle with the configuration-binder-aot change because it exercises the new path end-to-end (5.e).
- The edge-case coverage isn't a new feature — it's protection for invariants that already exist.

Per `CLAUDE.md`'s "Working style" — "One commit per capability/cluster" — this change will land as **6 commits on the branch**, one per capability, plus a final docs commit. That gives reviewers per-capability bisectability without fragmenting the proposal.
