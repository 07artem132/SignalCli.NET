# Design — e2e-coverage-expansion

## Method

One new test file mirroring the existing
`SignalCliE2EGracefulShutdownTests.cs` shape: same skip-gate helper, same
bundled-JRE / native-Linux runtime detection, same `Host.CreateDefaultBuilder`
+ `AddSignalCliWithBundledRuntimeDefaults` scaffolding, same
`hostedService.WaitForReadyAsync` pre-condition. The only delta is the body of
the single `[Fact]` — instead of testing graceful Stop, it tests concurrent
`VersionAsync` correlation.

The test exercises the **real virtual-thread dispatcher** in
`JsonRpcReader.java:58` (signal-cli upstream) and the
**real `ConcurrentDictionary<string, TaskCompletionSource>` correlation** in
`JsonRpcClient._pendingRequests`. Unit tests like
`JsonRpcClientTests.InvokeMethodAsync_WhenResponseArrives_ShouldCompleteTask`
inject single responses via reflection into the private `ProcessMessageAsync`;
they cannot exercise concurrent reads from a real stdout because the test setup
uses a `MemoryStream` rather than a piped child process. The new E2E closes
exactly that gap.

## Test design rationale

### Why 10 concurrent calls (not 2, not 100)?

- **2** would not reliably exercise the dispatcher's virtual-thread spawning;
  signal-cli might serialize 2 close-spaced requests by accident. Need ≥ N
  where N exceeds any "small request" batching heuristic.
- **100** would inflate test runtime (each `version` round-trip is ~5-50ms on
  bundled JRE; 100 sequential would be ≥ 500ms, parallel limited by signal-cli's
  reader-loop). 10 covers the regression class within 200-500ms total.
- **10** is the same magnitude used by the existing unit test
  `SignalEventServiceDispatchTests.SubscribeAsync_Concurrent_TenCallers_…`
  which proves the same "concurrent calls, one RPC" pattern at the mock level.
  Consistency with prior art is a tie-breaker.

### Why `VersionAsync` specifically?

- **Idempotent.** No state mutation, no account dependency, safe to retry, safe
  to run concurrently against the real signal-cli without side effects.
- **Lightweight.** Returns a small fixed string; doesn't stress JSON
  serialization for the test's primary purpose.
- **Already proven E2E-callable.** Existing
  `SignalCliE2EVersionTests.Version_RealSignalCli_ReturnsNonEmpty` confirms the
  path works against bundled-JRE — we're just multiplying the call count.
- **Deterministic expected value.** All 10 responses MUST equal the same
  `VersionResponse.Version` string. If even one differs, something is
  catastrophically wrong (cross-talk between request slots).

### Strong assertion shape

```csharp
var versionTasks = Enumerable.Range(0, 10)
    .Select(_ => signalCli.VersionAsync(cts.Token))
    .ToArray();

var results = await Task.WhenAll(versionTasks).WaitAsync(cts.Token);

// (1) All 10 completed without timeout.
Assert.Equal(10, results.Length);

// (2) All 10 returned the SAME version string (no cross-talk: each
//     response landed in its own TCS, not someone else's).
var distinctVersions = results.Select(r => r.Version).Distinct().ToArray();
Assert.Single(distinctVersions);

// (3) Each result is a valid VersionResponse (not null, non-empty string).
foreach (var r in results)
{
    Assert.NotNull(r);
    Assert.False(string.IsNullOrEmpty(r.Version),
        "Empty version → deserialization landed wrong response into TCS.");
}
```

The CancellationToken bounds the test runtime; if any single task hangs (broken
correlation), it taints `Task.WhenAll` which then faults with `OperationCanceledException`
from the linked CTS — clean failure mode, no hung CI runner.

## Affected files

| File | Operation | Description |
|------|-----------|-------------|
| `Tests/SignalCli.Tests.Integration/SignalCliE2EParallelRpcCorrelationTests.cs` | NEW | Single `[Fact]` E2E with skip-gate helper |
| `CLAUDE.md` "Audit baseline → Тестова база" | MODIFIED | E2E count bumped from ≥1 to ≥2 |

No source code changes in `src/SignalCli/**`. No new package references — uses
the same `SignalCli.NET.HealthChecks`-tier dependencies as the existing E2E
files (`Microsoft.Extensions.Hosting`, `SignalCli.Extensions`, etc.).

## Risk analysis

**Risk 1: signal-cli's virtual-thread dispatcher might serialize concurrent
calls under load** (defensive throttling). If `VersionAsync` is internally
single-threaded in signal-cli, our test still proves correlation works (because
the responses still arrive in some order and must be routed by id), but it
doesn't prove the parallel-dispatcher path is exercised.
**Mitigation:** the test name is `Process_ParallelVersionCalls_AllResolveToCorrectResponseById` —
the assertion is on correlation, not on parallelism. If signal-cli serializes,
our wrapper still gets exercised; the test still catches a queue-based
regression.

**Risk 2: Flakiness from runtime startup latency.** Bundled-JRE cold start
can add 2-5s to the first `VersionAsync` call.
**Mitigation:** `WaitForReadyAsync` pre-condition (already used by existing E2E
tests); `RequestTimeoutSeconds * 2` bound (60s default, generous); CancellationToken
on `Task.WhenAll` for clean failure.

**Risk 3: This test never runs in CI** because the skip-gate triggers when
bundled-JRE is missing.
**Mitigation:** existing E2E suite has the same gate; the project's CI runs
bundled-JRE smoke in `runtime-smoke.yml` workflow (per CLAUDE.md). Test runs
locally on developer machines with the bundled-runtime packages installed and
on the CI job that exercises them.

## Why one commit (not split)

Single test file, no source change, no new dependency. Splitting into "add
test" + "update CLAUDE.md baseline" would create a midpoint where the baseline
documentation lies (claims ≥2 E2E but actual count is 1).
