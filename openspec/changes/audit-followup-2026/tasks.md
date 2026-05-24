# Tasks — audit-followup-2026

## 0. Setup

- [ ] 0.1 Create branch `claude/audit-followup-2026` from current `main`
- [ ] 0.2 Run `npx -y @fission-ai/openspec@latest validate audit-followup-2026 --strict` and confirm green before any implementation work begins

## 1. Doc-sync (capability `obsolete-doc-sync`)

- [ ] 1.1 Rewrite `[Obsolete(...)]` strings to say "will be removed in 4.0" in:
  - [ ] `src/SignalCli/Models/Config.cs:17`
  - [ ] `src/SignalCli/Interfaces/SignalCli/ISignalCliClient.cs:54`
  - [ ] `src/SignalCli/Extensions/ServiceCollectionExtensions.cs:122`
- [ ] 1.2 Rewrite Ukrainian XML-docs / inline comments referring to "видалений у 3.0" / "зникне у 3.0" / "removed in 3.0" to "4.0" in:
  - [ ] `src/SignalCli/Models/SignalCliOptions.cs:24` (XML doc)
  - [ ] `src/SignalCli/Models/SignalCliOptions.cs:114-115` (inline comment + `#pragma` justification)
  - [ ] `src/SignalCli/Models/SignalCliOptionsExtensions.cs:13,15`
- [ ] 1.3 Update `CLAUDE.md` "Implemented, merged, archived" section: remove the false claim "Already removed in 3.0: `Version()`, `AddSignalCli(Action<Config>?)`-shim arg/property removal-shims". Move these to "Currently in flight (will be removed in 4.0)" — that paragraph already exists; merge by listing.
- [ ] 1.4 Use the `[System.IO.File]` UTF-8-BOM-aware mass-edit pattern (per CLAUDE.md "Mass-edit safety") when rewriting Cyrillic comments — do NOT use `Get-Content -Raw` + `Set-Content -Encoding UTF8`.
- [ ] 1.5 `dotnet build SignalCli.sln -p:TreatWarningsAsErrors=true` clean.

## 2. JSON hardening (capability `json-hardening`)

- [ ] 2.1 Add `AllowDuplicateProperties = false` to `SignalJson.Options` in `src/SignalCli/Serialization/SignalJson.cs`.
- [ ] 2.2 Add the same flag to `SignalJson.OptionsForTests` (test-only options).
- [ ] 2.3 Add test `JsonSerializationTests.DuplicateProperty_FailsDeserialization`:
  - Input: `{"jsonrpc":"2.0","jsonrpc":"X","id":"1","result":{}}`
  - Expect: `JsonException` with "duplicate" or equivalent in message.
- [ ] 2.4 Document the flag in the XML doc of `SignalJson.Options`: ".NET 10 hardening — reject duplicate keys (defensive against malformed signal-cli output)".
- [ ] 2.5 Run full `dotnet test` to confirm no existing test feeds duplicate keys (would surface as a new failure).

## 3. Configuration binder AOT (capability `configuration-binder-aot`)

- [ ] 3.1 Add `<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>` to `src/SignalCli/SignalCli.csproj` `<PropertyGroup>`.
- [ ] 3.2 Verify the binding source-generator emits code for `SignalCliOptions` by inspecting `obj/Debug/net10.0/generated/Microsoft.Extensions.Configuration.Binder.SourceGeneration/`.
- [ ] 3.3 If the generator cannot bind `IReadOnlyDictionary<string,string> EnvironmentVariables`, document the limitation in the XML doc of `AddSignalCli(IConfiguration)` (read-only-dictionary binding requires `Action<SignalCliOptions>` instead). Continue with the rest.
- [ ] 3.4 Remove `[RequiresUnreferencedCode("...")]` and `[RequiresDynamicCode("...")]` from:
  - [ ] `ServiceCollectionExtensions.AddSignalCli(IConfiguration)` (the extension-block member, lines 88-89)
  - [ ] `ServiceCollectionExtensions.ConfigureOptionsFromConfiguration` (lines 185-186)
