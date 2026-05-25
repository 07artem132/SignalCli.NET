# Tasks — claude-md-pattern-additions

## 0. Setup

- [ ] 0.1 Branch off main: `git checkout -b docs/claude-md-pattern-additions`.
- [ ] 0.2 Run `npx -y @fission-ai/openspec@latest validate claude-md-pattern-additions --strict` — confirm green.
- [ ] 0.3 Check current CLAUDE.md baseline: `wc -l CLAUDE.md` (expected 556 lines).

## 1. Three additions to CLAUDE.md (capability `claude-md-pattern-additions`)

- [ ] 1.1 **Addition 1 — "DI registration" new subsection.** Insert before `#### Background loops + time` subsection (currently around line 167). Content per `design.md` § Addition 1 (~10 lines: 3 bullets with `SignalCliHostedService` registration example + NF-003 cross-reference for sentinel-marker rationale).
- [ ] 1.2 **Addition 2 — "Conventions" section additions.** Insert at end of existing `## Conventions (match the existing code)` section, before `### Established patterns` heading. Content per `design.md` § Addition 2 (~10 lines: 4 bullets covering namespace hierarchy, DTO naming, event-args records, test-class naming).
- [ ] 1.3 **Addition 3 — "Other established patterns" exception-derivation bullet.** Insert as final bullet of existing `#### Other established patterns` subsection (currently around line 187-190). Content per `design.md` § Addition 3 (~3 lines: heuristic with two current derived-types as examples).
- [ ] 1.4 **Addition 4 — "README voice + drift rules" new subsection.** Insert after existing `### CHANGELOG voice template` subsection (currently around line 419, end of "Audit baseline → Version-CHANGELOG lockstep" block). Content per `design.md` § Addition 4 (~13 lines: 5 bullets covering audience contract, internal-ID prohibition, quick-start-compile invariant, PR-time triggers, badges + NuGet-pack pairing).
- [ ] 1.5 `wc -l CLAUDE.md` — confirm grew from 556 to ~591 (delta ~35 lines for all 4 additions).
- [ ] 1.6 `dotnet build SignalCli.sln && dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj --no-build` — 287 tests still green (no code change).
- [ ] 1.7 Manual cross-reference check: each addition cites an existing test or capability slug. Verify:
  - "NF-003 addsignalcli-idempotency-fix" — exists at `openspec/changes/archive/2026-05-24-audit-followup-2026/specs/addsignalcli-idempotency-fix/spec.md` ✓
  - "`JsonContextRegistrationTests`" — exists at `Tests/SignalCli.Tests/JsonContextRegistrationTests.cs` ✓
  - "`EventApiSymmetryTests` RG06" — exists at `Tests/SignalCli.Tests/RegressionGuards/EventApiSymmetryTests.cs` ✓
  - "Critical rule #13" — verify still numbered #13 in current CLAUDE.md (could have renumbered).
  - Addition 4 cites "csproj/MSBuild conventions in Established patterns" (added in commit `a95985d`) — verify subsection still exists at expected location.
- [ ] 1.8 Commit: `"docs(CLAUDE.md): document DI registration + naming + exception derivation + README voice (4 audit-v2.1 gap-fills)"`. Suggested body cites `claude-md-pattern-additions` capability slug + acknowledges co-existence with `claude-md-rules-restructure` (plan-only, will move these additions into topic files if executed after — DI + exceptions → `patterns.md`, naming → `conventions.md`, README voice → `openspec-workflow.md` alongside CHANGELOG-voice template).

## 2. Verify + push

- [ ] 2.1 `git pull --rebase origin main && git push origin docs/claude-md-pattern-additions`.
- [ ] 2.2 Open PR. Reviewer focus: each addition is "this pattern already exists in code, just wasn't in docs" — verify code matches doc claim (`grep -n "TryAddSingleton" src/SignalCli/Extensions/ServiceCollectionExtensions.cs` should show the 3-line one-instance-two-roles idiom for each of the 3 cited services).
- [ ] 2.3 Squash-merge or merge-merge per repo convention.

## 3. Post-merge archive

- [ ] 3.1 `git checkout main && git pull`.
- [ ] 3.2 `npx -y @fission-ai/openspec@latest archive claude-md-pattern-additions --yes --skip-specs`.
- [ ] 3.3 Commit: `"chore(openspec): archive claude-md-pattern-additions → YYYY-MM-DD"`.
- [ ] 3.4 Update root CLAUDE.md "Implemented, merged, archived" list — add bullet for `claude-md-pattern-additions` with one-line summary.
- [ ] 3.5 `git pull --rebase origin main && git push origin main`.

## Estimated time

- Phase 1 (4 text additions + verification): ~1h
- Phase 2 (push + PR + merge): ~15min (mechanical)
- Phase 3 (archive + CLAUDE.md update): ~15min after merge

**Total: ~1.5h agent-time.** Small change — 4 additions ~35 lines.

## Co-existence with `claude-md-rules-restructure`

| Execute order | Outcome |
|---|---|
| This first, restructure later | Additions land in monolithic CLAUDE.md; restructure moves them to `.claude/rules/patterns.md` + `.claude/rules/conventions.md` per its section-to-file mapping (verbatim move, no editing). |
| Restructure first, this later | Additions land directly in the topic files (`patterns.md`, `conventions.md`); skips the monolithic-interim. Same end-state. |
| Concurrent / merge race | Rebase conflict on CLAUDE.md (or on the topic files post-restructure) is mechanical — 3 insertion points, each independent. Each addition has unique surrounding context for unambiguous resolution. |

**Either order works.** No dependency lock between the two changes.
