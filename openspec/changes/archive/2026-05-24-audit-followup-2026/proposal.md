# Audit follow-up 2026 — doc-sync, .NET 10 polish, test hardening

## Why

A read-only audit against Microsoft Learn for **.NET 10 / C# 14 best practices** and **agent-friendliness** (2026-05-24, after `post-modernize-tuning` archive) found that the library:

- already shipped 3.0.0 with most of the modern .NET stack (AOT-clean, `[OptionsValidator]`, `IHostedLifecycleService`, `TimeProvider` everywhere, single `ActivitySource`/`Meter`, source-gen JSON only),
- has **no architectural defects** worth a new change,
- but does carry **three pockets of follow-up debt** that we should burn down before they ossify, and **non-trivial gaps in test coverage** of the very invariants `CLAUDE.md` declares to be law.

Three pockets of debt:

1. **Stale `[Obsolete]` doc-sync.** Six call-sites still say *"will be removed in 3.0"* in a codebase that **is** 3.0.0. `CLAUDE.md` itself contradicts the source on whether `ISignalCliClient.Version()`, `AddSignalCli(Action<Config>?)`, and `Config` were "Already removed in 3.0" or "in flight, will be removed in 4.0" — they are in fact in-flight. Agents (and humans) cannot reason about deprecation timeline when the source lies.
2. **Two .NET 10 polish items.** `JsonSerializerOptions.AllowDuplicateProperties = false` (new in .NET 10) closes a [signal-cli-side](https://github.com/AsamK/signal-cli) attack surface for free. `Microsoft.Extensions.Configuration.Binder` source-generator (`<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>`) makes `AddSignalCli(IConfiguration)` AOT-safe — today that overload is the *only* surface still bearing `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`.
3. **Test coverage gaps.** Despite 215+ tests across 40 files, several declared invariants from `CLAUDE.md` (EventId blocks per service, public API surface freeze, `Obsolete`-message version consistency, channel-overflow `EventsDropped` counter actually firing, observability `Activity` having a non-zero duration, `AtomicCounter` wraparound, attachment boundary at exactly 15MB encoded, JSON-RPC error codes `-32601`/`-32700` handling, NUL-byte filename) are **not** asserted by tests. Integration coverage is also thin — a single E2E test (`Version_RealSignalCli_ReturnsNonEmpty`) protects the start/handshake/version path but not the restart loop, the health-monitor cycle, the `IConfiguration` overload, or mid-flight `DisposeAsync`.

Doing this as one change keeps the doc-sync, the .NET 10 polish, and the safety-net tests coupled — because the regression-guard tests in §4 are what *prevent* the doc-sync from drifting again, and the .NET 10 changes themselves want tests landed in the same commit.

## What Changes

Grouped by capability (each is a separate spec under `specs/`):

- **`obsolete-doc-sync`** — every `[Obsolete("...will be removed in 3.0")]` attribute and every "буде видалений у 3.0" XML-doc/comment in the live code is rewritten to read 4.0 (or the deprecation is removed if the type truly is gone). `CLAUDE.md` "Implemented, merged, archived" + "Backward compatibility convention" sections are reconciled with the actual source. **No behavior change.**
- **`json-hardening`** — `SignalJson.Options` opts into `AllowDuplicateProperties = false` so a malformed signal-cli response with duplicate keys is rejected loudly at the JSON layer. Round-trip tests added.
- **`configuration-binder-aot`** — `<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>` enabled on `SignalCli.csproj`; the `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` attributes are removed from `AddSignalCli(IConfiguration)` and `ConfigureOptionsFromConfiguration` once the generator emits AOT-safe binding code.
- **`regression-guards`** — three new reflection-based defensive tests landing in `Tests/SignalCli.Tests/`:
  1. `ObsoleteMessageConsistencyTests` — scans every `[Obsolete]` attribute string in `SignalCli.dll`, parses "will be removed in N.0" tokens, asserts `N > current major`. Catches the M-1 class of drift the moment it reappears.
  2. `EventIdBlockTests` — scans every `[LoggerMessage(EventId = …)]` attribute in every `*Log.cs` file, asserts the EventId is within the block range declared by `CLAUDE.md` for that service. Catches an agent stealing EventId 250 (reserved for `SignalCliHealthMonitorLog`) for a new `JsonRpcClientLog` line.
  3. `PublicApiSurfaceTests` — generates a sorted line-per-API listing of every `public` member of `SignalCli.dll`; baseline file under `Tests/SignalCli.Tests/PublicApi/SignalCli.public-api.txt`; test fails on any unintended public-surface diff. Catches accidental new-`public-class` regressions.
- **`integration-tests-expansion`** — `Tests/SignalCli.Tests.Integration/` grows by five new E2E tests (Java/native runtime auto-detect like the existing one, skip-on-missing). Each one exercises a real signal-cli, but **none** require a registered Signal account or network — the same gate as today:
  1. `Process_StartStopRestart_TransitionsObservedCorrectly` — `Start → Stop → Start` cycles a real process; asserts `ProcessStateManager.CurrentState` transitions match `NotStarted → Starting → Running → Stopping → Stopped → Starting → Running`.
  2. `Process_KilledExternally_AutoRestartReclaimsProcess` — `MaxRestartAttempts=1`, externally `Process.Kill` the real signal-cli, asserts `OnProcessExited` triggers auto-restart and the new process answers `version` again.
  3. `HealthMonitor_OverInterval_LastPingResultUpdates` — `HealthCheckIntervalSeconds=2`, waits 5 wall-clock seconds, asserts `SignalCliHealthMonitor.LastPingResult.Ok == true` and the timestamp is fresh.
  4. `DisposeAsync_MidFlight_LeavesNoOrphanProcess` — start, fire `VersionAsync`, immediately `await host.DisposeAsync()`; assert the OS no longer has a `signal-cli` / `java` child of the test PID.
  5. `Configuration_FromIConfiguration_StartsRealCli` — uses the `AddSignalCli(IConfiguration)` overload (binding from in-memory dictionary) to start the real process and call `version`. Validates the new `configuration-binder-aot` path end-to-end.
- **`badge-url-fix`** *(new — discovered during landing post-mortem)* — coverage badges in `README.md` use relative paths `.github/badges/lines.svg` that work only on github.com's own markdown renderer. Other renderers (NuGet.org, IDE previewers, third-party gallery sites) interpret `.github` as a hostname → render as `http://.github/badges/lines.svg` (broken). Three coordinated fixes: (1) rewrite the 3 badge lines in `README.md` to absolute `https://raw.githubusercontent.com/07artem132/SignalCli.NET/main/.github/badges/...` URLs; (2) update the 4 hardcoded badge-emission sites in `.github/workflows/dotnet-desktop.yml` (lines ~166, 172, 181, 203) — otherwise the `stefanzweifel/git-auto-commit-action` step reverts the README fix on the next CI run with its `[skip ci]` commit; (3) add `<PackageReadmeFile>README.md</PackageReadmeFile>` + `<None Include="..\..\README.md" Pack="true" PackagePath="\" />` to `src/SignalCli/SignalCli.csproj` so the README ships in the NuGet package — the build currently warns *"The package SignalCli.NET.3.0.0 is missing a readme"*, and once it's included, badges with relative paths would be broken on nuget.org. All three changes ship in the same commit because they are mutually-dependent.
- **`addsignalcli-idempotency-fix`** *(new — discovered during landing of this change set)* — `ServiceCollectionExtensions.AddSignalCli` was declared idempotent in `CHANGELOG.md [3.0.0]`, but the runtime guard `services.Any(d => d.ServiceType == typeof(IOptions<SignalCliOptions>) || d.ServiceType == typeof(SignalCliOptions))` never matches: `IOptions<T>` is registered open-generic (`typeof(IOptions<>)`), not concrete, and `SignalCliOptions` itself is never registered as a service. Every repeated `AddSignalCli` call (a) re-runs the configure delegate (second-wins on options), (b) adds 3 duplicate `IHostedService` descriptors — `SignalCliHostedService`, `JsonRpcClientHostedService`, `SignalCliHealthMonitor` — so each starts twice. Discovered when `AddSignalCli_CalledTwice_SecondCallIsNoOp` test failed with `Expected: 28, Actual: 31` descriptor count. Fix: replace the broken guard with a sentinel-type check — register a private `SignalCliRegistrationMarker` on first call, short-circuit on subsequent calls if marker present.
- **`low-priority-polish`** — two cheap doc/example fixes that were flagged in the audit but did not justify a dedicated capability:
  - Drop the misleading `(Action<SignalCliOptions>)` cast in `Example/SignalCli.Example/Program.cs:30` — overload resolution is unambiguous; the cast suggests a non-existent problem to readers (especially AI agents copying the example).
  - Add a paragraph to `CLAUDE.md` "Established patterns — Background loops + time" noting the .NET 10 [`BackgroundService.ExecuteAsync` behaviour change](https://learn.microsoft.com/dotnet/core/compatibility/extensions/10.0/backgroundservice-executeasync-task) (entire body now runs on a background thread; do not put startup-blocking init at the top of `ExecuteAsync`). `SignalCliHealthMonitor` already complies — the note is forward-looking.
- **`edge-case-coverage`** — twelve targeted unit tests close the declared-invariant gaps. Full list in `tasks.md` §6; highlights:
  - JSON-RPC error codes `-32601` / `-32700` deserialize correctly; `error.data` field is preserved on `JsonRpcError` if present.
  - `AttachmentEntry.ToDataUri` filename ≈ 15 000 000 base64 chars: the inline / temp-file boundary is sharp (`< MaxInlineEncodedAttachmentBytes` exclusive).
  - `AttachmentEntry` with NUL-byte / Unicode-RTL filename: `SafeFileName` produces a safe ASCII fallback.
  - `AtomicCounter.Increment()` at `int.MaxValue` wraps to `int.MinValue` without throwing.
  - `SignalCliDiagnostics.EventsDropped` counter increments by exactly 1 per dropped event under channel overflow (today only the *absence* of PII is asserted, not the counter value).
  - `SignalCliDiagnostics.RpcDuration` records a non-zero positive value on a happy-path RPC.
  - `SubscribeAsync` follower receives the same `OperationCanceledException` when the leader's caller-token cancels mid-RPC.
  - `ForceRestartAsync` on a process currently in `Stopping` is a documented no-op (today untested).
  - `NotificationChannelCapacity = 1` (minimum) still delivers all messages in FIFO order.
  - `AddSignalCli` called twice in the same `IServiceCollection` is a documented no-op (today the guard exists, no test asserts it).
  - `SignalCliOptions.EnvironmentVariables` is captured by reference at host start — post-start mutation of the *external* dictionary must NOT leak to the running process (`IReadOnlyDictionary` defends this, but no test pins the snapshot semantics).
  - `JsonRpcResponse` with both `result` AND `error` present: `JsonRpcException` wins (defensive — should never happen per spec, but we choose).

## Capabilities

### New Capabilities

- `obsolete-doc-sync`: every `[Obsolete]` message and Ukrainian/English XML doc that refers to a removal version SHALL be consistent with the current package version + `CLAUDE.md` backward-compat convention.
- `json-hardening`: `SignalJson.Options.AllowDuplicateProperties` SHALL be `false` (new .NET 10 option).
- `configuration-binder-aot`: `AddSignalCli(IConfiguration)` SHALL be AOT-safe via the configuration-binding source-generator; `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` SHALL be removed from the overload.
- `regression-guards`: `[Obsolete]`-message consistency, `EventId` block adherence, and public-surface freeze SHALL each be asserted by an executable test.
- `integration-tests-expansion`: the Integration test suite SHALL cover the start/stop/restart cycle, external-kill auto-recovery, health-monitor cadence, mid-flight `DisposeAsync`, and the `IConfiguration` overload — each behind the same runtime-availability skip gate as today's `Version_RealSignalCli_ReturnsNonEmpty`.
- `edge-case-coverage`: twelve declared invariants (JSON-RPC error codes, attachment boundary, counter increment, channel-cap minimum, etc.) SHALL be pinned by unit tests.
- `addsignalcli-idempotency-fix`: `AddSignalCli` SHALL be truly idempotent across all three overloads — guard via private sentinel-type marker, not via framework-internal `IOptions<T>` descriptor check.
- `badge-url-fix`: coverage badges in `README.md` SHALL use absolute raw.githubusercontent.com URLs (not relative paths) so they render correctly outside github.com; the CI workflow SHALL emit the same absolute URLs (4 sites) to avoid auto-commit revert; NuGet package SHALL include the README.
- `low-priority-polish`: the Example file SHALL NOT carry a misleading `(Action<SignalCliOptions>)` cast; CLAUDE.md SHALL document the .NET 10 `BackgroundService.ExecuteAsync` behaviour change in its "Background loops + time" pattern block.

### Modified Capabilities

None of the existing capabilities under `openspec/changes/archive/2026-05-24-*/specs/` are weakened. Files touched by `regression-guards` and `edge-case-coverage` are test-only; files touched by `obsolete-doc-sync` are comments/attributes only; `json-hardening` adds one option-flag and a test; `configuration-binder-aot` adds one MSBuild property and removes two attributes.

## Out of scope

- **C# 14 `field` keyword adoption.** Not a regression; current code uses primary constructors / records, where `field` adds no clarity.
- **`JsonSerializerOptions.Strict` preset.** Strict implies `JsonUnmappedMemberHandling.Disallow`, which is incompatible with signal-cli's habit of adding new envelope fields between versions (forward-compat).
- **Removal of `Config` / `Version()` / `AddSignalCli(Action<Config>?)`.** That belongs in a future `4.0` change once the Integration E2E flow no longer depends on `Config.CreateDefault()`-auto-resolve. This change only fixes the doc-sync.
- **Migration to xUnit v3 / Microsoft.Testing.Platform.** Out of scope; would be its own change.
- **L-1: `AtomicCounter._seed` is `long` while `Increment()` returns `int`.** Flagged in audit as a minor type-mismatch. The wrap-around semantics is the deliberate design (per CLAUDE.md "Established patterns — `unchecked Interlocked.Increment` for monotonic ID counters"); the `int` return is what JSON-RPC `id` needs as `string` representation. Pinning the wrap behaviour as a test goes into `edge-case-coverage` (§6.c); the type itself stays as-is.
- **L-2: `Config.ToOptions` + `SignalCliOptionsExtensions.ToOptions(Config)` + `ServiceCollectionExtensions.CopyFrom` are three near-mirror field-copiers.** Adding a new `SignalCliOptions` property today requires updating all three sites. We accept this duplication trap because **all three sites disappear in 4.0** once `Config` is removed — collapsing them into a single mapper now is throwaway work. New `SignalCliOptions` fields landing before the 4.0 cut SHALL update all three sites manually; the test suite has no reflective drift-guard for this and we accept that risk.