- [ ] 3.5 Remove the "AOT-warning" paragraph from `AddSignalCli(IConfiguration)`'s XML doc — the overload is now AOT-safe.
- [ ] 3.6 `dotnet build src/SignalCli/SignalCli.csproj -p:TreatWarningsAsErrors=true` clean — no IL2026/IL3050 from either overload.
- [ ] 3.7 Confirm `OptionsValidationTests.AddSignalCli_FromConfiguration_BindsAppsettingsValues` still passes (binding still works for scalar properties).

## 4. Regression guards (capability `regression-guards`)

### 4.a `ObsoleteMessageConsistencyTests`

- [ ] 4a.1 Create `Tests/SignalCli.Tests/RegressionGuards/ObsoleteMessageConsistencyTests.cs`.
- [ ] 4a.2 Implement `AllObsoleteAttributes_HaveValidRemovalVersion`:
  - Reflect every member of `typeof(SignalCli.Models.SignalCliOptions).Assembly`.
  - For each `[ObsoleteAttribute]`, regex `"will be removed in (\d+)\.0"` on `.Message`.
  - Assert removal-major > `Assembly.GetName().Version!.Major`.
- [ ] 4a.3 If the test fails, error message must list each offending `MemberInfo.FullName` + the matched version, so an agent can fix without running grep.

### 4.b `EventIdBlockTests`

- [ ] 4b.1 Create `Tests/SignalCli.Tests/RegressionGuards/EventIdBlockTests.cs`.
- [ ] 4b.2 Implement `[Theory]` over the 12 `*Log.cs` classes per the table in `design.md` §4.b.
- [ ] 4b.3 Reflect each class's `partial` methods, walk `LoggerMessageAttribute.EventId`, assert in range.

### 4.c `PublicApiSurfaceTests`

- [ ] 4c.1 Create `Tests/SignalCli.Tests/RegressionGuards/PublicApiSurfaceTests.cs`.
- [ ] 4c.2 Implement reflection walker producing canonical-form lines (`T:`, `M:`, `P:`, `F:`, `E:` prefixes — mirrors xmldoc cref format).
- [ ] 4c.3 Sort lines, hash, compare to embedded baseline file `SignalCli.public-api.txt` under `Tests/SignalCli.Tests/RegressionGuards/`.
- [ ] 4c.4 Generate the baseline file once with the actual current public surface; commit it.
- [ ] 4c.5 On mismatch, the test must produce a unified diff (use `System.Text.RegularExpressions` + simple line-by-line compare). Output format: `+ <new>` / `- <removed>` so a reviewer reads it as a PR diff.
- [ ] 4c.6 Document in the test's XML doc: "to regenerate baseline, replace its contents with the actual surface dump produced by the failing test output".

## 5. Integration test expansion (capability `integration-tests-expansion`)

All five new tests live under `Tests/SignalCli.Tests.Integration/`. Each shares the existing `TryBuildHost`-style skip gate. **None** require a registered Signal account or network access.

- [ ] 5.1 Extract `TryBuildHost` into a shared base class `IntegrationTestBase` (currently inlined in `SignalCliE2EVersionTests`) — methods: `TryBuildHost(out string skipReason)`, `WaitForRunning(host)`, `IsRuntimeAvailable()`.
- [ ] 5.2 `Process_StartStopRestart_TransitionsObservedCorrectly` (file: `SignalCliE2EProcessLifecycleTests.cs`):
  - Subscribe to `ProcessStateManager.StateChanged`; capture state sequence.
  - Start → wait Running → stop → wait Stopped.
  - Assert observed sequence equals `[NotStarted-or-Starting, Running, Stopping, Stopped]` (allow `NotStarted` initial absorbed at subscribe-time).
- [ ] 5.3 `Process_KilledExternally_AutoRestartReclaimsProcess` (file: `SignalCliE2ERestartTests.cs`):
  - `MaxRestartAttempts=2`, `RestartDelaySeconds=1`, `RestartWindowSeconds=60`.
  - Start, capture `IProcess.Id`.
  - `Process.GetProcessById(id).Kill(entireProcessTree: true)`.
  - Wait via `Meter`-listener for `signalcli.process.restarts` with `trigger=crash` to tick.
  - Wait for state `Running` with **different** `IProcess.Id`.
  - Cap total wall-clock to 30s; if exceeded, fail.
