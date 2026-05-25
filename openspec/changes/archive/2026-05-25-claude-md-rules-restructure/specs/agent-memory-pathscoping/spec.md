## ADDED Requirements

### Requirement: Agent-instruction memory SHALL be split into root CLAUDE.md plus path-scoped .claude/rules/ topic files

Root `CLAUDE.md` SHALL contain only ALWAYS-RELEVANT content (project intro, build commands, Architecture overview, Critical rules numbered #1-N, Audit baseline, Version-CHANGELOG lockstep, Implemented-merged-archived list, Git conventions, and a TOC) and SHALL be capped at ≤ 200 lines.

Topic-scoped rule files under `.claude/rules/<topic>.md` SHALL declare YAML frontmatter `paths:` array (one or more globs) so each file loads only when Claude Code edits files matching at least one declared path. The single exception is `.claude/rules/audit-debt.md` (cross-cutting prevention checklist) which SHALL declare `<!-- always-load: no paths -->` HTML-comment marker as its first non-blank line in lieu of `paths:` frontmatter.

Cross-references between root and topic files SHALL use heading-anchor style (`.claude/rules/<topic>.md § <heading>`) rather than numeric anchors (`Critical rule #N`) because numeric anchors are reserved for the numbered Critical-rules list in root and would not resolve from inside a topic file context.

#### Scenario: Root CLAUDE.md stays under the size cap
- **GIVEN** the repository at any commit
- **WHEN** `wc -l CLAUDE.md` is run
- **THEN** the result is ≤ 200 lines
- **AND** the file contains a "Topic-scoped rules" section listing all `.claude/rules/*.md` files with one-line descriptions

#### Scenario: Every topic file declares a loading mode
- **GIVEN** any file under `.claude/rules/*.md`
- **WHEN** the file is read
- **THEN** the file begins with EITHER YAML frontmatter `---\npaths:\n  - "<glob>"\n  ...\n---` OR an explicit `<!-- always-load: no paths -->` HTML-comment marker
- **AND** files without either marker fail the regression-guard

#### Scenario: Critical-rule-number cross-references resolve only within root
- **GIVEN** the substring `Critical rule #N` (where N is 1-18)
- **WHEN** `grep -rn "Critical rule #" .claude/rules/` is run
- **THEN** the result is empty (numeric cross-references appear only in root CLAUDE.md, where the numbered rules live)
- **AND** topic files use heading-anchor cross-references (`.claude/rules/<topic>.md § <heading>`) for any cross-rule references

#### Scenario: A session editing one test file loads only the testing rule
- **GIVEN** a Claude Code session that edits `Tests/SignalCli.Tests/UtilityEdgeCaseTests.cs`
- **WHEN** the user runs `/memory`
- **THEN** the loaded-context report includes `CLAUDE.md` (root, always-loaded) AND `.claude/rules/testing.md` AND `.claude/rules/audit-debt.md` (always-loaded)
- **AND** the report does NOT include `.claude/rules/signal-cli-protocol.md` or `.claude/rules/csproj-build.md` (their `paths:` don't match `Tests/SignalCli.Tests/`)

#### Scenario: Content is preserved verbatim across the split
- **GIVEN** the pre-split `CLAUDE.md` content
- **WHEN** the concatenation of post-split root + all `.claude/rules/*.md` files (frontmatter stripped, TOC stripped) is produced
- **THEN** the textual diff against the pre-split content is empty modulo frontmatter additions and TOC introduction (no rule wording is lost or modified during the split — content edits are out-of-scope for this change)

Regression-guard tests SHALL live in `Tests/SignalCli.Tests/RegressionGuards/ClaudeMdSplitConsistencyTests.cs` (joins as `RG08` in the regression-guard table) with three facts: `RootClaudeMd_StaysUnder200Lines`, `EveryRuleFile_HasValidPathsFrontmatter`, `CriticalRuleNumbers_AppearOnlyInRoot`.
