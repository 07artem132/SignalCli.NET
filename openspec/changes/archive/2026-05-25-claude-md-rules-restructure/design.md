# Design — claude-md-rules-restructure

## Method

Content-preserving structural split of one 556-line `CLAUDE.md` into one slim root (~150 lines) plus 9 topic-scoped files under `.claude/rules/`. Each topic file declares which file globs trigger its load via YAML frontmatter `paths:` field. Cross-references re-pointed from "Critical rule #N" style (anchored in monolithic doc) to "see `<topic>.md` § <heading>" style (works across split).

No source-code change. No test-content change (existing R01–RG07 guards pin code/source state, not doc state). One new doc-structure regression-guard RG08 pins the split-shape so future PRs can't accidentally re-merge or lose path-scoping.

## Section-to-file mapping

Concrete decisions per current `CLAUDE.md` section (line numbers from HEAD@`6e0618e`):

| Current section (lines) | Destination | `paths:` frontmatter |
|---|---|---|
| Project (5-11) | **Root** | n/a (always load) |
| Build & test (12-32) | **Root** | n/a |
| Cloud development (34-36) | `.claude/rules/cloud-dev.md` | `.claude/**`, `docs/cloud-development.md` |
| Architecture (key types) (38-50) | **Root** | n/a (overview) |
| signal-cli protocol behavior (52-120) | `.claude/rules/signal-cli-protocol.md` | `src/SignalCli/Services/**`, `src/SignalCli.runtime/**`, `src/SignalCli.runtime.native/**`, `src/SignalCli.runtime.jre.*/**` |
| Conventions (122-131) | `.claude/rules/conventions.md` | `src/SignalCli/**`, `src/SignalCli.HealthChecks/**`, `Tests/**`, `Example/**` |
| Established patterns (133-233) | `.claude/rules/patterns.md` | `src/SignalCli/**`, `src/SignalCli.HealthChecks/**` |
| Backward compatibility convention (235-255) | `.claude/rules/obsolete-shims.md` | `src/SignalCli/**` |
| **Critical rules** (257-276) | **Root** | n/a (numbered, cross-ref-anchor) |
| Future development guardrails (278-301) | `.claude/rules/audit-debt.md` | no `paths:` (always load — cross-cutting) |
| **Audit baseline** (303-338) | **Root** | n/a (current minimum bar) |
| **Version-CHANGELOG lockstep** (340-355) | **Root** | n/a (release process — every commit could trigger) |
| CHANGELOG voice template (356-419) | `.claude/rules/openspec-workflow.md` | `openspec/**`, `CHANGELOG.md`, `CLAUDE.md` |
| How we discovered (421-453) | `.claude/rules/audit-debt.md` | (joined with Future guardrails — same author-audience: PR-time checklists) |
| Planning (OpenSpec) (455-499) | `.claude/rules/openspec-workflow.md` | (joined with CHANGELOG voice) |
| Working style (501-522) | `.claude/rules/audit-debt.md` | (joined — same "how Claude works in this repo" topic) |
| **Git** (524+) | **Root** | n/a (every commit) |
| **Implemented, merged, archived** list | **Root** | n/a (historical reference for cross-refs from archive) |

**Root CLAUDE.md after split** (≤ 200 lines target, ~150 expected):

```
# CLAUDE.md
- Project
- Build & test (+ restore-in-sandbox subsection)
- Architecture (key types)
- TOC → .claude/rules/<topic>.md
- Critical rules (numbered #1-18, cross-ref anchor stays here)
- Audit baseline (≥287 unit, ≥8 integration, RG table, version-lockstep)
- Version-CHANGELOG lockstep (+ CHANGELOG-voice-template-reference)
- Implemented, merged, archived (historical bullet list)
- Git
```

**`.claude/rules/<topic>.md`** files, all under 200 lines each (largest expected: `patterns.md` at ~110 lines).

## Frontmatter format

Anthropic-documented shape:

```markdown
---
paths:
  - "src/SignalCli/**"
  - "Tests/SignalCli.Tests/**"
---

# Topic heading

Content...
```

Edge cases:

- **No paths** = always load (used for `audit-debt.md` because the prevention-checklist applies to ANY edit session).
- **Multiple globs** = OR semantics (file matches ANY glob → rule loads).
- **Glob syntax** = `**` for recursive, `*` for one segment, no regex. Verified compatible with our existing path conventions.

## Cross-reference rewrite policy

Current CLAUDE.md uses two cross-ref styles:

1. **Numeric**: "Critical rule #18", "post-modernize-tuning §6.7", "CLAUDE.md rule #11".
2. **Heading**: "Established patterns → Event streams: two surfaces".

After split:

- **Numeric cross-refs stay valid for root content only.** Critical rules #1-18 stay in root CLAUDE.md → "Critical rule #N" works (always-loaded). audit-followup-2026 §N references in archived OpenSpec changes also work (point to archive files, not CLAUDE.md).
- **Heading cross-refs gain explicit file prefix**: "Established patterns → Logging" becomes ".claude/rules/patterns.md § Logging". Tedious but unambiguous.
- **Cross-file cross-refs**: where `csproj-build.md` references something in `patterns.md`, use ".claude/rules/patterns.md § AOT readiness" style.

The migration script (manual but mechanical):

```
grep -rn 'Established patterns →' .  # all heading refs to be rewritten
grep -rn 'rule #\(1[0-8]\|[1-9]\)' . # numeric refs — verify each still resolves to root
```

## RG08 — ClaudeMdSplitConsistencyTests

New reflection-based regression-guard in `Tests/SignalCli.Tests/RegressionGuards/`:

