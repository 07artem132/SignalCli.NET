## Why

The codebase already follows many modern C# conventions, but it has no machine-enforced style/quality gate (only a ReSharper `.DotSettings`, which is tool-specific) and no agent-instruction file. Microsoft's guidance for AI-assisted .NET development recommends a cross-tool `.editorconfig` + analyzers and a repo-level instructions file so that both humans and AI agents produce consistent, convention-aligned code. It also recommends catching specific exception types rather than broad `catch (Exception)`.

## What Changes

- **Analyzers + EditorConfig**: add a root `.editorconfig` with C# style/naming rules; enable `EnforceCodeStyleInBuild` and `AnalysisLevel=latest-recommended` in the projects so style/quality is checked at build time. Keep `Nullable` enabled.
- **Agent instructions**: a `CLAUDE.md` at the repo root captures architecture, build/test commands, conventions, and the critical audit-derived rules (no-PII-logging, `ArgumentList`, attachment sanitization, composite event dispatch, STJ-planned, the `.ps1` BOM gotcha). (Already drafted.)
- **Exception handling**: review broad `catch (Exception)` sites and narrow to specific types where appropriate; explicitly document the intentional broad catches at long-running loop boundaries (stdout reader, health-monitor loop, notification dispatcher) as log-and-continue.

## Capabilities

### New Capabilities
- `code-quality-gates`: machine-enforced C# style and analyzer rules at build time.
- `agent-guidance`: a repo-level instructions file for AI coding agents.
- `exception-handling`: catching specific exception types, with intentional broad catches confined to documented loop boundaries.

### Modified Capabilities
<!-- None: no baseline specs exist. -->

## Impact

- New files: `.editorconfig`, `CLAUDE.md`.
- Edited: all `.csproj` (`EnforceCodeStyleInBuild`, `AnalysisLevel`); `Services/Rpc/JsonRpcClient.cs`, `Services/Signal/{SignalService,SignalEventService}.cs`, `Services/SignalCli/*` (exception narrowing where safe).
- Risk: enabling analyzers may surface new warnings — triage and either fix or suppress with justification; do not blanket-treat warnings as errors initially.
