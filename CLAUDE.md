# CLAUDE.md

Guidance for AI coding agents (Claude Code, Copilot, etc.) working in this repository.

## Project

**SignalCli.NET** — a .NET wrapper around [`signal-cli`](https://github.com/AsamK/signal-cli) (a Java app) that exposes a typed API for the Signal messenger. The library launches and supervises `signal-cli` in JSON-RPC mode over stdin/stdout, correlates requests/responses, and surfaces incoming events through **two parallel surfaces**: `IObservable<T>` (Rx, for fan-out/broadcast) and `IAsyncEnumerable<T>` (Channels, default for `await foreach`).

- Target framework: **net10.0 (LTS)**, language **C# 14**. Package version **4.0.3**.
- Requires **JDK 25+** (signal-cli 0.14.3's `Main` is class-file version 69.0 = Java 25) and **signal-cli 0.14.3** (downloaded by the `SignalCli.Runtime` package at build time). Java is **not** required with the native package (`SignalCli.Runtime.Native`, Linux x64) or the bundled-JRE packages (`SignalCli.Runtime.Jre.win-x64`, `SignalCli.Runtime.Jre.osx-arm64`).

## Build & test

```bash
dotnet build SignalCli.sln                                  # build all
dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj    # run tests (287 tests)
dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj --collect:"XPlat Code Coverage"  # coverage
```

- The `SignalCli.runtime` project downloads signal-cli on first build (network required); subsequent builds are skipped via an MSBuild `Exists` gate. The `SignalCli.runtime.native` and `SignalCli.runtime.jre.*` projects similarly download their payloads (native binary / Temurin JRE), so a clean `dotnet build SignalCli.sln` pulls several hundred MB once. To iterate quickly on the library, build/test `src/SignalCli` + `Tests/SignalCli.Tests` directly.
- Prefer running tests after every meaningful change; the hosted-service/health-monitor suites are the safety net for process-lifecycle changes.
- Test suite is **wall-clock-independent**: `SignalCliHealthMonitor/` and `SignalCliHostedService/Restart*/` tests use `FakeTimeProvider` exclusively (never `Task.Delay(>10ms)`). If you add a test that depends on real time, you have introduced flake — use `FakeTimeProvider.Advance(...)` instead.

### Restoring packages in a sandboxed env (Claude Code on the web)

The repo's `NuGet.Config` has a `<packageSourceMapping>` that points `SignalCli.*` packages at a GitHub-hosted feed which requires auth. In a fresh remote-execution container, that feed is unreachable. Use this restore flag instead of plain `dotnet restore`:

```bash
dotnet restore <project> --source https://api.nuget.org/v3/index.json -p:NuGetAudit=false
```

`--source` overrides the GitHub feed; `-p:NuGetAudit=false` skips the (also unreachable) vulnerability scanner. Once restored, `dotnet build/test --no-restore` works normally. The `dotnet` SDK itself is `apt`-installable from `packages.microsoft.com` (which is allowlisted in our container policy).

## Architecture (key types)

- `SignalCliHostedService` — launches/stops/restarts signal-cli; implements `IStreamPairProvider`. Takes `IOptions<SignalCliOptions>` + optional `TimeProvider`.
- `ProcessStateManager` — process state machine (`ProcessState` enum + `ProcessStateInfo`); single source of truth.
- `SignalCliHealthMonitor` — `BackgroundService` using `PeriodicTimer(interval, TimeProvider)`; pings `version`; force-restarts on failure.
- `JsonRpcClient` / `JsonRpcClientHostedService` — JSON-RPC transport; request/response correlation via `id` + `TaskCompletionSource`; notifications via `IObservable`. `JsonRpcClient` is **`IAsyncDisposable`-only**.
- `SignalEventService` — fans `receive` notifications to both `IObservable<T>` (`TextMessages`/`Attachments`/…) and `IAsyncEnumerable<T>` (`TextMessagesAsync(ct)`/`AttachmentsAsync(ct)`/…) for each of 10 event kinds.
- `SignalMessage` / `SignalService` / `SignalAccounts` / `SignalDevices` / `SignalGroups` — the Signal API surface. **None implement `IDisposable`** (stateless facades).
- `SignalCliOptions` + `SignalCliOptionsValidator` (source-gen `[OptionsValidator]`) — typed configuration with `[Required]`/`[Range]` DataAnnotations validated on host start. Legacy `Config` exists as `[Obsolete]` shim.
- `Logging/*Log.cs` — one `internal static partial class` per service with `[LoggerMessage]`-generated methods (~109 of them). EventId blocks are reserved per service; see `.claude/rules/patterns.md` § Logging.
- DI composition root: `Extensions/ServiceCollectionExtensions.cs` (`AddSignalCli(Action<SignalCliOptions>?)` is the modern overload; `AddSignalCli(Action<Config>?)` is the legacy shim; `AddSignalEvents()` is separate).

Patterns in use: Dependency Injection, Options pattern (`IOptions<TOptions>` + source-gen validation), Hosted Services / BackgroundService, Factory, Adapter/Wrapper (`IProcess`), Builder (`*Options.Builder`), Provider, Observer/Rx + async streams via `Channel<T>`, Facade, State Machine, Watchdog, Source-generated logging.

## Topic-scoped rules

Path-scoped agent instructions live in `.claude/rules/` (load conditionally when Claude edits matching files). Refer to them for the full pattern reference; this root file keeps only the always-relevant core.

- [`.claude/rules/signal-cli-protocol.md`](.claude/rules/signal-cli-protocol.md) — 7 upstream signal-cli protocol facts with `file:line @ bda4e7f` citations *(loads when editing `src/SignalCli/Services/**`, `src/SignalCli.runtime*/**`)*.
- [`.claude/rules/conventions.md`](.claude/rules/conventions.md) — modern C# / naming / namespace hierarchy / DTO + event-args + test-class naming / comments-in-Ukrainian *(loads when editing `src/**`, `Tests/**`, `Example/**`)*.
- [`.claude/rules/patterns.md`](.claude/rules/patterns.md) — Established patterns: async/cancellation, configuration (`IOptions<SignalCliOptions>`), logging (`[LoggerMessage]`), DI registration, background loops + time, event streams, disposal, exception derivation, AOT readiness, observability *(loads when editing `src/SignalCli/**`, `src/SignalCli.HealthChecks/**`)*.
- [`.claude/rules/csproj-build.md`](.claude/rules/csproj-build.md) — csproj/MSBuild conventions + mass-edit safety + GitHub Actions supply-chain *(loads when editing `*.csproj`, `Directory.Build.props`, `.github/workflows/**`, `src/build/**`)*.
- [`.claude/rules/testing.md`](.claude/rules/testing.md) — FakeTimeProvider / regression-guard test patterns / xUnit1031 hygiene / MeterListener thread-safety *(loads when editing `Tests/**`)*.
- [`.claude/rules/obsolete-shims.md`](.claude/rules/obsolete-shims.md) — backward-compatibility convention (one-major-grace, doc-sync invariant, three-site duplication trap) *(loads when editing `src/SignalCli/**`)*.
- [`.claude/rules/audit-debt.md`](.claude/rules/audit-debt.md) — Future development guardrails + How we discovered prevention checklist + Working style *(always-load — cross-cutting)*.
- [`.claude/rules/openspec-workflow.md`](.claude/rules/openspec-workflow.md) — OpenSpec planning + post-merge archive + CHANGELOG voice template + README voice + drift rules *(loads when editing `openspec/**`, `CHANGELOG.md`, `CLAUDE.md`)*.
- [`.claude/rules/cloud-dev.md`](.claude/rules/cloud-dev.md) — Cloud Code on Web setup + SessionStart hook *(loads when editing `.claude/**`, `docs/cloud-development.md`)*.

## Critical rules (do not regress — these are audit findings + post-2.1.0 invariants)

1. **Privacy:** never log message bodies, phone numbers, or attachment payloads above `Trace`. RPC params/results and raw stdin/stdout lines are `Trace`-only. `SignalService` logs the method name only. `[LoggerMessage]` templates at `Information+` MUST NOT reference PII fields. **The same prohibition applies to `Activity` tag values and `Meter` tag values** (observability surface from `post-modernize-tuning` §11 — shipped): only method names, status enums, integer ids, durations, exception type names — never message contents / phones / file paths. Privacy guard tests (`ObservabilityPrivacyTests` — single fixture covering both `ActivityListener` and `MeterListener` capture paths) enforce this with literal-substring asserts on a seed phone, seed message body, and seed file path; `MeterTagValues_AreOnlyKnownEnumLiterals` also pins the canonical tag-key set (`method`, `status`, `trigger`, `event_type`), so any new tag key spawned without test-fixture update fails loudly.
2. **Process arguments:** build the signal-cli command via `ProcessConfig.ArgumentList` (each arg separate). Never go back to a single interpolated `Arguments` string with quoted paths.
3. **Attachments:** sanitize `FileName` with `Path.GetFileName` (see `AttachmentEntry.SafeFileName`) before writing temp files or building data URIs — guard against path traversal.
4. **Event dispatch:** in `SignalEventService`, a `DataMessage` is a *presence-based union*; emit every applicable observable AND its paired async-channel (text + attachment can both fire). Do not reintroduce early `return` between payload checks.
5. **Text styles:** use `ToUpperInvariant()` for style names (locale-independent).
6. **Serialization:** `System.Text.Json` **only** — `Newtonsoft.Json` is removed. Annotate model members with `[JsonPropertyName]` (never `[JsonProperty]`). Register every new serializable root type in the source-generated context `Serialization/SignalJsonContext.cs` — **source-gen-only**: reflection fallback removed in `post-modernize-tuning` §6.4 (raund 14). Every type passed to `JsonSerializer.Serialize`/`Deserialize`/`SerializeToElement` from `src/SignalCli/**` MUST be in `SignalJsonContext`, or you get `NotSupportedException: Metadata for type ... was not provided` on runtime — `JsonContextRegistrationTests` (§6.12) catches the omission. Production code uses `JsonTypeInfo<T>`-based overloads (AOT-safe); test-only `SignalJson.OptionsForTests` carries the reflection-fallback resolver for anonymous-type test payloads (annotated `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`). `JsonRpcRequest.Params` / `JsonRpcResponse.Result` are `JsonElement` (registered in context for typed deserialization downstream).
7. **Download scripts:** `src/SignalCli.runtime/download-signal-cli.*` and `src/build/download-jre.*` verify the archive SHA-256 before extraction. **The canonical version + hash live in the runtime csproj** (`<SignalCliVersion>`/`<SignalCliSha256>` у `SignalCli.runtime.csproj`; `<JreVersion>`/`<JreSha256>` у обох `SignalCli.runtime.jre.*.csproj`); the scripts read those values as arguments. Bumping a pinned version = single-csproj edit, no script edits. The `.ps1` files are deliberately **ASCII-only** (no Cyrillic/emoji) so they parse under Windows PowerShell 5.1 **without** needing a UTF-8 BOM — keep them ASCII. They also invoke the Windows system `tar` (`%SystemRoot%\System32\tar.exe`) explicitly and stage extraction through an ASCII temp dir, because Git's GNU `tar` mis-reads `C:\…` paths and bsdtar fails on non-ASCII target paths.
8. **Bundled-JRE packages** (`SignalCli.Runtime.Jre.win-x64`, `SignalCli.Runtime.Jre.osx-arm64`): bundle a SHA-256-pinned Eclipse Temurin **25** JRE + signal-cli. The JRE and jars are packed as **single `.zip` files** and extracted by the consumer `.targets` via MSBuild's built-in `<Unzip>` — **do not** pack the JRE as individual files: NuGet treats an extension-less `PackagePath` (e.g. the JRE's `lib/modules`) as a *directory* and corrupts the layout, which crashes the JVM at bootstrap. `Config.ResolveBundledJava` auto-discovers `<output>/jre/bin/java[.exe]`, so consumers need no system Java and should **not** set `Config.JavaExecutable`.
9. **No sync-over-async in disposal.** `IJsonRpcClient` is `IAsyncDisposable`-only; never re-introduce a `Dispose()` that does `DisposeAsync().AsTask().GetAwaiter().GetResult()`. DI containers and `await using` are the supported paths.
10. **Fail-fast configuration.** `SignalCliOptions` validation (DataAnnotations + custom rule + `[OptionsValidator]` source-gen) is wired into `AddSignalCli`. Internal services read `_options.Value` in the constructor — that's deliberately where validation fires. Don't bypass it (e.g. don't capture `IOptions<>` and read `.Value` lazily in some method later).
11. **No wall-clock in tests.** Tests in the lifecycle/health-monitor suites must drive time via `FakeTimeProvider`. Re-introducing `await Task.Delay(>10ms)` to a test there is a regression.
12. **Options validation has exactly one path: source-gen `[OptionsValidator]`.** `ServiceCollectionExtensions.ConfigureOptions` registers `SignalCliOptionsValidator` (source-gen, reflection-free, AOT-safe) via `TryAddEnumerable<IValidateOptions<SignalCliOptions>>`. Cross-field rules go in `.Validate(o => …, "msg")` lambdas. **Do not re-add `.ValidateDataAnnotations()`** alongside the `[OptionsValidator]`: it duplicates the same `[Required]`/`[Range]` checks through reflection and is the reason `<IsAotCompatible>true</IsAotCompatible>` still trips IL2026 warnings. `post-modernize-tuning` §8b.8 removes the redundant call; do not bring it back.
13. **Source-gen JSON has no reflection fallback.** Every type passed to `JsonSerializer.Serialize`/`Deserialize`/`SerializeToElement` from `src/SignalCli/**` MUST be registered in `Serialization/SignalJsonContext.cs`. The `JsonContextRegistrationTests` suite (added in `post-modernize-tuning` §6.12) reflectively enumerates `InvokeMethodAsync<TRequest,TResponse>` call sites and asserts each type pair is in the context — if your new RPC method adds a DTO that's not in the context, this test fails loudly instead of producing silent `"{}"` payloads at runtime.
14. **Typed/idempotent state errors.** `SignalEventService.SubscribeAsync` is **idempotent** (post-`subscription-race-safety` §3.7): re-subscribing the same account returns the existing `subscriptionId` instead of throwing a generic `InvalidOperationException` with a locale-dependent Ukrainian message. Argument null/empty checks throw `ArgumentException` (via `ArgumentException.ThrowIfNullOrEmpty`) with the correct `paramName`. When you add new state-error sites elsewhere, mirror this: prefer idempotency over throwing; if you must throw, prefer a derived typed exception (or `ObjectDisposedException`/`ArgumentException` subclasses) over a generic `InvalidOperationException` so callers can pattern-match without inspecting the message text.
15. **AOT-safe JsonSerializer overloads only in production.** `<IsAotCompatible>true</IsAotCompatible>` is enabled on `src/SignalCli/SignalCli.csproj`. Every `JsonSerializer.Serialize`/`Deserialize`/`SerializeToElement` call in `src/SignalCli/**` MUST use the `JsonTypeInfo<T>`-taking overload, NOT the generic `<T>(_, options)` overload (which is reflection-based and trips IL2026/IL3050). `ISignalCliClient.InvokeMethodAsync<TRequest, TResponse>` requires `JsonTypeInfo<TRequest>` + `JsonTypeInfo<TResponse>` as explicit parameters — consumers pass them from `SignalJsonContext.Default.*`. The only production exception is `AddSignalCli(IConfiguration)` (annotated `[RequiresUnreferencedCode]`+`[RequiresDynamicCode]` because `Bind` uses reflection — AOT-targeting consumers must use `AddSignalCli(Action<SignalCliOptions>?)` instead).
16. **Integration E2E tests use legacy `Action<Config>` overload.** `Tests/SignalCli.Tests.Integration/SignalCliE2EVersionTests.cs` calls `services.AddSignalCli((Config cfg) => …)` inside `#pragma warning disable CS0618` because the legacy flow runs `Config.CreateDefault()` first — which auto-resolves the bundled-JRE path on Windows/macOS (`Config.ResolveBundledJava`) AND sets `LibDirectory = "SignalCli/lib"` (default) which satisfies `[Required(AllowEmptyStrings = false)]` on `SignalCliOptions`. The `Action<SignalCliOptions>?` overload skips both, so the test would fail with `OptionsValidationException`. **Do not "modernize" the Integration test off the legacy overload** until either (a) Config-shim is fully removed in 4.0, or (b) auto-resolve logic is migrated into the SignalCliOptions-overload path.
17. **`InternalsVisibleTo` is the seam for source-gen context in tests.** `SignalJsonContext` is `internal` to keep the source-gen layer hidden from consumers (they pass `JsonTypeInfo<T>` from their own contexts if they need custom). Both `Tests/SignalCli.Tests` and `Tests/SignalCli.Tests.Integration` have `InternalsVisibleTo` to access `SignalJsonContext.Default.*` for AOT-safe `InvokeMethodAsync` calls. **Do not make `SignalJsonContext` public** as a workaround for new test access — add the test project to `InternalsVisibleTo` in `src/SignalCli/SignalCli.csproj`.
18. **JSON deserialization hardening — dual-site enforcement.** Both production JSON layers SHALL reject duplicate JSON keys with `JsonException` (never silently follow last-wins semantics): (a) `SignalJson.Options.AllowDuplicateProperties = false` (runtime flag, covers any reflection-based call-site like `OptionsForTests`; lands in `audit-followup-2026 §json-hardening`); (b) `[JsonSourceGenerationOptions(AllowDuplicateProperties = false)]` on `SignalJsonContext` (source-gen attribute, covers every `SignalJsonContext.Default.X` call-site — this is what `JsonRpcClient.ProcessMessageAsync` and the rest of production actually use; lands in `json-hardening-source-gen-attribute`). **Both layers are required because they cover orthogonal code paths** — the runtime flag is dead-flag for source-gen Default fast-path call-sites; the source-gen attribute is the binding contract that propagates into the generated `Utf8JsonReader` loop. Removing either layer silently weakens defense-in-depth. Pinned by `RG05` ×3 facts in `JsonSerializationTests.cs`. We deliberately do NOT enable `JsonSerializerOptions.Strict`-preset because it implies `JsonUnmappedMemberHandling.Disallow`, which is incompatible with signal-cli's habit of adding new envelope fields between versions (forward-compat).

## Audit baseline — invariants that MUST NOT regress

Цей список — мінімальна планка якості зафіксована після аудиту v2.1 (2026-05-24).
Будь-який PR що порушує хоча б один з цих пунктів МУСИТЬ бути відхилений або
супроводжуватись явним обґрунтуванням у CHANGELOG.

### Тестова база

- Unit tests: **≥ 290** (поточна планка після `claude-md-rules-restructure` landing RG08 +3 facts).
- E2E tests: **≥ 2** (bundled-JRE, не потребує live Signal account). Друга — `SignalCliE2EParallelRpcCorrelationTests.Process_ParallelVersionCalls_AllResolveToCorrectResponseById` — пінує `.claude/rules/signal-cli-protocol.md` §3 (parallel request correlation by `id`) проти реального virtual-thread-dispatcher'а.
- `dotnet build` з `TreatWarningsAsErrors=true` — **обидва** проекти (`src/SignalCli`, `Tests/SignalCli.Tests`); Integration слідує тому ж шляху коли стане доцільним.
- Нуль `xUnit1031` violations (DoNotUseBlockingTaskOperationsInTestMethod). Якщо новий тест вимагає sync-blocking — додай `[SuppressMessage("xUnit", "xUnit1031", Justification="…")]` із поясненням, інакше build впаде.

### Regression guards — ВСІ мають бути зеленими

| Guard | Файл | Що pins |
|-------|------|---------|
| R01 | `JsonContextRegistrationTests.cs` | Кожен `*Parameters`/`*Response` DTO зареєстрований у `SignalJsonContext` |
| R02 | `RegressionGuards/EventIdBlockTests.cs` | `[LoggerMessage(EventId=…)]` лежить у блоці свого `*Log.cs` класу |
| R03 | `RegressionGuards/PublicApiSurfaceTests.cs` | Public API surface не змінюється без оновлення `SignalCli.public-api.txt` baseline |
| R04 | `RegressionGuards/ObsoleteMessageConsistencyTests.cs` | `[Obsolete("...will be removed in N.0")]` посилається на N строго > поточного major |
| RG05 | `JsonSerializationTests.cs` (3 facts: runtime flag + `JsonDocument` proxy + source-gen `SignalJsonContext` path) | Dual-site `AllowDuplicateProperties = false` enforcement on BOTH `SignalJson.Options` AND `[JsonSourceGenerationOptions]` (Critical rule #18) |
| RG06 | `RegressionGuards/EventApiSymmetryTests.cs` | Кожен `IObservable<T>` на `ISignalEventService` має парний `IAsyncEnumerable<T>` метод |
| RG07 | `RegressionGuards/VersionLockstepTests.cs` | `SignalCli.NET.HealthChecks` assembly version == `SignalCli.NET` |
| RG08 | `RegressionGuards/ClaudeMdSplitConsistencyTests.cs` | Root CLAUDE.md ≤ 200 lines + every `.claude/rules/<topic>.md` has valid frontmatter or always-load marker + numeric "Critical rule #N" anchors live only in root |
| RG09 | `RegressionGuards/DocsApiCoverageTests.cs` | Кожен публічний метод на 8 `ISignal*` facade-інтерфейсах + `ISignalCliClient` + 4 `ServiceCollectionExtensions` extension'и згадується хоча б у одному файлі під `docs/api/*.md`. Запобігає drift'у docs vs public API surface |

### Архітектурні інваріанти

- Жоден `JsonSerializer.Serialize/Deserialize/SerializeToElement` у `src/SignalCli/**` без `JsonTypeInfo<T>` overload (rule #15).
- Жоден `_logger.LogXxx("template", arg)` — тільки `[LoggerMessage]`-generated `partial` методи (CA1848/CA1873 green).
- Жоден `new CancellationTokenSource(TimeSpan)` у класі що ін'єктить `TimeProvider` — тільки overload `(TimeSpan, TimeProvider)` (`.claude/rules/patterns.md` § Background loops + time).
- Жоден `Task.Delay(>10ms)` у тестах із `SignalCliHealthMonitor/` чи `SignalCliHostedService/Restart*/` — тільки `FakeTimeProvider.Advance` (rule #11).
- `<SignalCliPackageVersion>` живе **ТІЛЬКИ** у `Directory.Build.props` — не хардкодити `<Version>` у `SignalCli.csproj` або `SignalCli.HealthChecks.csproj`.
- Root CLAUDE.md ≤ 200 lines; topic-scoped rules under `.claude/rules/*.md` (RG08).

### Версійна синхронізація

`SignalCli.NET` і `SignalCli.NET.HealthChecks` ЗАВЖДИ мають однакову версію. Адаптер бінарно прив'язаний до main lib через `[InternalsVisibleTo("SignalCli.HealthChecks")]` — divergent versions = `MissingMethodException` на першому health-check-probe в продакшені. Єдине місце де версія визначається: `Directory.Build.props → <SignalCliPackageVersion>`. Enforced: `VersionLockstepTests.MainLibAndHealthChecksAdapter_ShareExactSameAssemblyVersion`.

## Version-CHANGELOG lockstep

**Кожен bump `<SignalCliPackageVersion>` у `Directory.Build.props` МУСИТЬ супроводжуватись відповідною `## [X.Y.Z] — YYYY-MM-DD` секцією у `CHANGELOG.md` — у **тому самому коміті**.** Без винятків. Включно з patch-bumps, test-only fix-релізами, doc-sync-патчами. Якщо тобі нема що написати — релізу не повинно бути; не bump'ай version "про запас".

Структура секції CHANGELOG, voice template, bad-vs-good приклади — `.claude/rules/openspec-workflow.md` § CHANGELOG voice template (loads automatically when editing `CHANGELOG.md`).

Перевірка при PR: `git diff <base>..HEAD -- Directory.Build.props CHANGELOG.md` — якщо одне змінилось без іншого, перевір чому. (Reflection-based regression guard для цього неможливий — CHANGELOG.md не доступний з runtime-assembly; це enforce'ується процесом review, не build-failure.)

## Implemented, merged, archived

Historical reference (do not re-open — all in `openspec/changes/archive/<date>-*/`):
- `address-audit-findings` — privacy/security/correctness audit round 1.
- `modernize-architecture` — `net9.0` → `net10.0`, `Newtonsoft.Json` → `System.Text.Json` (+ source-gen `JsonSerializerContext`), single-source-of-truth process state via `ProcessStateManager`.
- `agent-ready-conventions` — `.editorconfig`, analyzers, narrowed broad `catch`-es, this `CLAUDE.md`.
- `address-audit-findings-2` — audit round 2: bounded RPC timeout, windowed restart budget, idempotent `AddSignalCli`, `IAsyncDisposable` on `JsonRpcClient`, integration tests + bundled-JRE E2E.
- `comprehensive-code-audit` — the audit document itself; fixes live in the two `address-audit-findings*` changes.
- `agent-friendly-modernization` (**2.1.0**) — 5 capabilities: `agent-friendly-api`, `background-monitor`, `source-generated-logging`, `options-pattern`, `async-stream-events`.
- `post-modernize-tuning` (**3.0.0**) — 14 capabilities including AOT readiness, observability (`ActivitySource`/`Meter` + `SignalCli.NET.HealthChecks` package), RPC back-pressure, state-machine thread-safety, subscription race-safety, hosting modernization, options-validation tightening, supply-chain hardening, v3.0 breaking-API wave.
- `signal-cli-protocol-alignment` (**4.0.0**) — pinned 7 upstream signal-cli protocol facts with file:line citations; `graceful-shutdown-fix` (stdin EOF, not "exit"); typed RPC errors (`RateLimitException`/`UntrustedIdentityException`); attachment threshold lowered 15M → 12M.
- `audit-followup-2026` (**4.0.0**) — 9 capabilities: doc-sync, JSON hardening, configuration-binder-AOT, regression guards (R04 obsolete + R02 EventId + R03 public-api), integration tests expansion, edge-case coverage, AddSignalCli idempotency fix, badge URL fix, low-priority polish.
- `deprecated-shim-removal` (**4.0.0**) — BREAKING: deleted `Config` + 6 `[Obsolete]` shims. Migration table in CHANGELOG.
- `json-hardening-source-gen-attribute` (**4.0.2**) — closes Critical rule #18 dead-flag prog with `[JsonSourceGenerationOptions(AllowDuplicateProperties = false)]` on the source-gen context.
- `e2e-coverage-expansion` (**4.0.2**) — `SignalCliE2EParallelRpcCorrelationTests` pins protocol fact §3 (parallel correlation by `id`) against real upstream.
- `claude-md-pattern-additions` (**4.0.3**, archived 2026-05-25) — 4 doc additions: DI registration idioms (TryAddSingleton vs AddSingleton, one-instance-two-roles, sentinel-marker idempotency), namespace hierarchy + DTO/EventArgs/test-class naming, exception derivation heuristic, README voice + drift rules.
- `claude-md-rules-restructure` (**4.0.3**, archived 2026-05-25) — Anthropic-aligned split: root CLAUDE.md 592 → 150 lines + 9 path-scoped topic files under `.claude/rules/` with `paths:` frontmatter. New RG08 (`ClaudeMdSplitConsistencyTests`) pins split shape (root size cap, frontmatter validity, numeric `Critical rule #N` anchors only in root).
- `signal-cli-api-coverage` (**4.1.0 → 4.9.0**, archived 2026-05-25) — 9 capabilities raising JSON-RPC method coverage 18% → 100% (9 → 54 methods) across 9 release waves: `messaging-interactive` (reactions/receipts/typing/remoteDelete), `groups-crud` (join/update/quit), `contacts-identity` (listContacts/listIdentities/trust*/updateContact/updateProfile/removeContact/block/unblock), `sticker-packs` + `binary-resource-fetch` (getAttachment/getAvatar/getSticker), `device-management` (addDevice/listDevices/removeDevice/updateDevice), `account-lifecycle` (updateAccount/updateConfiguration/setPin/removePin/unregister/deleteLocalAccountData/startChangeNumber/finishChangeNumber — destructive ops behind `SignalCliOptions.EnableDestructiveOperations` opt-in flag), `polls` + `messaging-power-user` (send-side polls/payment-notification/messageRequestResponse/pinMessage), `utility-rpc` (getUserStatus/submitRateLimitChallenge/sendContacts), and `polls-receive-decoders` (paired `IObservable<T>` + `IAsyncEnumerable<T>` for 7 new event kinds — `PollCreates`/`PollVotes`/`PollTerminates`/`Payments`/`PinMessages`/`UnpinMessages`/`AdminDeletes` — bringing event-pair count 10 → 17). Introduced §0.5 anti-hallucination protocol: every public service method's XMLDoc cites `org.asamk.signal.commands/<X>Command.java @ bda4e7fc` source path + commit SHA, and every DTO XMLDoc cites matching `org.asamk.signal.json.Json*` record. ⚠ **BREAKING in 4.9.0:** `JsonPayment` wire shape corrected from speculative `(Amount: decimal, Currency: string?)` to actual upstream `(Note: string?, Receipt: byte[])` — the old shape was never wire-validated against real signal-cli payment notifications. Test count 290 → 503 unit tests + 4 new env-gated E2E tests (`TestAccountFixture` via `SIGNALCLI_TEST_ACCOUNT`).
- `docs-api-reference` (archived 2026-05-27, see `openspec/changes/archive/2026-05-27-docs-api-reference/`) — 2 capabilities. **`docs-api-reference`**: 8 per-category `docs/api/*.md` files з middle-depth per-method documentation (signature + опис + signal-cli source citation `<X>Command.java @ bda4e7fc` + приклад + типізовані винятки) для всіх 54 публічних RPC методів на 9 інтерфейсах + 4 `ServiceCollectionExtensions` extension'и. Worker-example + device-link flow переміщено з README у `docs/examples/worker-auto-reply.md`. README скорочено 586 → 398 рядків (також refresh "API-можливості" таблиці на 4.9.0 coverage + remove застарілої 3.x→4.0 migration section — CHANGELOG `[4.0.0]` залишається authoritative). **`docs-coverage-guard`**: new RG09 (`DocsApiCoverageTests`) reflectively asserts кожен публічний метод згаданий у хоча б одному `docs/api/*.md` через `\b`-anchored word-boundary regex match (substring-match був занадто лояльним і пропускав rename'и типу `Foo` → `FooBar`). Test count 503 → 504.
- `api-coverage-audit-followup` (**4.10.0**, archived 2026-05-27, see `openspec/changes/archive/2026-05-27-api-coverage-audit-followup/`) — 5 capabilities з post-merge code-review після `signal-cli-api-coverage` epic. ⚠ **`identity-changed-deprecation`**: `IdentityChangedException` marked `[Obsolete(DiagnosticId="SIGNALCLI001")]` — type was speculative-split (never dispatched) бо upstream `SendMessageResultUtils.java:60 @ bda4e7fc` throws ЄДИНУ fixed-string помилку `"Failed to send message due to untrusted identities"` без first-contact vs re-install distinguisher; видаляється у 5.0. ⚠ **`json-payment-receipt-nullable`**: `JsonPayment.Receipt: byte[]` → `byte[]?` (NRT honesty fix — upstream Java has no NRT enforcement, `"receipt": null` AND missing-field cases deliver `null`). **`event-dispatch-refactor`**: 13 dispatch branches у `SignalEventService.OnNotificationReceived` → `DispatchUnionMember<TPayload, TArgs>` generic helper; `OnNotificationReceived` 213 → 138 рядків; presence-based union semantics (CLAUDE.md rule #4) preserved. **`protocol-checklist-amend`**: pinned fact #8 added to `.claude/rules/signal-cli-protocol.md` (closure для IdentityChanged deprecation); version-bump checklist розширено re-grep `"admin"`-substring stability; `audit-debt.md` § new "§0.5 cite-and-read, not cite-and-trust" working-style bullet (lesson з Wave-1 finding). **`captcha-dispatch-test`**: NO-OP — test уже існував з `8825b22 feat(4.1.0)`; proposal's claim of "absence" виявилось cite-and-trust error (the very pattern §0.5 warns against — author writing the §0.5 lesson committed the same error in finding #4 of the same proposal). Test count 503 → 506 (2 new JsonPayment nullable serialization tests).

**Pending changes:** _(none)_ — `openspec/changes/` має лише `archive/` subdirectory; start a new change to add work.

## Git

Work on a feature branch; do not push or commit unless asked. When commits are requested, prefer one commit per OpenSpec capability (see `.claude/rules/audit-debt.md` § Working style). Never amend already-pushed commits without explicit approval.