- [ ] 5.4 `HealthMonitor_OverInterval_LastPingResultUpdates` (file: `SignalCliE2EHealthMonitorTests.cs`):
  - `HealthCheckIntervalSeconds=2`.
  - Start, sleep 5s (wall-clock, documented exception to the no-wall-clock rule).
  - Reflect `monitor.LastPingResult` (internal, `InternalsVisibleTo("SignalCli.Tests.Integration")` already exists in `src/SignalCli/SignalCli.csproj`).
  - Assert `Ok==true` and `(DateTimeOffset.UtcNow - At).TotalSeconds < 5`.
- [ ] 5.5 `DisposeAsync_MidFlight_LeavesNoOrphanProcess` (file: `SignalCliE2EDisposeTests.cs`):
  - Start, capture child PID via `SignalCliHostedService.CurrentProcessForTests.Id`.
  - Fire `VersionAsync` without await.
  - `await host.DisposeAsync()`.
  - Within 5s, assert `Process.GetProcessById(childPid).HasExited == true` (or the call throws `ArgumentException`).
- [ ] 5.6 `Configuration_FromIConfiguration_StartsRealCli` (file: `SignalCliE2EConfigurationTests.cs`):
  - Build in-memory `IConfiguration` with platform-specific section.
  - `services.AddSignalCli(configuration.GetSection("SignalCli"))`.
  - Start, call `version`, assert Version contains "0.14".
  - This is the only test where we DO use the AOT-safe `IConfiguration` overload on a real process — locks in §3 end-to-end.

### 5.7 Test infrastructure

- [ ] 5.7.1 Add `[Trait("Category", "E2E")]` to each new test class (matches existing convention).
- [ ] 5.7.2 Verify `runtime-smoke.yml` CI workflow picks up the new tests automatically (it `dotnet test`s the whole project; if so, no workflow edit needed).
- [ ] 5.7.3 If any test consistently takes more than 30 wall-clock-seconds on a green-path Linux native CI run, downgrade it to `[Trait("Category", "E2E-Slow")]` and gate the runtime-smoke matrix on `--filter "Category!=E2E-Slow"` for the default job.

## 6. Edge-case coverage (capability `edge-case-coverage`)

Twelve unit tests. Group by SUT for ergonomics.

### 6.a JSON-RPC error contract

- [ ] 6a.1 `JsonRpcErrorTests.Error_Minus32601_DeserializesAsMethodNotFound` (new file `Tests/SignalCli.Tests/JsonRpcErrorTests.cs`).
- [ ] 6a.2 `JsonRpcErrorTests.Error_Minus32700_DeserializesAsParseError`.
- [ ] 6a.3 `JsonRpcErrorTests.Error_WithDataField_PreservesPayload` — feeds `error.data = {"foo":42}`, asserts `JsonRpcError.Data` is non-null `JsonElement` and contains `foo` after deserialization.

### 6.b Attachment boundaries

- [ ] 6b.1 `AttachmentEntryTests.EncodedSize_ExactlyAtBoundary_UsesTempFile`:
  - Compute `raw = MaxInlineEncodedAttachmentBytes * 3 / 4 + 1` bytes (one byte over the inline threshold).
  - Assert the captured `Attachments` element is a file path (not a `data:` URI).
- [ ] 6b.2 `AttachmentEntryTests.Filename_WithNulByte_IsSanitized`:
  - Filename `"x\0evil.bin"` (NUL byte mid-string).
  - `ToDataUri()` filename param must contain no NUL byte.
  - `SaveToTempFile()` writes to a path that exists (NTFS/POSIX both reject NUL in filenames).
- [ ] 6b.3 `AttachmentEntryTests.Filename_WithUnicodeRtl_IsSanitized`:
  - Filename `"safe‮evil.bin"` (right-to-left override exploit).
  - Output filename must not contain U+202E.
- [ ] 6b.4 `AttachmentEntryTests.SaveToTempFile_CalledTwice_BehaviorDocumented`:
  - Either pass (idempotent — re-uses existing temp path) OR throw a documented exception. Pin whichever the implementation chose.

### 6.c AtomicCounter

- [ ] 6c.1 `UtilityEdgeCaseTests.AtomicCounter_AtInt32MaxValue_WrapsToInt32MinValue_WithoutThrowing`:
  - Seed `int.MaxValue - 1`.
  - First `Increment()` returns `int.MaxValue`.
  - Second `Increment()` returns `int.MinValue` (unchecked wrap), no exception.

