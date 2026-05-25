# Tasks — claude-md-rules-restructure

## 0. Setup

- [ ] 0.1 Branch off main: `git checkout -b refactor/claude-md-rules-split`.
- [ ] 0.2 Run `npx -y @fission-ai/openspec@latest validate claude-md-rules-restructure --strict` — confirm green before any source edits.
- [ ] 0.3 Audit pre-state: `wc -l CLAUDE.md` (record baseline; expected 556 lines), `grep -c 'Critical rule #' CLAUDE.md` (record # of cross-refs), `gh api repos/anthropics/claude-code/releases/latest --jq .tag_name` (record current CLI version for path-scoping support evidence).

## 1. Create `.claude/rules/` topic files (capability `agent-memory-pathscoping`)

- [ ] 1.1 Create `.claude/rules/` directory.
- [ ] 1.2 Create `.claude/rules/signal-cli-protocol.md` — frontmatter `paths: ["src/SignalCli/Services/**", "src/SignalCli.runtime/**", "src/SignalCli.runtime.native/**", "src/SignalCli.runtime.jre.*/**"]`; content = current CLAUDE.md lines 52-120 (signal-cli protocol behavior we depend on).
- [ ] 1.3 Create `.claude/rules/conventions.md` — frontmatter `paths: ["src/SignalCli/**", "src/SignalCli.HealthChecks/**", "Tests/**", "Example/**"]`; content = current lines 122-131 (Conventions section).
- [ ] 1.4 Create `.claude/rules/patterns.md` — frontmatter `paths: ["src/SignalCli/**", "src/SignalCli.HealthChecks/**"]`; content = current lines 133-233 (Established patterns) + the `[LoggerMessage]` template (added in commit `a95985d`).
- [ ] 1.5 Create `.claude/rules/csproj-build.md` — frontmatter `paths: ["**/*.csproj", "Directory.Build.props", ".github/workflows/**", "src/build/**", "src/SignalCli.runtime*/**"]`; content = the csproj/MSBuild conventions subsection (added in commit `a95985d`) + the "Mass-edit safety" + supply-chain subsection from current Established patterns.
- [ ] 1.6 Create `.claude/rules/testing.md` — frontmatter `paths: ["Tests/**"]`; content = the FakeTimeProvider + test-only rules currently scattered across "Background loops + time" subsection (line 167-172) + "Regression guards" subsection (line 205-215) + the "No wall-clock in tests" critical rule (#11).
- [ ] 1.7 Create `.claude/rules/obsolete-shims.md` — frontmatter `paths: ["src/SignalCli/**"]`; content = current lines 235-255 (Backward compatibility convention).
- [ ] 1.8 Create `.claude/rules/audit-debt.md` — **no `paths:` frontmatter** (always-load; explicit `<!-- always-load: no paths -->` marker on first line); content = Future development guardrails (278-301) + How we discovered (421-453) + Working style (501-522).
- [ ] 1.9 Create `.claude/rules/openspec-workflow.md` — frontmatter `paths: ["openspec/**", "CHANGELOG.md", "CLAUDE.md"]`; content = Planning (OpenSpec) (455-499) + CHANGELOG voice template (356-419).
- [ ] 1.10 Create `.claude/rules/cloud-dev.md` — frontmatter `paths: [".claude/**", "docs/cloud-development.md"]`; content = Cloud development paragraph (34-36) + (after execution validation) the minimum-Claude-Code-CLI-version note.
- [ ] 1.11 `wc -l .claude/rules/*.md` — each file ≤ 200 lines; report total combined size.
- [ ] 1.12 `dotnet build SignalCli.sln` clean (no code touched; sanity check that doc-only changes don't break anything via path-injection).
- [ ] 1.13 `git add .claude/rules/` + commit: `"docs(claude-md): create .claude/rules/ topic files (Phase 1 — content copied, root unchanged)"`. At this point root CLAUDE.md is unchanged → rules effectively double-load. Intentional intermediate state.

## 2. Slim root CLAUDE.md + rewrite cross-references (capability `agent-memory-pathscoping`)

- [ ] 2.1 Edit `CLAUDE.md` to keep only: Project + Build & test + Architecture (key types) + Critical rules (numbered #1-18) + Audit baseline + Version-CHANGELOG lockstep + Implemented-merged-archived list + Git. Remove all sections moved to `.claude/rules/`.
- [ ] 2.2 Add TOC near top of root CLAUDE.md pointing to each `.claude/rules/<topic>.md`:
        ```markdown
        ## Topic-scoped rules

        Path-scoped agent instructions in `.claude/rules/` (load conditionally when Claude edits matching files):
        - `signal-cli-protocol.md` — upstream protocol facts (loads when editing `src/SignalCli/Services/`)
        - `conventions.md` — modern C# / naming / comments (loads when editing `src/**` or `Tests/**`)
        - `patterns.md` — Established patterns (loads when editing `src/**`)
        - `csproj-build.md` — csproj/MSBuild + CI conventions (loads when editing `*.csproj` / workflows)
        - `testing.md` — FakeTimeProvider / regression-guard test patterns (loads when editing `Tests/**`)
        - `obsolete-shims.md` — backward-compatibility convention (loads when editing `src/**`)
        - `audit-debt.md` — Future guardrails + prevention checklist + working style (always-load)
        - `openspec-workflow.md` — Planning + post-merge archive + CHANGELOG voice (loads when editing `openspec/**`, `CHANGELOG.md`, `CLAUDE.md`)
        - `cloud-dev.md` — Cloud Code on Web setup (loads when editing `.claude/**` or docs/cloud-development.md)
        ```
- [ ] 2.3 Rewrite cross-references in `.claude/rules/*.md`:
        - `grep -rn 'Established patterns →' .claude/rules/` — rewrite each `Established patterns → X` to `.claude/rules/patterns.md § X`.
        - `grep -rn 'Critical rule #' .claude/rules/` — for each match, verify the referenced rule # is still in root CLAUDE.md (numbered Critical rules survived the slim). If yes, leave as-is (the cross-ref resolves to always-loaded root). If no, find the new location in topic file and rewrite to heading-style.
        - `grep -rn 'CLAUDE.md "X"' .claude/rules/` — for each, verify "X" anchor still exists in root post-slim; if moved to topic file, rewrite path.
- [ ] 2.4 `wc -l CLAUDE.md` — assert ≤ 200 (target 120-150). If over, identify content that could move into a topic file.
- [ ] 2.5 `dotnet build SignalCli.sln && dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj --no-build` — 287 tests still green (no code change; existing RG01-RG07 still pass; new RG08 not yet added).
- [ ] 2.6 Commit: `"docs(claude-md): slim root to <=200 lines + rewrite cross-references (Phase 2)"`.

## 3. Add RG08 — ClaudeMdSplitConsistencyTests

- [ ] 3.1 Create `Tests/SignalCli.Tests/RegressionGuards/ClaudeMdSplitConsistencyTests.cs` with 3 facts:
        - `RootClaudeMd_StaysUnder200Lines`
        - `EveryRuleFile_HasValidPathsFrontmatter`
        - `CriticalRuleNumbers_AppearOnlyInRoot`
        Skeleton in `design.md` § "RG08 — ClaudeMdSplitConsistencyTests".
- [ ] 3.2 `dotnet test --filter "FullyQualifiedName~ClaudeMdSplitConsistencyTests"` — 3/3 green.
- [ ] 3.3 `dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj` full suite — 290 tests green (287 baseline + 3 new RG08 facts).
- [ ] 3.4 Update root CLAUDE.md "Audit baseline → Regression guards" table — add RG08 row.
- [ ] 3.5 Commit: `"test: add RG08 ClaudeMdSplitConsistencyTests (pins doc-structure split shape)"`.

## 4. CHANGELOG entry + version bump

- [ ] 4.1 Bump `<SignalCliPackageVersion>` in `Directory.Build.props`: 4.0.2 → 4.0.3.
- [ ] 4.2 Add `## [4.0.3] — YYYY-MM-DD` section in CHANGELOG.md (top, above `[4.0.2]`) following CHANGELOG voice template — first claim must be plain-language consumer-facing. Suggested opening: "Patch — внутрішнє: agent-instruction memory (`CLAUDE.md`) переструктурована по Anthropic guidance в root + 9 path-scoped topic files. **Нульовий impact на consumer'ів** — це чисто developer/agent ergonomics."
- [ ] 4.3 Commit: `"docs: 4.0.3 release entry — claude-md-rules-restructure (no consumer impact)"`.

## 5. Verify + push

- [ ] 5.1 `dotnet build SignalCli.sln` 0/0; `dotnet test` 290 green.
- [ ] 5.2 `git pull --rebase origin main && git push origin <branch>`.
- [ ] 5.3 Open PR. Reviewer focus: read `.claude/rules/<topic>.md` files in TOC-order — should feel coherent as standalone topics.
- [ ] 5.4 **Post-merge `/memory` validation** — in a Claude Code session editing a single test file (e.g. `Tests/SignalCli.Tests/UtilityEdgeCaseTests.cs`), run `/memory` and confirm `testing.md` loaded, `signal-cli-protocol.md` NOT loaded. Record finding in `cloud-dev.md` (the minimum-CLI-version evidence).

## 6. Post-merge archive

- [ ] 6.1 `git checkout main && git pull`.
- [ ] 6.2 `npx -y @fission-ai/openspec@latest archive claude-md-rules-restructure --yes --skip-specs`.
- [ ] 6.3 Commit: `"chore(openspec): archive claude-md-rules-restructure → YYYY-MM-DD"`.
- [ ] 6.4 Update CLAUDE.md "Implemented, merged, archived" list (in root, where it stayed) — add bullet for `claude-md-rules-restructure` with one-line summary.
- [ ] 6.5 `git pull --rebase origin main && git push origin main`.

## Estimated time

- Phase 1 (mechanical file creation): ~1.5h
- Phase 2 (slim root + cross-ref rewrite): ~1h
- Phase 3 (RG08 test): ~30min
- Phase 4 (CHANGELOG): ~15min
- Phase 5 (verify + push + `/memory` validation): ~30min
- Phase 6 (post-merge archive): ~15min after PR merges

**Total: ~3.5h agent-time** (per honest correction).
