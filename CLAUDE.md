# CLAUDE.md

Guidance for AI coding agents (Claude Code, Copilot, etc.) working in this repository.

## Project

**SignalCli.NET** — a .NET wrapper around [`signal-cli`](https://github.com/AsamK/signal-cli) (a Java app) that exposes a typed, reactive API for the Signal messenger. The library launches and supervises `signal-cli` in JSON-RPC mode over stdin/stdout, correlates requests/responses, and surfaces incoming events through `System.Reactive` observables.

- Target framework: **net10.0 (LTS)**, language **C# 14**. (The net9.0→net10.0 migration is done.)
- Requires **JDK 25+** (signal-cli 0.14.3's `Main` is class-file version 69.0 = Java 25) and **signal-cli 0.14.3** (downloaded by the `SignalCli.Runtime` package at build time). Java is **not** required with the native package (`SignalCli.Runtime.Native`, Linux x64) or the bundled-JRE packages (`SignalCli.Runtime.Jre.win-x64`, `SignalCli.Runtime.Jre.osx-arm64`).

## Build & test

```bash
dotnet build SignalCli.sln                                  # build all
dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj    # run tests (152 tests)
dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj --collect:"XPlat Code Coverage"  # coverage
```

- The `SignalCli.runtime` project downloads signal-cli on first build (network required); subsequent builds are skipped via an MSBuild `Exists` gate. The `SignalCli.runtime.native` and `SignalCli.runtime.jre.*` projects similarly download their payloads (native binary / Temurin JRE), so a clean `dotnet build SignalCli.sln` pulls several hundred MB once. To iterate quickly on the library, build/test `src/SignalCli` + `Tests/SignalCli.Tests` directly.
- Prefer running tests after every meaningful change; the hosted-service/health-monitor suites are the safety net for process-lifecycle changes.

## Architecture (key types)

- `SignalCliHostedService` — launches/stops/restarts signal-cli; implements `IStreamPairProvider`.
- `ProcessStateManager` — process state machine (`ProcessState` enum + `ProcessStateInfo`).
- `SignalCliHealthMonitor` — pings `version`; force-restarts on failure.
- `JsonRpcClient` / `JsonRpcClientHostedService` — JSON-RPC transport; request/response correlation via `id` + `TaskCompletionSource`; notifications via `IObservable`.
- `SignalEventService` — fans `receive` notifications out to `TextMessages`/`Attachments`/`Reaction`/… observables.
- `SignalMessage` / `SignalService` / `SignalAccounts` / `SignalDevices` / `SignalGroups` — the Signal API surface.
- DI composition root: `Extensions/ServiceCollectionExtensions.cs` (`AddSignalCli`, `AddSignalEvents`).

Patterns in use: Dependency Injection, Hosted Services, Factory, Adapter/Wrapper (`IProcess`), Builder (`*Options.Builder`), Provider, Observer/Rx, Facade, State Machine, Watchdog.

## Conventions (match the existing code)

- Modern C#: file-scoped namespaces, primary constructors, records for DTOs, `required`/collection expressions where natural, `Func<>`/`Action<>` over custom delegates.
- `var` only when the type is obvious; explicit type in `foreach`.
- `string`/`int` keywords, not `String`/`Int32`.
- `_camelCase` private fields, PascalCase public, `I`-prefixed interfaces.
- Always `.ConfigureAwait(false)` in library code.
- **Exceptions:** throw and catch *specific* types. A broad `catch (Exception)` is allowed **only** at long-running boundaries (the stdout reader loop, the health-monitor loop, the notification dispatcher) where one bad item must not kill the loop — and such catches must log and continue. Do not swallow exceptions silently elsewhere.
- **Comments and log messages are written in Ukrainian** in this codebase — match that when editing existing files.
- Keep XML doc comments on public members.

### For new code (forward-looking, tracked by `agent-friendly-modernization`)

These patterns apply to *new* code and to non-trivial edits in touched files. They make the library more discoverable for both humans and LLM agents (typed DI, real `Async` signatures, structured logs). The full migration of existing code is staged across the `agent-friendly-modernization` OpenSpec change — but do **not** add new code that re-introduces the old shape.

- **Async naming:** every new `Task`/`ValueTask`-returning method gets the `Async` suffix. The single historical exception, `ISignalCliClient.Version()`, is being renamed to `VersionAsync()` with an `[Obsolete]` shim — do not copy that anti-pattern.
- **CancellationToken:** new public methods expose `CancellationToken cancellationToken = default` as the **last explicit parameter**, even when an `*Options` record also carries one (link both via `CreateLinkedTokenSource` if needed). Agents look at signatures, not at options fields.
- **Logging:** new `ILogger` callsites go through `[LoggerMessage]` `partial` methods in a sibling `XxxLog.cs` file (closes CA1848/CA1873). Direct `_logger.LogInformation("template", args)` is being phased out; do not add new such calls. Privacy rules (#1 in Critical rules) still apply — never reference PII in templates at `Information+`. EventId blocks are reserved per service in `openspec/changes/agent-friendly-modernization/design.md`.
- **Background loops:** any new periodic worker is a `BackgroundService` whose `ExecuteAsync` uses `PeriodicTimer(interval, TimeProvider)` — not `Task.Run` + `while (!ct.IsCancellationRequested) { await Task.Delay(...) }`. Inject `TimeProvider` (default `TimeProvider.System`) so `FakeTimeProvider` drives tests; tests under `SignalCliHealthMonitor/` and `SignalCliHostedService/Restart*/` must not do `Task.Delay(>10ms)`.
- **Configurable knobs:** new settings go into `SignalCliOptions` (typed `IOptions<>`) with `[Required]` / `[Range]` data annotations and `.ValidateOnStart()`. The public `Config` is being replaced; do not extend it with new fields. Internal services read `_options.Value` once in the constructor (options are immutable).
- **Event streams:** new event channels should be exposed as `IAsyncEnumerable<T>` on top of a bounded `Channel<T>` (`FullMode = DropOldest`, capacity 1024) — `await foreach` is the default ergonomic for both humans and LLM agents. Pair with the existing `IObservable<T>` only when broadcast/fan-out is a documented requirement; in XMLDoc, state single-consumer semantics explicitly.
- **Disposal:** classes with asynchronous cleanup implement **only** `IAsyncDisposable` — no synchronous `Dispose()` that blocks via `GetAwaiter().GetResult()`. Stateless façades (`SignalAccounts`, `SignalGroups`, `SignalDevices`, `SignalService`, `SignalMessage`) should not implement `IDisposable` at all.
- **TaskCompletionSource cancellation:** pass the originating token to `TrySetCanceled(token)` so callers see the actual cancellation source via `OperationCanceledException.CancellationToken`.
- **`TimeProvider` consistency:** if a class already takes `TimeProvider`, every wait it performs goes through it (`Task.Delay(_, _, TimeProvider, ct)`, `new CancellationTokenSource(timeout, TimeProvider)`, `TimeProvider.CreateTimer(...)`). Do not mix real and virtual clocks in the same class.
- **Strong typing over magic strings:** prefer `enum` (e.g. `TextStyleMode`) over `string? mode = "styled"`-flags in new code; case-insensitive string compares for protocol values use `StringComparison.OrdinalIgnoreCase` (and `ToUpperInvariant()` only when the value leaves the process boundary — see #5).

## Critical rules (do not regress — these are audit findings)

1. **Privacy:** never log message bodies, phone numbers, or attachment payloads above `Trace`. RPC params/results and raw stdin/stdout lines are `Trace`-only. `SignalService` logs the method name only.
2. **Process arguments:** build the signal-cli command via `ProcessConfig.ArgumentList` (each arg separate). Never go back to a single interpolated `Arguments` string with quoted paths.
3. **Attachments:** sanitize `FileName` with `Path.GetFileName` (see `AttachmentEntry.SafeFileName`) before writing temp files or building data URIs — guard against path traversal.
4. **Event dispatch:** in `SignalEventService`, a `DataMessage` is a *presence-based union*; emit every applicable observable (text + attachment can both fire). Do not reintroduce early `return` between payload checks.
5. **Text styles:** use `ToUpperInvariant()` for style names (locale-independent).
6. **Serialization:** `System.Text.Json` **only** — `Newtonsoft.Json` is removed. Annotate model members with `[JsonPropertyName]` (never `[JsonProperty]`). Register every new serializable root type in the source-generated context `Serialization/SignalJsonContext.cs`, and serialize/deserialize via the shared options in `Serialization/SignalJson.cs` (which combines the source-gen resolver with a reflection fallback). `JsonRpcRequest.Params` / `JsonRpcResponse.Result` are `JsonElement`.
7. **Download scripts:** `src/SignalCli.runtime/download-signal-cli.*` and `src/build/download-jre.*` verify the archive SHA-256 before extraction. If you change a pinned version, update the hash in **both** the `.ps1` and `.sh`. These PowerShell scripts are deliberately **ASCII-only** (no Cyrillic/emoji) so they parse under Windows PowerShell 5.1 **without** needing a UTF-8 BOM — keep them ASCII. They also invoke the Windows system `tar` (`%SystemRoot%\System32\tar.exe`) explicitly and stage extraction through an ASCII temp dir, because Git's GNU `tar` mis-reads `C:\…` paths and bsdtar fails on non-ASCII target paths.
8. **Bundled-JRE packages** (`SignalCli.Runtime.Jre.win-x64`, `SignalCli.Runtime.Jre.osx-arm64`): bundle a SHA-256-pinned Eclipse Temurin **25** JRE + signal-cli. The JRE and jars are packed as **single `.zip` files** and extracted by the consumer `.targets` via MSBuild's built-in `<Unzip>` — **do not** pack the JRE as individual files: NuGet treats an extension-less `PackagePath` (e.g. the JRE's `lib/modules`) as a *directory* and corrupts the layout, which crashes the JVM at bootstrap. `Config.ResolveBundledJava` auto-discovers `<output>/jre/bin/java[.exe]`, so consumers need no system Java and should **not** set `Config.JavaExecutable`.

## Planning (OpenSpec)

This repo uses [OpenSpec](https://github.com/Fission-AI/OpenSpec) for change planning under `openspec/changes/`. For non-trivial work, create/extend a change (proposal → design → specs → tasks) and run `npx -y @fission-ai/openspec@latest validate <change> --strict` before implementing.

**Implemented and merged** (historical reference, do not re-open):
- `address-audit-findings` — privacy/security/correctness audit round 1.
- `modernize-architecture` — `net9.0` → `net10.0`, `Newtonsoft.Json` → `System.Text.Json` (+ source-gen `JsonSerializerContext`), single-source-of-truth process state via `ProcessStateManager`.
- `agent-ready-conventions` — `.editorconfig`, analyzers (`AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`), narrowed broad `catch`-es, this `CLAUDE.md`.
- `address-audit-findings-2` — audit round 2: bounded RPC timeout (`Config.RequestTimeoutSeconds`), windowed restart budget, idempotent `AddSignalCli`, `IAsyncDisposable` on `JsonRpcClient`, integration tests + bundled-JRE E2E.
- `comprehensive-code-audit` — the audit document itself (`AUDIT-FINDINGS.md`); fixes live in the two `address-audit-findings*` changes.

**Pending** (proposal stage, not yet implemented):
- `agent-friendly-modernization` — `IOptions<SignalCliOptions>` + `ValidateOnStart`, `SignalCliHealthMonitor` as `BackgroundService` + `PeriodicTimer`, source-generated logging (`[LoggerMessage]`), `IAsyncEnumerable<T>` event streams via `Channel<T>`, and API-discoverability fixes (`Async` suffix, explicit `CancellationToken`, drop sync-over-async `Dispose`, drop empty `IDisposable` on façades, simplify `AtomicCounter`, `[CallerArgumentExpression]`). Five capabilities, five independently shippable PRs (`agent-friendly-api` → `background-monitor` → `source-generated-logging` → `options-pattern` → `async-stream-events`). See `openspec/changes/agent-friendly-modernization/{proposal,design,tasks}.md`. **Follow the forward-looking conventions in §"For new code" above when adding code, even before the migration lands.**

## Git

Work on a feature branch; do not push or commit unless asked.