### 6.d Observability counters

All under new file `Tests/SignalCli.Tests/ObservabilityCounterTests.cs`. Same `MeterListener` pattern as `ObservabilityPrivacyTests`.

- [ ] 6d.1 `EventsDropped_OnChannelOverflow_IncrementsByExactlyOne`:
  - Build `SignalEventService`, push N=ChannelCapacity+5 text-message envelopes without consuming.
  - Assert `signalcli.events.dropped` counter sum across `event_type=text` is exactly 5.
- [ ] 6d.2 `RpcDuration_HappyPath_RecordsPositiveValue`:
  - Mock the streams; push a response synchronously.
  - Assert at least one `signalcli.rpc.duration` measurement with `value > 0` and `method="someMethod"`.
- [ ] 6d.3 `ProcessRestarts_OnForceRestart_IncrementsWithTriggerForce`:
  - Drive `SignalCliHostedService.ForceRestartAsync` via the existing test base.
  - Assert exactly one `signalcli.process.restarts` measurement with `trigger=force`.

### 6.e Subscription cancellation propagation

- [ ] 6e.1 `SignalEventServiceDispatchTests.SubscribeAsync_LeaderCancelled_FollowersReceiveSameCancellation`:
  - Set up mock `ISignalCliClient.InvokeMethodAsync` that blocks until `CancellationToken` fires.
  - Leader calls `SubscribeAsync(account, leaderCts.Token)`.
  - Two followers call `SubscribeAsync(account)` (no token of their own).
  - Cancel `leaderCts`.
  - Assert leader's task → `OperationCanceledException`.
  - Assert followers' tasks → `OperationCanceledException` (propagated from the same TCS).
  - **OR** if the design pivot is to elect a new leader when the original cancels: assert followers do NOT receive the cancellation but instead make their own RPC. Pin whichever the implementation chose; document in the spec.

### 6.f State machine no-op paths

- [ ] 6f.1 `SignalCliHostedServiceStateTests.ForceRestartAsync_WhenStopping_IsNoOp`:
  - Drive state to `Stopping` (start, then begin `StopAsync` but suspend the process-exit).
  - Call `ForceRestartAsync`.
  - Assert log entry `ForceRestartSkipped` recorded with state=`Stopping`.
  - Assert NO `ForceRestartAttempt` log.

### 6.g Channel-capacity minimum

- [ ] 6g.1 `BackPressureTests.Capacity1_DeliversAllMessagesInFifoOrder`:
  - Same shape as existing `NotificationBurst_WithSlowSubscriber_AllMessagesDeliveredInOrder` but with `NotificationChannelCapacity = 1` and burst = 20.
  - Assert all 20 delivered, FIFO.

### 6.h DI idempotency + options-snapshot

- [ ] 6h.1 `OptionsValidationTests.AddSignalCli_CalledTwice_SecondCallIsNoOp`:
  - Build services, `AddSignalCli(opts)`, capture descriptor count.
  - `AddSignalCli(differentOpts)`.
  - Assert descriptor count unchanged; resolved `SignalCliOptions.AppHome` equals the FIRST call's value.
- [ ] 6h.2 `OptionsValidationTests.EnvironmentVariables_PostStartMutationOfExternalMap_DoesNotLeakToProcess`:
  - Configure `SignalCliOptions` with `EnvironmentVariables = new Dictionary<string,string> { ["k"]="v" }` (mutable reference).
  - Build SP, resolve `IOptions<SignalCliOptions>.Value`, capture `EnvironmentVariables`.
  - Assert `EnvironmentVariables` is `IReadOnlyDictionary` (compile-time + assert-runtime).
  - Mutate the external `Dictionary<,>` reference (cast back via `(IDictionary<string,string>)opts.EnvironmentVariables`; if that throws because `IReadOnlyDictionary` returned a `ReadOnlyDictionary` wrapper, the test passes — defense works at the type level).

### 6.i Response shape defensiveness

- [ ] 6i.1 `JsonRpcClientTests.Response_WithBothResultAndError_PrefersError`:
  - Inject response `{"id":"1","result":{...},"error":{"code":-1,"message":"E"}}`.
  - Assert `JsonRpcException` thrown (error wins).

## 6c. Badge URL fix (capability `badge-url-fix`)

