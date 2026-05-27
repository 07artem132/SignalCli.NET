<!-- always-load: no paths -->

# Audit debt + working style + prevention checklist

This file collects cross-cutting agent-instruction-quality rules that apply to ANY edit session — future development guardrails, the "how we discovered these issues" prevention checklist, and the working-style conventions Claude and the user landed during 2.1.0 work. Loaded always (no `paths:` frontmatter) because every PR benefits from these.

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

### `docs/api/` prose drift after API-changing capability (→ post-4.10.0 PR #21 doc-sync)

**Що сталося:** 4.10.0 `api-coverage-audit-followup` ship'нув Cap 4 (`JsonPayment.Receipt: byte[]` → `byte[]?`) і Cap 5 (`[Obsolete]` на `IdentityChangedException`). Обидва — production-side type changes, але `docs/api/messaging.md` залишився з застарілою prose: common-exceptions table все ще описувала `IdentityChangedException` як "підтип для re-install'ів", а `SendPaymentNotificationAsync` footnote показувала `(Note, Receipt: byte[])` без `?`. RG09 (`DocsApiCoverageTests`) **не fail'ив** бо названі типи все ще згадуються — substring/word-boundary match passes; але семантика prose стала misleading. Drift спіймано лише user-ініційованою перевіркою docs ↔ code; виправлено окремим follow-up PR #21.

**Чому RG09 не зловив:** RG09 enforce'ить **наявність згадки** (anti-omission guard), не правильність опису. Catching prose-staleness вимагає або (a) AST/MSDoc-based comparison (rejected у `audit-followup-2026` як "infrastructure cost > value"), або (b) PR-time review discipline. (b) — поточний enforcement.

**Правило (нова checklist-розширення):** при API-changing capability, **`tasks.md` має містити explicit task** перед release commit'ом:
- `grep -rn '<AffectedType>' docs/ README.md CHANGELOG.md` — знайди всі живі згадки.
- Для кожної згадки: чи опис ще true після change'у? Якщо ні — оновити у тому ж PR.
- Якщо забув і drift проліз у production — окремий `docs(*)`-only PR із посиланням на capability що його спричинив.

Це extension до **`.claude/rules/openspec-workflow.md § README + docs/api/ PR-time triggers`** — там той самий fact'у з іншого ракурсу. Обидва файли тепер посилаються одне на одного.

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
- **§0.5 cite-and-read, not cite-and-trust.** When citing an upstream line range as protocol evidence, read those lines AND grep the broader file/module for contradictory or extending logic before deriving a wrapper-side type/method/enum from the claim. The Wave-1 `IdentityChangedException` finding (deprecated in 4.10.0) cited correct lines for the `-4 UntrustedKeyErrorException → JSON-RPC` mapping, but didn't verify those lines exclude alternative interpretations — there is no `IDENTITY_NEW`/`IDENTITY_CHANGED` distinguisher anywhere in signal-cli's `JsonSendMessageResult` or `SendMessageResultUtils`, so the speculative split type was dead-throw API from day one. Rule: when about to add a typed exception, enum, or branch that depends on an upstream behavioral split, `grep` the whole upstream module (not just the cited file) for the keyword family of the proposed split — if you don't find a distinguishing token, the split is speculative and should not ship.
- **Custom CI workflows: prefer static-check over consumer-build-simulation.** `runtime-smoke.yml` `jre-guard-static-check` `grep`s the `.targets` files for the post-extract `<Error Condition>` guard text + an actionable hint — catches the "guard removed" regression class in 3 seconds on ubuntu-latest. The original attempt to simulate consumer-build by deleting `bin/java` after JRE-package build never triggered the guard (it lives on consumer's `TargetDir`, not the runtime-package's own build). When a CI check needs a real consumer, look for an existing one (e.g. `Tests/SignalCli.Tests.Integration` for native delivery) instead of bolting on a synthetic consumer project.
- **PR webhook auto-handling: skip purely informational bot comments.** `github-actions[bot]` posts coverage badges (`marocchino/sticky-pull-request-comment`) and `Test Results 0/0` after every CI run; these are NOT review comments and require NO action. Address only real CI failures and human-author comments. CI-failure response loop: read `gh api repos/<owner>/<repo>/actions/jobs/<id>/logs` (individual job log, more reliable than `gh run view`), find root cause, fix, push. Most failures during this PR cluster batched into single fix-commits per CI-cycle.
- **`git pull --rebase` before every push to `main`.** Automated coverage-badge bot (`stefanzweifel/git-auto-commit-action`) commits to main after every successful CI run with `[skip ci]` — your local main lags within minutes of any merge. Force-pushes are forbidden (root CLAUDE.md § Git); rebase is the only safe path.
- **Verify-then-tick is the bookkeeping rule for OpenSpec tasks.** Don't mass-`sed 's/\[ \]/[x]/'` without first confirming each unchecked task is actually shipped. Round-16 audit found `agent-friendly-modernization` with 55 unchecked tasks but CLAUDE.md confirmed "shipped as 2.1.0" — safe to bulk-tick. Generalize: cross-reference CLAUDE.md "Implemented and merged" before sweeping ticks; if status ambiguous, leave for explicit review.
- **Cross-check root CLAUDE.md "Implemented, merged, archived" against live source before claiming a deprecation is "already removed".** The 2026-05-24 audit found CLAUDE.md said `Version()` and `AddSignalCli(Action<Config>?)` were "Already removed in 3.0" while both still existed in source. The check is one `grep` (`grep -rn '\[Obsolete' src/`) — do it before editing the "Backward compatibility convention" section. Drift here trains agents to disbelieve the doc; the `ObsoleteMessageConsistencyTests` regression-guard (in `audit-followup-2026`) automates this.
- **When the audit lists a HIGH / MEDIUM finding, file an OpenSpec change before fixing — even tiny doc-sync fixes.** The regression-guard test that prevents recurrence is the durable artifact, not the fix itself. The fix without the guard is one less file showing the right text; the fix with the guard is a permanent invariant. `audit-followup-2026` shape: each capability has its own spec; one commit per capability; final test-count post-merge documented in the proposal.
- **Validate the agent instructions periodically.** Microsoft's *Custom instructions for AI agents* guide recommends: "Test your custom instructions by asking the AI to write a representative task; if the AI still produces the wrong pattern, add a more explicit rule." Apply this to CLAUDE.md — when you notice an agent (Claude, Copilot, etc.) repeatedly violating a rule we thought was written, the rule is too implicit. Make it explicit, with a code-anchored example, in the relevant "Established patterns" subsection.
- **GitHub Copilot reads `.github/copilot-instructions.md`, not `CLAUDE.md`.** Our repo currently has only `CLAUDE.md`. If a contributor uses Copilot/Cursor/Windsurf on this repo and needs the same patterns to apply, a tiny `.github/copilot-instructions.md` containing a single line ("This project's authoritative agent guidance lives in `CLAUDE.md`. Read it first.") is sufficient — multiplying the patterns into multiple files would create a drift-vector. Not added today; mentioned as the cheap escape valve if Copilot users complain.
- **The `awesome-copilot` `CSharpExpert.agent.md` is a complementary reference**, not a replacement for CLAUDE.md. CLAUDE.md describes *this* project's invariants; `CSharpExpert` describes general modern-C# conventions. Both can apply; do not duplicate.