```csharp
public class ClaudeMdSplitConsistencyTests
{
    [Fact]
    public void RootClaudeMd_StaysUnder200Lines()
    {
        var lines = File.ReadAllLines(LocateClaudeMd()).Length;
        Assert.True(lines <= 200,
            $"Root CLAUDE.md grew to {lines} lines — split topic-sections into .claude/rules/.");
    }

    [Fact]
    public void EveryRuleFile_HasValidPathsFrontmatter()
    {
        var rulesDir = LocateRulesDir();
        var failures = new List<string>();
        foreach (var ruleFile in Directory.EnumerateFiles(rulesDir, "*.md"))
        {
            var content = File.ReadAllText(ruleFile);
            // Either '---\npaths:\n  - ...\n---' OR explicit '<!-- always-load: no paths -->'
            var hasFrontmatter = content.StartsWith("---\n") && content.Contains("\npaths:\n");
            var hasAlwaysLoadMarker = content.Contains("<!-- always-load: no paths -->");
            if (!hasFrontmatter && !hasAlwaysLoadMarker)
                failures.Add($"{Path.GetFileName(ruleFile)}: missing frontmatter OR always-load marker");
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CriticalRuleNumbers_AppearOnlyInRoot()
    {
        // 'Critical rule #N' (N=1..18) substring must appear in CLAUDE.md root, not in any
        // .claude/rules/<topic>.md — those numbers are anchored to root content.
        var rootRefs = CountMatches(LocateClaudeMd(), @"Critical rule #1[0-8]|Critical rule #[1-9]");
        Assert.True(rootRefs > 0, "Root CLAUDE.md should have the numbered Critical rules anchor.");

        var rulesDir = LocateRulesDir();
        foreach (var ruleFile in Directory.EnumerateFiles(rulesDir, "*.md"))
        {
            var matches = CountMatches(ruleFile, @"Critical rule #1[0-8]|Critical rule #[1-9]");
            Assert.True(matches == 0,
                $"{Path.GetFileName(ruleFile)} references Critical rule #N — those numbers anchor to root; use heading-style cross-ref instead.");
        }
    }
}
```

Joins the RG family as `RG08`.

## Risk analysis

**Risk 1: `paths:` frontmatter isn't supported by older Claude Code CLI versions.**
- Mitigation: First execution session uses `/memory` to verify which files actually loaded. Document minimum CLI version in `cloud-dev.md` after empirical check.
- Fallback: if a contributor's CLI version doesn't support path-scoping, all rule files just load unconditionally (degrades to current monolithic behavior — no harm).

**Risk 2: Context-budget regression — multiple rules load simultaneously, total context exceeds monolithic baseline.**
- Mitigation: each rule file ≤ 200 lines; even loading all 9 simultaneously = ~1500 lines vs current 556. Worst case 2.7× context. But typical session edits a small set of paths → 1-3 rule files load → 200-600 lines. Net improvement.
- Verify: `/memory` reports loaded files per session. If overflow becomes an issue, split the rules into finer-grained files.

**Risk 3: Doc-discovery friction for humans not using Claude Code.**
- Mitigation: TOC in root CLAUDE.md lists all topic files with one-line description each. README "Як зробити внесок" section points to CLAUDE.md as entry point.
- The rules ARE more discoverable in split form: `ls .claude/rules/` shows topics; current monolithic doc requires `Ctrl-F`.

**Risk 4: Cross-reference breakage from un-discovered "Critical rule #18" reference inside a topic file post-split.**
- Mitigation: RG08 `CriticalRuleNumbers_AppearOnlyInRoot` regression-guard catches this.
- One-time migration uses `grep` over all `.claude/rules/*.md` to flag and rewrite during the split.

**Risk 5: `.claude/` directory naming collision with user's `~/.claude/` global config.**
- Mitigation: per Anthropic docs, project `.claude/` and user `~/.claude/` are independent namespaces. No collision.

## Validation strategy

After execution (separate from this OpenSpec change):

1. **Build + test** — should pass unchanged (RG01-RG07 unchanged; new RG08 should pass on the freshly-split structure).
2. **`/memory` command** in a session that edits exactly one file (e.g. `Tests/SignalCli.Tests/UtilityEdgeCaseTests.cs`) — should report `testing.md` rule loaded, NOT `patterns.md` or `signal-cli-protocol.md`.
3. **Manual content audit** — diff old monolithic CLAUDE.md against (root + all topic files concatenated) → text-identical except for added frontmatter blocks and TOC.
4. **CHANGELOG entry** under future `[4.0.3]` release: brief "Restructured CLAUDE.md per Anthropic guidance — split into root + 9 topic files under `.claude/rules/` with path-scoping. No content removed; cross-references rewritten. Adds RG08 regression-guard. Anthropic-recommended pattern for projects exceeding 250-line CLAUDE.md."

## Why one commit per phase (not one mega-commit)

Per CLAUDE.md "Working style → One commit per capability/cluster":

1. Commit 1: create `.claude/rules/` directory + add the 9 topic files with content copied from current sections + frontmatter. CLAUDE.md unchanged. Result: rules double-load (also in root) but build/tests still green.
2. Commit 2: slim root CLAUDE.md to only always-load content + TOC. Cross-references rewritten. Result: rules load only conditionally; root ≤ 200 lines.
3. Commit 3: add RG08 (`ClaudeMdSplitConsistencyTests`). Result: doc-structure now pinned against regression.
4. Commit 4: CHANGELOG entry + version bump (`<SignalCliPackageVersion>` 4.0.2 → 4.0.3).

Phase 1 → 2 is the riskiest transition (cross-ref rewrites); isolating it as its own commit means easy revert if `/memory` shows unexpected behavior.

Total estimated agent-time: 3-4 hours (per honest correction in chat — was previously overestimated as 8-10).
