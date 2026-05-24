# Restructure CLAUDE.md into root + path-scoped `.claude/rules/` files

## Why

Anthropic's official Claude Code guidance ([Best practices](https://code.claude.com/docs/en/best-practices.md), [Memory](https://code.claude.com/docs/en/memory.md)) targets CLAUDE.md at **50–200 lines** (canonical example ~50–120). Their published "signal of drift" is `CLAUDE.md > 250 lines → it's too large; use @import, .claude/rules/, or .claude/skills/ to split it`. Our `CLAUDE.md` is **556 lines** — **2.8× over** the upper guideline.

This wasn't accidental. We deliberately favored depth over slimness through the audit-v2.0/v2.1 work cycle: every rule is code-anchored, regression-guards prevent drift, and the file's `Audit baseline` section is itself a quality artifact. The content stands. But the **packaging** doesn't match the canonical pattern, and we're paying a context-window tax on EVERY Claude session — including ones that touch a single test file or a doc-only CHANGELOG edit, where signal-cli-protocol or csproj-conventions content adds zero value but consumes context.

`.claude/rules/<topic>.md` with frontmatter `paths:` is Anthropic's recommended split: rule files load **only when Claude edits matching files**. A session that edits `CHANGELOG.md` loads only the `openspec-workflow` rule; a session editing `Tests/**` loads the `testing` rule; a session editing `src/SignalCli/Services/Rpc/**` loads `signal-cli-protocol`. The root CLAUDE.md becomes a slim **always-loaded core**: project intro, build commands, the 18 critical rules, the regression-guard table, version-lockstep, and Git conventions.

Three secondary benefits:

1. **Doc-discovery for humans.** "Where's the rule about csproj patterns?" → `ls .claude/rules/` shows topic-files; current monolithic CLAUDE.md requires `Ctrl-F` through 556 lines.
2. **Per-topic versioning.** Audit-derived rules can age into `archive/` over time; current monolithic doc has no graceful way to "retire" a rule beyond inline note.
3. **Multi-agent friendlier.** Future plugins / specialized agents that only care about one domain (testing-bot, release-bot) can `@import` just one rule file.

## What Changes

Single capability covering both the split mechanic and the new invariants it establishes:

- **`agent-memory-pathscoping`**:
  - **Root `CLAUDE.md` slimmed to ~120-150 lines** containing only ALWAYS-relevant content: project intro, build/test commands, Architecture (key types) high-level diagram, Critical rules (the 18 numbered), Audit baseline (test counts + regression-guards table), Version-CHANGELOG lockstep, Git conventions, and a TOC pointing to `.claude/rules/<topic>.md` files.
  - **9 new files under `.claude/rules/`**, each with frontmatter `paths:` declaring which file globs trigger loading:
    - `signal-cli-protocol.md` — paths: `src/SignalCli/Services/**`, `src/SignalCli.runtime/**` — the 7 cited upstream facts.
    - `conventions.md` — paths: `src/SignalCli/**`, `Tests/**` — modern C#, naming, exceptions, comments-in-Ukrainian.
    - `patterns.md` — paths: `src/SignalCli/**` — Established patterns (async/cancellation, configuration, logging, background loops, event streams, disposal, other patterns, AOT readiness, regression guards) + the `[LoggerMessage]` template.
    - `csproj-build.md` — paths: `**/*.csproj`, `Directory.Build.props`, `.github/workflows/**` — csproj/MSBuild conventions + mass-edit safety + supply-chain.
    - `testing.md` — paths: `Tests/**` — FakeTimeProvider, MeterListener, reflection-based regression-guards, baseline counters details.
    - `obsolete-shims.md` — paths: `src/SignalCli/**` — Backward compatibility convention (one-major-grace, doc-sync invariant, three-site duplication trap).
    - `audit-debt.md` — no paths (always load) — Future development guardrails + How we discovered (prevention checklist) — because these are cross-cutting agent-instruction-quality rules that any session should heed.
    - `openspec-workflow.md` — paths: `openspec/**`, `CHANGELOG.md`, `CLAUDE.md` — Planning workflow + post-merge archive + CHANGELOG voice template.
    - `cloud-dev.md` — paths: `.claude/**`, `docs/cloud-development.md` — Cloud development notes.
  - **`Implemented, merged, archived` list** stays in root CLAUDE.md (small, historical reference; loaded once per session anyway).
  - **One new regression-guard test** RG08 — `ClaudeMdSplitConsistencyTests` — pins (a) root CLAUDE.md size ≤ 200 lines, (b) every `.claude/rules/<topic>.md` file has valid frontmatter with `paths:` array (or explicit no-path marker), (c) no critical-rule-number appears in TWO files (would break "see Critical rule #N" cross-refs).

The split is **content-preserving**: every existing rule survives in some file. No new rules added (other than the meta-rules about the split itself). No code change.

## Capabilities

### New Capabilities

- **`agent-memory-pathscoping`**: agent-instruction memory SHALL be split across root `CLAUDE.md` (always-loaded core, ≤ 200 lines) and topic-scoped `.claude/rules/<topic>.md` files with frontmatter `paths:` declarations. Each topic file loads only when Claude edits files matching its declared paths. Cross-references between root and topic files SHALL use either anchor-name (`see "Established patterns → Logging" in .claude/rules/patterns.md`) or path-style (`paths: see .claude/rules/csproj-build.md`); numeric cross-references ("Critical rule #N") only resolve within root CLAUDE.md and SHALL NOT be used to point into topic files.

### Modified Capabilities

- **None.** All existing rule content survives; only packaging changes. Regression-guard tests (R01–RG07) continue passing — they pin code-level invariants, not document-structure invariants. New RG08 adds document-structure pinning on top.

## Out of scope

- **Rewriting any rule content.** This change is purely structural — text moves verbatim. Sentence-level editing (clarity passes, voice tightening) is a separate future change.
- **Adopting `.claude/skills/` for any of our patterns.** Skills serve interactive workflows (e.g. "create-new-event-kind" wizard); our rules are passive constraints. Skills could augment later but aren't a substitute for the rules.
- **Adopting `.claude/hooks/` to ENFORCE rules.** Our rules are largely build-time enforced through regression-guard tests (privacy, EventId blocks, version lockstep, Obsolete-message consistency, etc.). Hooks would be redundant. Re-evaluate only if a rule emerges that's hard to test but easy to detect via hook (e.g. "every PR title starts with type(scope):").
- **Moving the `Implemented, merged, archived` historical bullet list out of root.** It's append-only history (~12 items), small, and useful as session-start context for date-anchored cross-references in archived OpenSpec changes. Leaving it in root keeps it discoverable.
- **Verifying the path-scope feature works in older Claude Code versions.** We document the minimum CLI version in `cloud-dev.md` after first execution validates `/memory` actually loads files conditionally; if a user reports an older CLI doesn't path-scope, we add a fallback `CLAUDE.md` content equivalent for that case.
