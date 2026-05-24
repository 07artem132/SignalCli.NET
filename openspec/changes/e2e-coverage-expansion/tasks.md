# Tasks — e2e-coverage-expansion

## 0. Setup

- [ ] 0.1 Branch off the post-audit-remediation work (or any subsequent main).
        Single-commit change.
- [ ] 0.2 Run `npx -y @fission-ai/openspec@latest validate e2e-coverage-expansion --strict`
        and confirm green before any source edits.
- [ ] 0.3 Verify path-to-7 prerequisite: confirm that
        `Tests/SignalCli.Tests.Integration/SignalCliE2EGracefulShutdownTests.cs`
        already ships the graceful-shutdown E2E (it does, per audit v2.1 re-read
        of commit 706cf27). If somehow missing, that test must land first as
        score-7 prerequisite — but expect it to already be there.

## 1. New E2E test (capability `e2e-coverage-expansion`)

- [ ] 1.1 Create `Tests/SignalCli.Tests.Integration/SignalCliE2EParallelRpcCorrelationTests.cs`
        following the shape of `SignalCliE2EGracefulShutdownTests.cs`:

        - `[Trait("Category", "E2E")]` on the class.
        - Same `IsRuntimeAvailable(SignalCliOptions, out string)` static helper
          (copy-paste from the existing file — extracting to a shared base is
          out of scope; 3 files all use the same gate).
        - Same `Host.CreateDefaultBuilder` + `AddSignalCliWithBundledRuntimeDefaults`
          scaffolding with the same Linux-native vs Java branching.
        - `RequestTimeoutSeconds = 30` (default).
        - `StopTimeoutSeconds = 5`.
        - `MaxRestartAttempts = 0` (single-shot test).
        - Unique `StoragePathCli` per test run (timestamp + Guid).

- [ ] 1.2 Implement `[Fact] Process_ParallelVersionCalls_AllResolveToCorrectResponseById`
        per the assertion shape in `design.md`:

        ```csharp
        // Resolve signal-cli client from DI.
        var signalCli = host.Services.GetRequiredService<ISignalCliClient>();
        var hostedService = host.Services.GetRequiredService<SignalCli.Services.SignalCli.SignalCliHostedService>();
        await hostedService.WaitForReadyAsync(startCts.Token);

        // Fire 10 concurrent VersionAsync calls.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        const int parallelism = 10;
        var versionTasks = Enumerable.Range(0, parallelism)
            .Select(_ => signalCli.VersionAsync(cts.Token))
            .ToArray();

        var results = await Task.WhenAll(versionTasks).WaitAsync(cts.Token);

        Assert.Equal(parallelism, results.Length);
        var distinctVersions = results.Select(r => r.Version).Distinct().ToArray();
        Assert.Single(distinctVersions);
        foreach (var r in results)
        {
            Assert.NotNull(r);
            Assert.False(string.IsNullOrEmpty(r.Version),
                "Empty version → response landed in wrong TCS (id-correlation broken).");
        }
        ```

- [ ] 1.3 finally-block: `Directory.Delete(cfg.StoragePathCli, recursive: true)`
        best-effort cleanup (mirrors existing E2E pattern).

## 2. Documentation

- [ ] 2.1 Update `CLAUDE.md` "Audit baseline → Тестова база":
        change "E2E tests: **≥ 1**" to "E2E tests: **≥ 2**".
        Add one-liner explaining the second test pins the parallel-RPC-correlation
        invariant against real virtual-thread dispatcher.
- [ ] 2.2 No CHANGELOG.md update required for an Integration test addition
        (CHANGELOG focuses on consumer-facing API; Integration tests are
        internal quality bar). Skip per CLAUDE.md "Working style → don't
        create *.md documentation files unless asked".

## 3. Verify + commit

- [ ] 3.1 `dotnet build SignalCli.sln` → 0 warnings, 0 errors.
- [ ] 3.2 `dotnet test Tests/SignalCli.Tests.Integration/SignalCli.Tests.Integration.csproj`
        → all E2E tests pass on a machine with bundled-JRE installed (locally);
        on CI without bundled runtime they SKIP gracefully (existing pattern).
- [ ] 3.3 Run the new test specifically — verify it actually exercises the
        path: `dotnet test --filter "FullyQualifiedName~ParallelRpcCorrelation"`.
        Expected: 1 test passes (or skips with the runtime-missing message).
- [ ] 3.4 `git commit` with message:

        ```
        test: parallel RPC correlation E2E — Integration score 7 → 8

        e2e-coverage-expansion: pins CLAUDE.md "Parallel request processing
        → match by id, not by order" against the real signal-cli
        virtual-thread dispatcher. Unit tests cover correlation via mocked
        Subject<T>; this E2E proves _pendingRequests : ConcurrentDictionary
        survives concurrent real-process responses arriving in execution-time
        order, not request order.

        New file: SignalCliE2EParallelRpcCorrelationTests.cs.
        Same skip-gate pattern as existing 3 E2E files. 10 concurrent
        VersionAsync calls must all complete with the same version string
        within RequestTimeoutSeconds*2.
        ```

## 4. Post-merge archive

- [ ] 4.1 After PR merges, run from the archive workflow in CLAUDE.md:
        `npx -y @fission-ai/openspec@latest archive e2e-coverage-expansion --yes --skip-specs`
- [ ] 4.2 Update CLAUDE.md "Implemented, merged, archived" list to include the
        new archive entry.

## Optional follow-ups (out of scope for this change)

- [ ] **OF.1** Extract `IsRuntimeAvailable` + bundled-runtime DI scaffolding
        into a shared `IntegrationTestBase` class. Currently duplicated across
        4 E2E files (`E2EVersion`, `E2EGracefulShutdown`, `E2EAdditional`,
        and the new `E2EParallelRpcCorrelation`). Tradeoff: shared base adds
        coupling; current copy-paste makes each test independently runnable.
        Not blocking this change.
- [ ] **OF.2** Future score-9 path: a synthetic signal-cli mock-server (in-proc
        or test-harness fake) that simulates protocol violations signal-cli
        itself never emits — e.g. responses arriving for nonexistent request
        ids, dual-field responses, untyped error payloads. Substantially
        larger change; separate OpenSpec proposal warranted.
