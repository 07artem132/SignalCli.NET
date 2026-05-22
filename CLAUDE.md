# CLAUDE.md

Guidance for AI coding agents (Claude Code, Copilot, etc.) working in this repository.

## Project

**SignalCli.NET** — a .NET wrapper around [`signal-cli`](https://github.com/AsamK/signal-cli) (a Java app) that exposes a typed, reactive API for the Signal messenger. The library launches and supervises `signal-cli` in JSON-RPC mode over stdin/stdout, correlates requests/responses, and surfaces incoming events through `System.Reactive` observables.

- Target framework: **net9.0** (migration to **net10.0 LTS** is planned — see `openspec/changes/modernize-architecture`).
- Requires **JDK 25+** (signal-cli 0.14.3's `Main` is class-file version 69.0 = Java 25) and **signal-cli 0.14.3** (downloaded by the `SignalCli.Runtime` package at build time). Java is **not** required with the native package (`SignalCli.Runtime.Native`, Linux x64) or the bundled-JRE packages (`SignalCli.Runtime.Jre.win-x64`, `SignalCli.Runtime.Jre.osx-arm64`).

## Build & test

```bash
dotnet build SignalCli.sln                                  # build all
dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj    # run tests (89 tests)
dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj --collect:"XPlat Code Coverage"  # coverage
```

- The `SignalCli.runtime` project downloads signal-cli on first build (network required); subsequent builds are skipped via an MSBuild `Exists` gate.
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

## Critical rules (do not regress — these are audit findings)

1. **Privacy:** never log message bodies, phone numbers, or attachment payloads above `Trace`. RPC params/results and raw stdin/stdout lines are `Trace`-only. `SignalService` logs the method name only.
2. **Process arguments:** build the signal-cli command via `ProcessConfig.ArgumentList` (each arg separate). Never go back to a single interpolated `Arguments` string with quoted paths.
3. **Attachments:** sanitize `FileName` with `Path.GetFileName` (see `AttachmentEntry.SafeFileName`) before writing temp files or building data URIs — guard against path traversal.
4. **Event dispatch:** in `SignalEventService`, a `DataMessage` is a *presence-based union*; emit every applicable observable (text + attachment can both fire). Do not reintroduce early `return` between payload checks.
5. **Text styles:** use `ToUpperInvariant()` for style names (locale-independent).
6. **Serialization:** currently `Newtonsoft.Json`; migration to `System.Text.Json` is planned (`openspec/changes/modernize-architecture`). If you add models, keep `[JsonProperty]` consistent until the STJ step.
7. **Download scripts:** `src/SignalCli.runtime/download-signal-cli.*` and `src/build/download-jre.*` verify the archive SHA-256 before extraction. If you change a pinned version, update the hash in **both** the `.ps1` and `.sh`. These PowerShell scripts are deliberately **ASCII-only** (no Cyrillic/emoji) so they parse under Windows PowerShell 5.1 **without** needing a UTF-8 BOM — keep them ASCII. They also invoke the Windows system `tar` (`%SystemRoot%\System32\tar.exe`) explicitly and stage extraction through an ASCII temp dir, because Git's GNU `tar` mis-reads `C:\…` paths and bsdtar fails on non-ASCII target paths.
8. **Bundled-JRE packages** (`SignalCli.Runtime.Jre.win-x64`, `SignalCli.Runtime.Jre.osx-arm64`): bundle a SHA-256-pinned Eclipse Temurin **25** JRE + signal-cli. The JRE and jars are packed as **single `.zip` files** and extracted by the consumer `.targets` via MSBuild's built-in `<Unzip>` — **do not** pack the JRE as individual files: NuGet treats an extension-less `PackagePath` (e.g. the JRE's `lib/modules`) as a *directory* and corrupts the layout, which crashes the JVM at bootstrap. `Config.ResolveBundledJava` auto-discovers `<output>/jre/bin/java[.exe]`, so consumers need no system Java and should **not** set `Config.JavaExecutable`.

## Planning (OpenSpec)

This repo uses [OpenSpec](https://github.com/Fission-AI/OpenSpec) for change planning under `openspec/changes/`. For non-trivial work, create/extend a change (proposal → design → specs → tasks) and run `npx -y @fission-ai/openspec@latest validate <change>` before implementing. Active changes: `address-audit-findings` (implemented), `modernize-architecture`, `agent-ready-conventions`.

## Git

Work on a feature branch; do not push or commit unless asked.
