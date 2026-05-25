using System.Reflection;
using System.Text.RegularExpressions;

namespace SignalCli.Tests.RegressionGuards;

/// <summary>
/// RG08 — capability <c>agent-memory-pathscoping</c>
/// (<see href="../../../openspec/changes/claude-md-rules-restructure/">claude-md-rules-restructure</see>):
/// агент-instruction memory розділена на корінь <c>CLAUDE.md</c> + path-scoped
/// <c>.claude/rules/&lt;topic&gt;.md</c> файли. Цей guard пінує саму shape сплиту,
/// щоб майбутній PR випадково не повернув монолітну форму чи не зламав
/// path-scoping declarations.
/// </summary>
/// <remarks>
/// 3 facts pinned:
/// 1. <see cref="RootClaudeMd_StaysUnder200Lines"/> — root CLAUDE.md ≤ 200 lines (Anthropic
///    Memory guidance cap; topic-content moves to <c>.claude/rules/&lt;topic&gt;.md</c>).
/// 2. <see cref="EveryRuleFile_HasValidPathsFrontmatter"/> — кожен файл під <c>.claude/rules/</c>
///    починається з YAML frontmatter <c>---\npaths:\n  - …\n---</c> або explicit
///    <c>&lt;!-- always-load: no paths --&gt;</c> marker.
/// 3. <see cref="CriticalRuleNumbers_AppearOnlyInRoot"/> — substring <c>Critical rule #N</c>
///    (N=1..18) існує у root CLAUDE.md, але НЕ існує у жодному з <c>.claude/rules/*.md</c>.
///    Topic-файли мають вживати descriptive-style cross-references замість numeric anchors,
///    бо нумерація resolve'иться тільки в root.
/// </remarks>
public class ClaudeMdSplitConsistencyTests
{
    private const int RootSizeLimit = 200;

    [Fact]
    public void RootClaudeMd_StaysUnder200Lines()
    {
        var path = LocateClaudeMd();
        var lines = File.ReadAllLines(path).Length;

        Assert.True(
            lines <= RootSizeLimit,
            $"Root CLAUDE.md grew to {lines} lines (cap is {RootSizeLimit}). " +
            "Move topic-sections into .claude/rules/<topic>.md per "
            + "`agent-memory-pathscoping` capability.");
    }

    [Fact]
    public void EveryRuleFile_HasValidPathsFrontmatter()
    {
        var rulesDir = LocateRulesDir();
        var failures = new List<string>();

        foreach (var ruleFile in Directory.EnumerateFiles(rulesDir, "*.md"))
        {
            // CRLF→LF normalization: на Windows runner'ах git checkout конвертує
            // text files у CRLF (через `* text=auto` у .gitattributes), і
            // `StartsWith("---\n")` падає, бо actual content = "---\r\n".
            // Нормалізуємо до LF щоб тест працював cross-platform.
            var content = File.ReadAllText(ruleFile).Replace("\r\n", "\n");

            // Either YAML frontmatter:  ---\npaths:\n  - "..."\n---
            // OR explicit always-load:  <!-- always-load: no paths -->  (as first non-blank line).
            var hasFrontmatter = content.StartsWith("---\n", StringComparison.Ordinal)
                                 && content.Contains("\npaths:\n", StringComparison.Ordinal);
            var hasAlwaysLoadMarker = content.StartsWith(
                "<!-- always-load: no paths -->",
                StringComparison.Ordinal);

            if (!hasFrontmatter && !hasAlwaysLoadMarker)
            {
                failures.Add(
                    $"{Path.GetFileName(ruleFile)}: missing YAML `paths:` frontmatter "
                    + "OR explicit `<!-- always-load: no paths -->` marker as first line.");
            }
        }

        Assert.True(
            failures.Count == 0,
            "Topic-file path-scoping declaration missing:\n  " + string.Join("\n  ", failures));
    }

    [Fact]
    public void CriticalRuleNumbers_AppearOnlyInRoot()
    {
        // Pattern matches "Critical rule #1" through "Critical rule #18" (covers all current rules).
        var pattern = new Regex(@"Critical rule #(?:1[0-8]|[1-9])\b", RegexOptions.Compiled);

        var rootPath = LocateClaudeMd();
        var rootMatches = CountMatches(rootPath, pattern);
        Assert.True(
            rootMatches > 0,
            "Root CLAUDE.md should contain numbered 'Critical rule #N' anchors "
            + $"(found {rootMatches}). The numbered list is the cross-ref target.");

        var rulesDir = LocateRulesDir();
        var failures = new List<string>();
        foreach (var ruleFile in Directory.EnumerateFiles(rulesDir, "*.md"))
        {
            var matches = CountMatches(ruleFile, pattern);
            if (matches > 0)
            {
                failures.Add(
                    $"{Path.GetFileName(ruleFile)}: contains {matches} 'Critical rule #N' "
                    + "reference(s). Numeric anchors only resolve within root CLAUDE.md; "
                    + "use descriptive-style cross-references inside topic files.");
            }
        }

        Assert.True(
            failures.Count == 0,
            "Topic files leak numeric Critical-rule anchors:\n  "
            + string.Join("\n  ", failures));
    }

    private static int CountMatches(string filePath, Regex pattern)
        => pattern.Count(File.ReadAllText(filePath));

    private static string LocateClaudeMd() => Path.Combine(LocateRepoRoot(), "CLAUDE.md");

    private static string LocateRulesDir() => Path.Combine(LocateRepoRoot(), ".claude", "rules");

    private static string LocateRepoRoot()
    {
        // Walk up from the test assembly until we find the repo root (where CLAUDE.md lives).
        var dir = new DirectoryInfo(
            Path.GetDirectoryName(typeof(ClaudeMdSplitConsistencyTests).Assembly.Location)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir.FullName;
    }
}