All three sub-fixes SHALL land in a SINGLE commit — they are mutually dependent (README + workflow + csproj). If only one lands, either CI reverts the fix (workflow not updated) or NuGet renders broken images (csproj not updated).

- [ ] 6c.1 Rewrite `README.md:2` from:
  ```markdown
  ![Lines](.github/badges/lines.svg) ![Methods](.github/badges/methods.svg) ![Branches](.github/badges/branches.svg)
  ```
  to:
  ```markdown
  ![Lines](https://raw.githubusercontent.com/07artem132/SignalCli.NET/main/.github/badges/lines.svg) ![Methods](https://raw.githubusercontent.com/07artem132/SignalCli.NET/main/.github/badges/methods.svg) ![Branches](https://raw.githubusercontent.com/07artem132/SignalCli.NET/main/.github/badges/branches.svg)
  ```
- [ ] 6c.2 Update `.github/workflows/dotnet-desktop.yml` at 4 emission sites — replace every `.github/badges/<name>.svg` with the absolute URL. Sites by line number (subject to drift — search `\.github/badges/` to locate):
  - Line ~166 — `sed -i "${FIRST_EMPTY_LINE}i![Lines](...)...!"` (insert branch).
  - Line ~172 — `echo "![Lines](...) ..." >> README.md.new` (rewrite branch).
  - Line ~181 — `echo "![Lines](...) ..." >> README.md` (create-from-scratch branch).
  - Line ~203 — `echo "![Lines](...) ..." >> coverage-summary.md` (PR-comment branch).
- [ ] 6c.3 Add to `src/SignalCli/SignalCli.csproj` `<PropertyGroup>`:
  ```xml
  <PackageReadmeFile>README.md</PackageReadmeFile>
  ```
  Add a new `<ItemGroup>`:
  ```xml
  <ItemGroup>
    <!-- audit-followup-2026 (badge-url-fix §6c.3): include repo-root README у NuGet pack.
         Без цього CI warning'ить "package is missing a readme" і nuget.org показує лише
         description. Badge-URL'и у README мають бути absolute (§6c.1) інакше nuget.org-
         renderer показуватиме broken images. -->
    <None Include="..\..\README.md" Pack="true" PackagePath="\" />
  </ItemGroup>
  ```
- [ ] 6c.4 `dotnet build src/SignalCli/SignalCli.csproj -p:TreatWarningsAsErrors=true` — confirm:
  - Build succeeds.
  - NO warning `The package SignalCli.NET.<version> is missing a readme.`
- [ ] 6c.5 `dotnet pack src/SignalCli/SignalCli.csproj --no-build` — confirm produced `.nupkg` contains README.md at root (verify via `unzip -l SignalCli.NET.<version>.nupkg | grep README.md`).
- [ ] 6c.6 Manual verification (when PR open): preview the README diff on GitHub.com — badges still render (relative→absolute is transparent for github.com itself).
- [ ] 6c.7 Update `CHANGELOG.md [3.0.x]` entry: `### 🐛 Виправлено` — "coverage badges now use absolute URLs so they render on nuget.org, IDE markdown previewers, and other non-github.com markdown renderers (relative paths were resolving to broken `http://.github/...` URLs outside github.com)".

## 6a. AddSignalCli idempotency fix (capability `addsignalcli-idempotency-fix`)

- [ ] 6a.1 In `src/SignalCli/Extensions/ServiceCollectionExtensions.cs`, define a private sentinel type at file scope:
  ```csharp
  // audit-followup-2026 (addsignalcli-idempotency-fix): sentinel-type marker для guard'у.
  // Тип private nested щоб consumer'и не могли випадково зарегати/перевірити його.
  private sealed class SignalCliRegistrationMarker { }
  ```
- [ ] 6a.2 Replace the three broken guards in `AddSignalCli(Action<SignalCliOptions>?)`, `AddSignalCli(IConfiguration)`, and `AddSignalCli(Action<Config>?)` from:
  ```csharp
  if (services.Any(d => d.ServiceType == typeof(IOptions<SignalCliOptions>)
                        || d.ServiceType == typeof(SignalCliOptions)))
      return services;
  ```
  to:
  ```csharp
  if (services.Any(d => d.ServiceType == typeof(SignalCliRegistrationMarker)))
      return services;
  services.AddSingleton<SignalCliRegistrationMarker>();
  ```
