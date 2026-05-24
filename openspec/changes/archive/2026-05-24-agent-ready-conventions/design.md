## Context

The project enables `Nullable` and ships rich XML docs and 89 tests, but style/quality is only described in a ReSharper `.DotSettings` (not enforced in CI/build and not cross-tool). There is no agent-instruction file. Broad `catch (Exception)` appears in several places; some are legitimate loop boundaries, others could be narrowed.

## Goals / Non-Goals

**Goals:** a cross-tool, build-time style/quality gate; a single source of agent guidance; exception handling that matches Microsoft conventions without destabilizing the supervised-process resilience.

**Non-Goals:** `TreatWarningsAsErrors` everywhere on day one (too disruptive); rewriting the resilience model; AOT/trim analyzers (those belong with the STJ source-gen step).

## Decisions

- **EditorConfig**: root `.editorconfig` derived from the .NET runtime/docs conventions — naming rules (`_camelCase` private, `s_` static, `I` interfaces, PascalCase public), `var` usage, file-scoped namespaces, `using` outside namespace, expression preferences. This supersedes the ReSharper-only `.DotSettings` for cross-tool/agent use.
- **Analyzers**: `<AnalysisLevel>latest-recommended</AnalysisLevel>` + `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>` in `Directory.Build.props` (one place) rather than per-csproj, so it applies uniformly. Keep warnings as warnings initially; promote to errors later once the backlog is clean.
- **Agent guidance**: `CLAUDE.md` (Claude Code's convention) is the canonical instructions file; a `.github/copilot-instructions.md` may later point to it. Content mirrors the critical audit rules so agents don't regress them.
- **Exception handling**: classify each `catch (Exception)`:
  - *Keep (documented)* at long-running boundaries where one bad item must not kill the loop: `JsonRpcClient` stdout reader, `SignalCliHealthMonitor.MonitorLoop`, `SignalEventService.OnNotificationReceived`. Add a brief comment stating the intent.
  - *Narrow* where a specific failure is expected: JSON parse → `JsonException`, process start → `InvalidOperationException`/`Win32Exception`, IO → `IOException`.

## Risks / Trade-offs

- [Analyzers surface a wall of warnings] → introduce via `Directory.Build.props`, triage in one pass, suppress with justification only where needed; do not gate the build on them yet.
- [Over-narrowing exception catches reduces resilience] → never narrow the supervised-process loop boundaries; those stay broad-but-documented.
- [EditorConfig vs existing `.DotSettings` conflict] → `.editorconfig` is authoritative; align or remove conflicting `.DotSettings` rules.
