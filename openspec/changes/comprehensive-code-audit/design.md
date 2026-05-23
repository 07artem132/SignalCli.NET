# Design — comprehensive code audit

## Method

1. **Decompose** the codebase into subsystems so each can be reviewed whole (not by excerpt):
   - S1 Process lifecycle: `SignalCliHostedService`, `ProcessWrapper`, `ProcessRunner`, `ProcessFactory`, `ProcessConfig`.
   - S2 State & health: `ProcessStateManager`, `SignalCliHealthMonitor`, `ProcessState`.
   - S3 RPC transport: `JsonRpcClient`, `JsonRpcClientHostedService`, `JsonRpcClientFactory`, RPC models.
   - S4 Serialization: `SignalJson`, `SignalJsonContext`, `Envelope` and DTOs.
   - S5 Signal API: `SignalService`, `SignalMessage`, `SignalAccounts`, `SignalDevices`, `SignalGroups`, `SignalEventService`.
   - S6 Config & runtime: `Config`, runtime/native/JRE packaging, download scripts.
   - S7 Utilities & FS: `TextStyleParser`, `MimeTypeHelper`, `AttachmentEntry`.

2. **Best-practice dimensions**, each grounded in Microsoft Learn (cite the URL per finding):
   - Async & cancellation: `CancellationToken` flow, no `async void`, no sync-over-async (`.Result`/`.Wait()`/`.GetAwaiter().GetResult()`), `ConfigureAwait(false)`, `TaskCompletionSource` with `RunContinuationsAsynchronously`.
   - Hosted services: `BackgroundService`/`IHostedService` start/stop semantics, blocking `StartAsync`.
   - Process: redirected-stream deadlocks, async reads, `WaitForExitAsync`, process-group kill, disposal/zombie avoidance.
   - System.Text.Json: source-gen coverage & fallback, **`JsonDocument` disposal**, `JsonElement` lifetime, max-depth/limits, trimming/AOT readiness.
   - Reactive: `Subject` thread-safety & disposal, `AsObservable`, subscription leaks, back-pressure.
   - Resource management: `IDisposable`/`IAsyncDisposable` pattern, double-dispose, `ObjectDisposedException`, finalizers.
   - Concurrency: locking discipline, shared mutable state, the pending-request dictionary.
   - Logging: privacy, structured logging, high-perf `LoggerMessage` (CA1848).
   - Nullability: annotations vs runtime reality.
   - Security: arg injection, path traversal, input validation, supply chain.
   - DI: service lifetimes, captive dependencies, disposable singletons.

3. **Parallelize** the read with subagents (one per subsystem) that must report concrete findings as `severity | file:line | issue | why (MS-docs) | recommendation`; the auditor then **verifies each high/critical finding by reading the cited lines** before it enters the report (no unverified claims).

4. **Tests**: classify each test as real-value vs tautological; measure coverage; define the integration-test gap and a concrete plan (a real `signal-cli` JSON-RPC round-trip, ideally reusing the bundled JRE so CI needs no system Java).

5. **Docs**: XML-doc completeness on public surface, README accuracy, CLAUDE.md/copilot-instructions accuracy, language-consistency note.

## Severity rubric

- **Critical**: data loss, deadlock/hang, crash, security hole, or incorrect results in a normal path.
- **High**: resource leak, race under realistic load, swallowed error, or a documented-but-wrong behavior.
- **Medium**: best-practice deviation with limited blast radius; missing test for a risky path.
- **Low**: style, naming, doc, micro-optimization.

## Output

`AUDIT-FINDINGS.md` at repo root: executive summary, table of findings (id, severity, area, `file:line`, MS-docs link, recommendation), test-quality section, integration-test plan, documentation section. Each accepted finding is mirrored as a checkbox in this change's `tasks.md` so remediation can be tracked and split into follow-up changes.

## Non-goals

- No production-code edits under this change (the C# 13/14 + Rx + CLAUDE.md work already in flight is tracked separately).
- No new runtime dependencies.