- [ ] 6a.3 Re-introduce `OptionsValidationTests.AddSignalCli_CalledTwice_SecondCallIsNoOp` (was removed during landing — left a TODO comment in the file). Assertions:
  - Descriptor count after second call equals descriptor count after first call.
  - Resolved `IOptions<SignalCliOptions>.Value.AppHome` matches FIRST call's value.
- [ ] 6a.4 Add `AddSignalCli_MixedOverloadsCalled_SecondIsNoOp` — first call uses `Action<SignalCliOptions>` overload, second uses `IConfiguration` overload. Same assertions.
- [ ] 6a.5 Add `HostedServices_RegisteredExactlyOnce_RepeatedRegistration` — count `services.Where(d => d.ServiceType == typeof(IHostedService))`; assert that across `AddSignalCli` × N the count of NEW hosted-service descriptors stays at 3.
- [ ] 6a.6 Update `CHANGELOG.md [3.0.3]` (or appropriate patch version) with `### 🐛 Виправлено` section documenting:
  - Pre-fix broken behavior (configure delegate re-runs, 3 duplicate hosted services per extra call).
  - Fix mechanism (sentinel-type marker).
  - Explicit note: `CHANGELOG.md [3.0.0]` idempotency claim is now correct for the first time.
- [ ] 6a.7 Remove the `// TODO: новий OpenSpec change idempotency-fix …` comment block from `Tests/SignalCli.Tests/OptionsValidationTests.cs` (line ~181); it's superseded by the re-introduced test.
- [ ] 6a.8 `dotnet test` — all green; the 3 new idempotency tests pass.

## 6b. Low-priority polish (capability `low-priority-polish`)

- [ ] 6b.1 Remove the `(Action<SignalCliOptions>)` cast from `Example/SignalCli.Example/Program.cs:30`. After rewrite the line reads `services.AddSignalCli(o => { … });`. Verify with `dotnet build Example/SignalCli.Example/SignalCli.Example.csproj -p:TreatWarningsAsErrors=true`.
- [ ] 6b.2 Append a paragraph to `CLAUDE.md` "Established patterns — Background loops + time" documenting the .NET 10 [`BackgroundService.ExecuteAsync` behaviour change](https://learn.microsoft.com/dotnet/core/compatibility/extensions/10.0/backgroundservice-executeasync-task). State explicitly:
  - The entire body of `ExecuteAsync` runs on a background thread starting in .NET 10.
  - Do NOT place startup-blocking initialization at the top of `ExecuteAsync` expecting it to run before other services start. Use the constructor or `StartAsync` override for that.
  - `SignalCliHealthMonitor.ExecuteAsync` already complies (first line is `new PeriodicTimer(...)` + `WaitForNextTickAsync` — no synchronous prefix).
- [ ] 6b.3 No tests required for this capability — `low-priority-polish` is doc/example only.

## 7. Final pass

- [ ] 7.1 Run full test suite: `dotnet test SignalCli.sln`. Expected: **215 + (3 regression-guard) + (12 edge-case) + (5 integration) = 235** tests on a platform with bundled runtime; integration tests skip otherwise.
- [ ] 7.2 `dotnet build -p:TreatWarningsAsErrors=true` from scratch — no IL2026/IL3050/CS warnings.
- [ ] 7.3 Update `CHANGELOG.md` `[3.1.0]` section (or `[3.0.1]` if no public API changes — confirm by running the new `PublicApiSurfaceTests` baseline). Likely `3.0.1` since the only public-surface effect is removing `[RequiresUnreferencedCode]` attributes from one overload (additive, not breaking).
- [ ] 7.4 Bump `<Version>` in `src/SignalCli/SignalCli.csproj` accordingly.
- [ ] 7.5 Update `CLAUDE.md`:
  - Reconcile the contradiction in §1.3 above.
  - Add new entry under "Established patterns" for the regression-guard test trio: "All `[Obsolete]` removal-version strings, all `[LoggerMessage]` EventIds, and the public API surface are pinned by reflection-based regression-guard tests under `Tests/SignalCli.Tests/RegressionGuards/`."
- [ ] 7.6 `npx -y @fission-ai/openspec@latest validate audit-followup-2026 --strict` final-green.
- [ ] 7.7 PR description references this change directory.
