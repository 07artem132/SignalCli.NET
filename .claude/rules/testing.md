---
paths:
  - "Tests/**"
---

# Testing patterns

## FakeTimeProvider / wall-clock-independent suites

- **Tests in `SignalCliHealthMonitor/` and `SignalCliHostedService/Restart*/` must not call `Task.Delay(>10ms)`.** Use `FakeTimeProvider.Advance(...)`. If you find yourself wanting to wait for real time in those suites, you are reaching for the wrong tool. See root CLAUDE.md § Critical rule #11 ("No wall-clock in tests").
- **.NET 10 `BackgroundService.ExecuteAsync` runs entirely on a background thread** ([compatibility breaking change](https://learn.microsoft.com/dotnet/core/compatibility/extensions/10.0/backgroundservice-executeasync-task)) — when writing a test that fires `StartAsync` and expects the first iteration to have run synchronously, that assumption no longer holds; await a signal (semaphore / `TaskCompletionSource`) instead of fire-and-poll. See `.claude/rules/patterns.md` § Background loops + time for the production-side details.

## Regression guards (reflection-based defensive tests)

These tests pin CLAUDE.md-declared invariants at build time. Each is small (~50-100 LOC), reflection-based, and runs in the unit test suite. **When you introduce a new "do not regress" rule in CLAUDE.md, prefer adding a matching reflection-based guard over relying on narrative discipline.**

- **`JsonContextRegistrationTests`** (shipped in `post-modernize-tuning` §6.12) — every `*Parameters` / `*Response` DTO in `Models/Signal/*` MUST be registered in `SignalJsonContext`. Otherwise the source-gen-only JSON path throws `NotSupportedException` at runtime.
- **`ObsoleteMessageConsistencyTests`** (shipped in `audit-followup-2026/regression-guards`, v4.0.0) — every `[Obsolete("...; will be removed in N.0")]` message has N strictly greater than the current package major. Drift is the M-1 audit finding made impossible going forward.
- **`EventIdBlockTests`** (shipped in `audit-followup-2026/regression-guards`, v4.0.0) — every `[LoggerMessage(EventId = X)]` lies inside the block reserved for its `*Log.cs` class per the "Logging" table in `.claude/rules/patterns.md`. A new `[LoggerMessage(EventId = 250)]` on `JsonRpcClientLog` (whose block is 300-399) fails the build.
- **`PublicApiSurfaceTests`** (shipped in `audit-followup-2026/regression-guards`, v4.0.0) — baseline-diff at `Tests/SignalCli.Tests/RegressionGuards/SignalCli.public-api.txt` (1087 lines as of v4.0). Intentional public-API changes update the baseline in the same PR; accidental ones are caught immediately with unified-diff output telling the developer exactly which member to add/remove.

Privacy-guard tests (`PrivacyLoggingTests`, `ObservabilityPrivacyTests` with `MeterTagValues_AreOnlyKnownEnumLiterals`) are part of this family too — they pin root CLAUDE.md § Critical rule #1.

## Test hygiene

- **Zero `xUnit1031` violations** (`DoNotUseBlockingTaskOperationsInTestMethod`). If a new test must use sync-blocking on purpose — add `[SuppressMessage("xUnit", "xUnit1031", Justification="…")]` with a justification; otherwise the build will fail because `Tests/SignalCli.Tests.csproj` opts into `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- **Listener-fan-out in tests must be thread-safe.** `ActivitySource.AddActivityListener` and `MeterListener` are global registrations; callbacks may arrive from parallel-test threads. Use `Lock` + snapshot pattern (see `ObservabilityPrivacyTests._captureLock`) for any captured-collection access, otherwise `List<T>` throws `Collection was modified` intermittently.
